using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Textfyre.VM;
using System.Threading;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Nito.AsyncEx;
using System.Collections.Concurrent;
using System.Diagnostics.Contracts;

namespace Guncho
{
    public class FyreVMInstance : IInstance
    {
        private readonly IInstanceSite site;
        private readonly Realm realm;
        private readonly Stream zfile;
        private readonly RealmIO io;
        private readonly string name;
        private readonly ILogger logger;

        private Engine vm;

        private bool rawMode;
        private bool needReset;
        private bool restartRequested;
        private DateTime watchdogTime = DateTime.MaxValue;
        private object watchdogLock = new object();
        private DateTime activationTime;

        // TODO: replace terpThread with a Task
        private Thread terpThread;
        private object terpThreadLock = new object();

        private readonly Dictionary<int, Player> players = new Dictionary<int, Player>();
        private readonly AsyncReaderWriterLock playersLock = new AsyncReaderWriterLock();

        private int tagstate = 0;
        private Player curPlayer = null;
        private Stack<Player> prevPlayers = new Stack<Player>();
        private StringBuilder tagParam;

        private const int MAX_LINE_LENGTH = 120;
        private const double WATCHDOG_SECONDS = 10.0;
        private static readonly Player Announcer = new Player(-1, "*Announcer*", false);

        /// <summary>
        /// Creates an new playable instance of a realm.
        /// </summary>
        /// <param name="site">The Guncho server.</param>
        /// <param name="realm">The realm to instantiate.</param>
        /// <param name="zfile">The compiled realm file.</param>
        /// <param name="name">The unique name of the instance.</param>
        /// <param name="logger">The logger.</param>
        public FyreVMInstance(IInstanceSite site, Realm realm, Stream zfile, string name, ILogger logger)
        {
            this.site = site;
            this.realm = realm;
            this.zfile = zfile;
            this.name = name;
            this.logger = logger;

            this.io = new RealmIO(this);
            this.vm = new Engine(zfile);
            vm.MaxHeapSize = Properties.Settings.Default.MaxHeapSize;
            vm.OutputReady += io.FyreOutputReady;
            vm.KeyWanted += io.FyreKeyWanted;
            vm.LineWanted += io.FyreLineWanted;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the realm's input and output are unfiltered.
        /// </summary>
        /// <remarks>
        /// Raw mode is meant to allow existing single-player games to be played online. Raw
        /// realms are unable to distinguish between different players, and cannot interact
        /// with the Guncho server or other realms.  If multiple players are connected, they
        /// will all control the same player-character and see the same output.
        /// </remarks>
        public bool RawMode
        {
            get { return rawMode; }
            set { rawMode = value; if (value) curPlayer = Announcer; }
        }

        /// <summary>
        /// Gets the realm that this instance was created from.
        /// </summary>
        public Realm Realm
        {
            get { return realm; }
        }

        /// <summary>
        /// Gets the unique name of this instance.
        /// </summary>
        public string Name
        {
            get { return name; }
        }

        /// <summary>
        /// Gets a value indicating when the realm should be considered frozen
        /// (if the watchdog time hasn't been pushed forward by then).
        /// </summary>
        public DateTime WatchdogTime
        {
            get { lock (watchdogLock) return watchdogTime; }
        }

        public bool RestartRequested
        {
            get { return restartRequested; }
            set { restartRequested = value; }
        }

        /// <summary>
        /// Prevents the watchdog timer from tripping until it's reset.
        /// </summary>
        private void DisableWatchdog()
        {
            lock (watchdogLock)
                watchdogTime = DateTime.MaxValue;
        }

        /// <summary>
        /// Sets the watchdog timer to trip in a few seconds from now, unless the timer is
        /// reset again or disabled before then.
        /// </summary>
        private void ResetWatchdog()
        {
            lock (watchdogLock)
                watchdogTime = DateTime.Now.AddSeconds(WATCHDOG_SECONDS);
        }

        /// <summary>
        /// Starts the instance's interpreter, if it isn't already running.
        /// </summary>
        public Task ActivateAsync()
        {
            return Task.Run(() =>
            {
                lock (terpThreadLock)
                {
                    if (terpThread == null)
                    {
                        terpThread = new Thread(TerpThreadProc);
                        terpThread.IsBackground = true;
                        terpThread.Name = "Realm: " + this.name;
                        terpThread.Start();

                        activationTime = DateTime.Now;

                        logger.LogMessage(LogLevel.Verbose, "Activating realm '{0}'", name);
                    }
                }
            });
        }

        /// <summary>
        /// Terminates the instance's interpreter, if it is running.
        /// </summary>
        public Task DeactivateAsync()
        {
            return Task.Run(() =>
            {
                Thread theThread;
                lock (terpThreadLock)
                {
                    theThread = terpThread;
                    terpThread = null;
                }

                if (theThread != null)
                {
                    try
                    {
                        theThread.Abort();
                        theThread.Join();
                    }
                    catch (ThreadStateException)
                    {
                        // ignore
                    }
                }
            });
        }

        /// <summary>
        /// Gets a value indicating whether the instance's interpreter is running.
        /// </summary>
        public bool IsActive
        {
            get { return terpThread != null; }
        }

        private void TerpThreadProc()
        {
            try
            {
                try
                {
                    if (needReset)
                    {
                        this.vm = new Engine(zfile);
                        vm.MaxHeapSize = Properties.Settings.Default.MaxHeapSize;
                        vm.OutputReady += io.FyreOutputReady;
                        vm.KeyWanted += io.FyreKeyWanted;
                        vm.LineWanted += io.FyreLineWanted;
                    }

                    vm.Run();
                }
                finally
                {
                    needReset = true;
                    curPlayer = null;

                    bool wasTerminated;
                    lock (terpThreadLock)
                    {
                        wasTerminated = (terpThread == null);
                        terpThread = null;
                    }

                    Player[] abandoned;
                    using (playersLock.WriterLock())
                    {
                        abandoned = new Player[players.Count];
                        players.Values.CopyTo(abandoned, 0);
                        players.Clear();
                    }

                    site.NotifyInstanceFinished(this, abandoned, wasTerminated);
                }
            }
            catch (Exception ex)
            {
                logger.LogException(ex);
                throw;
            }
        }

        public void Dispose()
        {
            DisposeAsync().Wait();
        }

        public async Task DisposeAsync()
        {
            await DeactivateAsync();

            if (zfile != null)
                zfile.Close();
        }

        /// <summary>
        /// Informs the realm that it's about to be shut down, then shuts it down.
        /// </summary>
        public async Task PolitelyDisposeAsync()
        {
            try
            {
                if (IsActive)
                    await SendAndGetAsync("$shutdown");
            }
            catch { }

            await DisposeAsync();
        }

        /// <summary>
        /// Adds a line of text to the instance's input queue.
        /// </summary>
        /// <param name="line">The text.</param>
        public void QueueInput(string line)
        {
            io.QueueInput(line);
        }

        /// <summary>
        /// Queues a line of input, waits for the instance to respond to it, and
        /// returns the response that was printed.
        /// </summary>
        /// <param name="line">The line of input to send.</param>
        /// <returns>The text that was printed in response to the line, or
        /// an empty string if the transaction timed out.</returns>
        public async Task<string> SendAndGetAsync(string line)
        {
            Transaction trans = new Transaction(line);
            io.QueueTransaction(trans);

            var delay = Task.Delay(Properties.Settings.Default.TransactionTimeout);

            await Task.WhenAny(trans.WhenCompleted, delay);
            return trans.Response.ToString().Trim();
        }

        /// <summary>
        /// Adds a player to the instance.
        /// </summary>
        /// <param name="player">The player who is joining.</param>
        /// <param name="position">A string describing the player's location,
        /// as returned by <see cref="ExportPlayerPositionsAsync"/>, or
        /// <b>null</b>.</param>
        public Task AddPlayerAsync(Player player, string position)
        {
            using (playersLock.WriterLock())
            {
                players.Add(player.ID, player);
            }

            if (!rawMode)
                QueueInput(string.Format("$join {0}={1}{2}",
                    player.Name,
                    player.ID,
                    position == null ? "" : "," + position));

            return TaskConstants.Completed;
        }

        /// <summary>
        /// Removes a player from the instance.
        /// </summary>
        /// <param name="player">The player who is leaving.</param>
        public async Task RemovePlayerAsync(Player player)
        {
            if (!rawMode)
            {
                string result = await SendAndGetAsync(string.Format("$part {0}", player.ID));
                await HandleOutputAsync(result);
                await FlushAllAsync();
            }

            using (await playersLock.WriterLockAsync())
                players.Remove(player.ID);
        }

        /// <summary>
        /// Gets a list of all players who are in the instance.
        /// </summary>
        /// <returns>An array of players.</returns>
        public Player[] ListPlayers()
        {
            Player[] result;
            using (playersLock.ReaderLock())
            {
                result = new Player[players.Count];
                players.Values.CopyTo(result, 0);
            }
            return result;
        }

        /// <summary>
        /// Fills a dictionary with strings describing the locations of each
        /// player in the realm, and removes those players from the realm.
        /// </summary>
        /// <param name="results">The dictionary to fill.</param>
        /// <remarks>If an exception occurs while retrieving any player's
        /// location string, that player will be added to the dictionary
        /// with a <b>null</b> value.</remarks>
        public async Task ExportPlayerPositionsAsync(IDictionary<Player, string> results)
        {
            if (results == null)
                throw new ArgumentNullException(nameof(results));
            if (results.IsReadOnly)
                throw new ArgumentException("Dictionary is read only", nameof(results));

            Player[] temp;
            using (await playersLock.WriterLockAsync())
            {
                temp = new Player[players.Count];
                players.Values.CopyTo(temp, 0);
                players.Clear();
            }

            foreach (Player p in temp)
            {
                string locationStr;
                try
                {
                    locationStr = await SendAndGetAsync("$locate " + p.ID.ToString());
                }
                catch
                {
                    results.Add(p, null);
                    continue;
                }

                results.Add(p, locationStr);
            }
        }

        private async Task FlushAllAsync()
        {
            Task[] tasks;

            using (await playersLock.ReaderLockAsync())
            {
                tasks = players.Values.Select(p => site.FlushPlayerAsync(p)).ToArray();
            }

            await Task.WhenAll(tasks);
        }

        private async Task HandleOutputAsync(string text)
        {
            foreach (char c in text)
                await HandleOutputAsync(c);
        }

        private async Task HandleOutputAsync(char c)
        {
            if (rawMode)
            {
                await SendCurPlayerAsync(c);
                return;
            }

            switch (tagstate)
            {
                case 0:
                    // outside any tag
                    if (c == '<')
                        tagstate++;
                    else
                        await SendCurPlayerAsync(c);
                    break;

                case 1:
                    // got opening bracket
                    if (c == '$')
                    {
                        tagstate = 100;
                    }
                    else if (c == '/')
                    {
                        tagstate = 200;
                    }
                    else
                    {
                        await SendCurPlayerAsync('<');
                        tagstate = 0;
                    }
                    break;

                case 100:
                    // got <$
                    if (c == 't')
                    {
                        tagstate = 110;
                    }
                    else if (c == 'a')
                    {
                        tagstate = 120;
                    }
                    else if (c == 'b')
                    {
                        tagstate = 130;
                    }
                    else if (c == 'd')
                    {
                        tagstate = 140;
                    }
                    else
                    {
                        await SendCurPlayerAsync("<$");
                        tagstate = 0;
                    }
                    break;

                case 110:
                    // got <$t
                    if (c == ' ')
                    {
                        tagstate++;
                    }
                    else
                    {
                        await SendCurPlayerAsync("<$t");
                        tagstate = 0;
                    }
                    break;

                case 111:
                    // got <$t_
                    if ((c >= '0' && c <= '9') || (c == '-'))
                    {
                        tagstate++;
                        tagParam = new StringBuilder(6);
                        tagParam.Append(c);
                    }
                    else
                    {
                        await SendCurPlayerAsync("<$t ");
                        tagstate = 0;
                    }
                    break;

                case 112:
                    // got <$t_ and some digits
                    if (c >= '0' && c <= '9')
                    {
                        tagParam.Append(c);
                    }
                    else if (c == '>')
                    {
                        // done
                        prevPlayers.Push(curPlayer);

                        int playerNum = int.Parse(tagParam.ToString());
                        players.TryGetValue(playerNum, out curPlayer);
                        if (curPlayer != null)
                            await site.SendLineToPlayerAsync(curPlayer);
                        tagParam = null;
                        tagstate = 0;
                    }
                    else
                    {
                        await SendCurPlayerAsync("<$t " + tagParam.ToString());
                        tagstate = 0;
                    }
                    break;

                case 120:
                    // got <$a
                    if (c == '>')
                    {
                        prevPlayers.Push(curPlayer);
                        curPlayer = Announcer;
                        tagstate = 0;
                    }
                    else
                    {
                        await SendCurPlayerAsync("<$a");
                        tagstate = 0;
                    }
                    break;

                case 130:
                    // got <$b
                    if (c == ' ')
                    {
                        tagstate++;
                    }
                    else
                    {
                        await SendCurPlayerAsync("<$b");
                        tagstate = 0;
                    }
                    break;

                case 131:
                    // got <$b_
                    tagParam = new StringBuilder();
                    tagstate++;
                    goto case 132;

                case 132:
                    // got <$b_ and 0 or more characters
                    if (c == '>')
                    {
                        // done
                        TransferCurPlayer(tagParam.ToString());
                        tagstate = 0;
                        tagParam = null;
                    }
                    else
                    {
                        tagParam.Append(c);
                    }
                    break;

                case 140:
                    // got <$d
                    if (c == ' ')
                    {
                        tagstate++;
                    }
                    else
                    {
                        await SendCurPlayerAsync("<$d");
                        tagstate = 0;
                    }
                    break;

                case 141:
                    // got <$d_
                    tagParam = new StringBuilder();
                    tagstate++;
                    goto case 142;

                case 142:
                    // got <$d_ and 0 or more characters
                    if (c == '>')
                    {
                        // done
                        DisambiguateCurPlayer(tagParam.ToString());
                        tagstate = 0;
                        tagParam = null;
                    }
                    else
                    {
                        tagParam.Append(c);
                    }
                    break;

                case 200:
                    // got </
                    if (c == '$')
                    {
                        tagstate++;
                    }
                    else
                    {
                        await SendCurPlayerAsync("</");
                        tagstate = 0;
                    }
                    break;

                case 201:
                    // got </$
                    if (c == 't')
                    {
                        tagstate = 210;
                    }
                    else if (c == 'a')
                    {
                        tagstate = 220;
                    }
                    else
                    {
                        await SendCurPlayerAsync("</$");
                        tagstate = 0;
                    }
                    break;

                case 210:
                    // got </$t
                    if (c == '>')
                    {
                        if (prevPlayers.Count >= 1)
                            curPlayer = prevPlayers.Pop();
                        tagstate = 0;
                    }
                    else
                    {
                        await SendCurPlayerAsync("</$t");
                        tagstate = 0;
                    }
                    break;

                case 220:
                    // got </$a
                    if (c == '>')
                    {
                        if (prevPlayers.Count >= 1)
                            curPlayer = prevPlayers.Pop();
                        tagstate = 0;
                    }
                    else
                    {
                        await SendCurPlayerAsync("</$a");
                        tagstate = 0;
                    }
                    break;
            }
        }

        private async Task SendCurPlayerAsync(char c)
        {
            if (curPlayer == Announcer)
            {
                Task[] tasks;

                using (await playersLock.ReaderLockAsync())
                {
                    if (c == '\n')
                    {
                        tasks = players.Values.Select(p => site.SendLineToPlayerAsync(p)).ToArray();
                    }
                    else
                    {
                        tasks = players.Values.Select(p => site.SendToPlayerAsync(p, c)).ToArray();
                    }
                }

                await Task.WhenAll(tasks);
            }
            else if (curPlayer != null)
            {
                if (c == '\n')
                    await site.SendLineToPlayerAsync(curPlayer);
                else
                    await site.SendToPlayerAsync(curPlayer, c);
            }
            else
            {
#if CONSOLE_SPAM
                // redirect output that no one should see to the console
                if (c == '\n')
                    Console.WriteLine();
                else
                    Console.Write(c);
#endif
            }
        }

        private async Task SendCurPlayerAsync(string str)
        {
            str = str.Replace("\n", "\r\n");

            if (curPlayer == Announcer)
            {
                Task[] tasks;

                using (await playersLock.ReaderLockAsync())
                {
                    tasks = players.Values.Select(p => site.SendToPlayerAsync(p, str)).ToArray();
                }

                await Task.WhenAll(tasks);
            }
            else if (curPlayer != null)
            {
                await site.SendToPlayerAsync(curPlayer, str);
            }
            else
            {
#if CONSOLE_SPAM
                Console.Write(str);
#endif
            }
        }

        private void TransferCurPlayer(string spec)
        {
            if (curPlayer == Announcer || curPlayer == null)
            {
                logger.LogMessage(LogLevel.Warning,
                    "Illegal transfer (curPlayer is {0}) attempted from {1} to {2}.",
                    curPlayer == Announcer ? "Announcer" : "null",
                    name, spec);
            }
            else
            {
                site.TransferPlayer(curPlayer, spec);
            }
        }

        private void DisambiguateCurPlayer(string info)
        {
            if (curPlayer == Announcer || curPlayer == null)
            {
                logger.LogMessage(LogLevel.Warning,
                    "Illegal (curPlayer is {0}) disambiguation mode request.",
                    curPlayer == Announcer ? "Announcer" : "null");
            }
            else
            {
                using (curPlayer.Lock.WriterLock())
                    curPlayer.Disambiguating = info;
            }
        }

        private bool AllowSetPlayerAttribute(string name, string value)
        {
            // TODO: establish rules for setting player attributes

            switch (name)
            {
                case "waygone":
                    return true;
            }

            return false;
        }

        #region RealmIO

        private class RealmIO
        {
            private readonly FyreVMInstance instance;
            private readonly AsyncProducerConsumerQueue<object> inputQueue = new AsyncProducerConsumerQueue<object>();  // string
            private readonly AsyncProducerConsumerQueue<object> transQueue = new AsyncProducerConsumerQueue<object>();  // Transaction
            private readonly ConcurrentQueue<string> specialResponses = new ConcurrentQueue<string>();
            private Transaction curTrans;

            public RealmIO(FyreVMInstance instance)
            {
                this.instance = instance;
            }

            public void QueueInput(string line)
            {
                inputQueue.Enqueue(line);
            }

            public void QueueTransaction(Transaction trans)
            {
                transQueue.Enqueue(trans);
            }

            private static Regex chatRegex = new Regex(@"^(-?\d+):\$(say|emote) (?:\>([^ ]*) )?(.*)$");

            private async Task<string> GetInputLineAsync()
            {
                string specialResponse;
                if (specialResponses.TryDequeue(out specialResponse))
                    return specialResponse;

                instance.DisableWatchdog();

                try
                {
                    instance.prevPlayers.Clear();
                    await instance.FlushAllAsync();

                    if (curTrans is DisambigHelper)
                        instance.curPlayer = ((DisambigHelper)curTrans).Player;
                    else if (!instance.rawMode)
                        instance.curPlayer = null;

                    // is there a previous transaction?
                    if (curTrans != null)
                    {
                        // response has already been collected, so just complete the task
                        curTrans.Complete();
                        curTrans = null;
                    }

                    var queues = new AsyncProducerConsumerQueue<object>[] { transQueue, inputQueue };

                    do
                    {
                        // wait for a transaction or a line of player input
                        var dqResult = await queues.TryDequeueFromAnyAsync();

                        if (!dqResult.Success)
                            throw new InvalidOperationException("failed to get input");

                        if (dqResult.Queue == transQueue)
                        {
                            // got a transaction
                            curTrans = (Transaction)dqResult.Item;
                            instance.logger.LogMessage(LogLevel.Spam,
                                "Transaction in {0}: {1}",
                                instance.name, curTrans.Query);
                            return curTrans.Query;
                        }
                        else
                        {
                            Contract.Assert(dqResult.Queue == inputQueue);

                            // got a line of player input
                            string line = (string)dqResult.Item;
                            instance.logger.LogMessage(LogLevel.Spam,
                                "Processing in {0}: {1}",
                                instance.name, line);

                            if (!instance.rawMode)
                            {
                                if (line.StartsWith("$silent ", StringComparison.Ordinal))
                                {
                                    // set up a fake transaction so the output will be hidden
                                    // and the next line's output substituted instead
                                    line = line.Substring(8);
                                    curTrans = new DisambigHelper(instance, line);
                                }
                                else
                                {
                                    Match m = chatRegex.Match(line);
                                    if (m.Success)
                                    {
                                        string id = m.Groups[1].Value;
                                        string type = m.Groups[2].Value;
                                        string target = m.Groups[3].Value;
                                        string msg = m.Groups[4].Value;
                                        instance.chatTargetRegister = target;
                                        instance.chatMsgRegister = msg;
                                        line = id + ":$" + type;
                                    }
                                }
                            }

                            if (line.Length > MAX_LINE_LENGTH)
                                line = line.Substring(0, MAX_LINE_LENGTH);

                            return line;
                        }
                    } while (true);
                }
                finally
                {
                    instance.ResetWatchdog();
                }
            }

            public void FyreLineWanted(object sender, LineWantedEventArgs e)
            {
                e.Line = GetInputLineAsync().Result;
            }

            public void FyreKeyWanted(object sender, KeyWantedEventArgs e)
            {
                string line;
                do
                {
                    line = GetInputLineAsync().Result;
                }
                while (line == null || line.Length < 1);

                e.Char = line[0];
            }

            public void FyreOutputReady(object sender, OutputReadyEventArgs e)
            {
                string main;
                if (e.Package.TryGetValue(OutputChannel.Main, out main) == true)
                {
                    if (curTrans != null)
                        curTrans.Response.Append(main);
                    else
                        instance.HandleOutputAsync(main).Wait();
                }

                string special;
                if (e.Package.TryGetValue(OutputChannel.Conversation, out special) && special.Length > 0)
                {
                    string[] parts = special.Split(new char[] { ' ' }, 3);
                    if (parts.Length > 0)
                    {
                        string name = (parts.Length > 1) ? parts[1] : "";
                        string rest = (parts.Length > 2) ? parts[2] : "";
                        int word;
                        int chunkSize;
                        string str;

                        switch (parts[0])
                        {
                            case "getword":
                                if (instance.TryGetWordRegister(name, out word))
                                    specialResponses.Enqueue(word.ToString());
                                else
                                    specialResponses.Enqueue("-1");
                                break;

                            case "gettext":
                                str = instance.GetTextRegister(name) ?? "";
                                specialResponses.Enqueue(str.Length.ToString());
                                if (!int.TryParse(rest, out chunkSize))
                                    chunkSize = str.Length;
                                for (int offset = 0; offset < str.Length; offset += chunkSize)
                                    specialResponses.Enqueue(
                                        str.Substring(offset, Math.Min(chunkSize, str.Length - offset)));
                                break;

                            case "putword":
                                if (int.TryParse(rest, out word) && instance.TryPutWordRegister(name, word))
                                    specialResponses.Enqueue("1");
                                else
                                    specialResponses.Enqueue("0");
                                break;

                            case "puttext":
                                instance.PutTextRegister(name, rest);
                                specialResponses.Enqueue("?");
                                break;
                        }
                    }
                }
            }
        }

        private class Transaction
        {
            private readonly TaskCompletionSource tcs = new TaskCompletionSource();

            public Transaction(string query)
            {
                this.Query = query;
            }

            public readonly string Query;
            public readonly StringBuilder Response = new StringBuilder();

            public Task WhenCompleted => tcs.Task;

            public void Complete()
            {
                tcs.SetResult();
            }
        }

        private class DisambigHelper : Transaction
        {
            /// <summary>
            /// Constructs a new DisambigHelper.
            /// </summary>
            /// <param name="instance">The instance where disambiguation is occurring.</param>
            /// <param name="line">The command that will need disambiguation, starting with
            /// the player ID and a colon.</param>
            public DisambigHelper(FyreVMInstance instance, string line)
                : base("")
            {
                string[] parts = line.Split(':');
                int num;
                if (parts.Length >= 2 && int.TryParse(parts[0], out num))
                    using (instance.playersLock.ReaderLock())
                        instance.players.TryGetValue(num, out this.Player);
            }

            public readonly Player Player;
        }

        #endregion

        #region Server Registers

        // for working with text registers
        private int strRegPtr;
        private StringBuilder strRegister;  // txtl, txtd
        private string strRegName;          // txtn

        // for player queries
        private Player queriedPlayer;       // pq_id
        private string queriedAttr;         // pq_attr

        // word registers available to the game
        private int timerInterval;          // rteinterval

        // text registers available to the game
        private string chatMsgRegister;     // chatmsg
        private string chatTargetRegister;  // chattarget

        private bool TryGetWordRegister(string name, out int value)
        {
            DateTime dt;

            switch (name)
            {
                case "timeq":
                    // time in quarts (15-second intervals)
                    dt = DateTime.Now;
                    value = (dt.Hour * 60 + dt.Minute) * 4 + (dt.Second / 15);
                    return true;

                case "timehm":
                    // time as hhmm
                    dt = DateTime.Now;
                    value = (dt.Hour * 100) + dt.Minute;
                    return true;

                case "times":
                    // seconds past the minute
                    dt = DateTime.Now;
                    value = dt.Second;
                    return true;

                case "datemd":
                    // date as mmdd
                    dt = DateTime.Now;
                    value = (dt.Month * 100) + dt.Day;
                    return true;

                case "datey":
                    // year
                    dt = DateTime.Now;
                    value = dt.Year;
                    return true;

                case "datedow":
                    // day of the week (0 = Sunday)
                    dt = DateTime.Now;
                    value = (int)dt.DayOfWeek;
                    return true;

                case "txtl":
                    // text register length
                    if (strRegister == null && strRegName != null)
                    {
                        string text = GetTextRegister(strRegName);
                        if (text != null)
                            strRegister = new StringBuilder(text);
                    }
                    if (strRegister == null)
                        value = 0;
                    else
                        value = strRegister.Length;
                    return true;

                case "pq_id":
                case "ls_playerid":
                    // ID for player queries
                    if (queriedPlayer == null)
                        value = 0;
                    else
                        value = queriedPlayer.ID;
                    return true;

                case "rteinterval":
                    // real-time event timer interval
                    value = timerInterval;
                    return true;

                default:
                    value = 0;
                    return false;
            }
        }

        private bool TryPutWordRegister(string name, int value)
        {
            switch (name)
            {
                case "txtl":
                    // declare length of incoming text
                    strRegPtr = 0;
                    strRegister = new StringBuilder(value);
                    return true;

                case "pq_id":
                case "ls_playerid":
                    // select player to query
                    using (playersLock.ReaderLock())
                        players.TryGetValue(value, out queriedPlayer);
                    break;

                case "rteinterval":
                    // change real-time event timer interval
                    timerInterval = value;
                    site.SetEventIntervalAsync(this, value).Wait();
                    break;
            }

            return false;
        }

        private string GetTextRegister(string name)
        {
            switch (name)
            {
                case "chatmsg":
                    // complete, capitalized chat text
                    return chatMsgRegister;

                case "chattarget":
                    // target of directed chats
                    return chatTargetRegister;

                case "pq_attrval":
                    // value of selected player attribute
                    if (queriedPlayer != null && queriedAttr != null)
                    {
                        switch (queriedAttr)
                        {
                            case "accesslevel":
                                return realm.GetAccessLevel(queriedPlayer).ToString();

                            case "idletime":
                                var idleTime = site.GetPlayerIdleTime(queriedPlayer);
                                if (idleTime != null)
                                    return Server.FormatTimeSpan(idleTime.Value);
                                return "";
                        }

                        using (queriedPlayer.Lock.ReaderLock())
                            return queriedPlayer.GetAttribute(queriedAttr);
                    }
                    return null;

                case "ls_realmval":
                    // value of selected realm storage register
                    if (queriedAttr != null)
                        return realm.GetRealmStorage(queriedAttr);
                    else
                        return "";

                case "ls_playerval":
                    // value of selected player storage register
                    if (queriedPlayer != null && queriedAttr != null)
                        return realm.GetPlayerStorage(queriedPlayer, queriedAttr);
                    else
                        return "";
            }

            return null;
        }

        private void PutTextRegister(string name, string value)
        {
            switch (name)
            {
                case "pq_attr":
                case "ls_attr":
                    // select player attribute or realm/player storage register to query
                    queriedAttr = value;
                    break;

                case "pq_attrval":
                    // change value of selected player attribute
                    if (queriedPlayer != null && queriedAttr != null &&
                        AllowSetPlayerAttribute(queriedAttr, value))
                    {
                        using (queriedPlayer.Lock.WriterLock())
                            queriedPlayer.SetAttribute(queriedAttr, value);
                    }
                    break;

                case "ls_realmval":
                    // change value of selected realm storage register
                    if (queriedAttr != null)
                        realm.SetRealmStorage(queriedAttr, value);
                    break;

                case "ls_playerval":
                    // change value of selected player storage register
                    if (queriedPlayer != null && queriedAttr != null)
                        realm.SetPlayerStorage(queriedPlayer, queriedAttr, value);
                    break;
            }
        }

        private bool TryGetBlockRegister(string name, int size, out byte[] block)
        {
            switch (name)
            {
                case "txtd":
                    // text register data
                    if (strRegister != null)
                    {
                        int count = Math.Min(size, strRegister.Length - strRegPtr);
                        block = new byte[count];
                        Encoding.ASCII.GetBytes(strRegister.ToString(), strRegPtr, count, block, 0);
                        strRegPtr += count;
                        if (strRegPtr >= strRegister.Length)
                        {
                            strRegName = null;
                            strRegister = null;
                            strRegPtr = 0;
                        }
                        return true;
                    }
                    break;
            }

            block = null;
            return false;
        }

        private void PutRegister(string name, byte[] block)
        {
            if (block.Length == 2)
            {
                ushort value = (ushort)((block[0] << 8) + block[1]);
                if (TryPutWordRegister(name, value))
                    return;
            }

            switch (name)
            {
                case "txtn":
                    // text register name
                    strRegName = Encoding.ASCII.GetString(block);
                    strRegister = null;
                    strRegPtr = 0;
                    break;

                case "txtd":
                    // text register data
                    if (strRegister != null)
                    {
                        strRegister.Append(Encoding.ASCII.GetString(block));
                        if (strRegister.Length >= strRegister.Capacity)
                        {
                            PutTextRegister(strRegName, strRegister.ToString());
                            strRegName = null;
                            strRegister = null;
                            strRegPtr = 0;
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// A <see cref="MemoryStream"/> that runs a delegate when it fills up.
        /// </summary>
        private class ReactiveMemoryStream : MemoryStream
        {
            private int fillLength;
            private EventHandler whenFilled;

            public ReactiveMemoryStream(int size, EventHandler whenFilled)
                : base(size)
            {
                this.fillLength = size;
                this.whenFilled = whenFilled;
            }

            public ReactiveMemoryStream(byte[] buffer, EventHandler whenFilled)
                : base(buffer)
            {
                this.fillLength = buffer.Length;
                this.whenFilled = whenFilled;
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                base.Write(buffer, offset, count);
                if (this.Position >= fillLength && whenFilled != null)
                    whenFilled(this, EventArgs.Empty);
            }
        }

        #endregion
    }
}
