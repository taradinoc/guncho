using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Textfyre.VM;
using System.Threading;
using System.Text.RegularExpressions;

namespace Guncho
{
    abstract class Instance
    {
        protected readonly Server server;
        private readonly Realm realm;
        private readonly Stream zfile;
        private readonly RealmIO io;
        private readonly string name;

        private Engine vm;

        private bool needReset;
        private bool restartRequested;
        private DateTime watchdogTime = DateTime.MaxValue;
        private object watchdogLock = new object();
        private DateTime activationTime;
        private Thread terpThread;
        private object terpThreadLock = new object();

        private const int MAX_LINE_LENGTH = 120;
        private const double WATCHDOG_SECONDS = 10.0;

        protected virtual void OnPreReadLine(Transaction curTrans)
        {
            // nada
        }
        protected virtual void OnPostReadLine(ref string line, ref Transaction curTrans)
        {
            // nada
        }
        protected abstract void OnHandleOutput(string text);
        protected abstract void OnVMStarting();
        protected abstract void OnVMFinished(bool wasTerminated);

        protected static string GetToken(char sep, ref string text)
        {
            int idx = text.IndexOf(sep);

            string result;
            if (idx == -1)
            {
                result = text;
                text = string.Empty;
            }
            else
            {
                result = text.Substring(0, idx);
                text = text.Substring(idx + 1);
            }

            return result;
        }

        /// <summary>
        /// Creates an new playable instance of a realm.
        /// </summary>
        /// <param name="server">The Guncho server.</param>
        /// <param name="realm">The realm to instantiate.</param>
        /// <param name="zfile">The compiled realm file.</param>
        /// <param name="name">The unique name of the instance.</param>
        public Instance(Server server, Realm realm, Stream zfile, string name)
        {
            this.server = server;
            this.realm = realm;
            this.zfile = zfile;
            this.name = name;

            this.io = new RealmIO(this);
            this.vm = new Engine(zfile);
            vm.MaxHeapSize = Properties.Settings.Default.MaxHeapSize;
            vm.OutputReady += io.FyreOutputReady;
            vm.KeyWanted += io.FyreKeyWanted;
            vm.LineWanted += io.FyreLineWanted;
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
        public void Activate()
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

                    server.LogMessage(LogLevel.Verbose, "Activating realm '{0}'", name);
                }
            }
        }

        /// <summary>
        /// Terminates the instance's interpreter, if it is running.
        /// </summary>
        public void Deactivate()
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

                    actionMap = new TranslationMap<ActionInfo>();
                    kindMap = new IntTranslationMap();
                    propMap = new IntTranslationMap();

                    OnVMStarting();
                    vm.Run();
                }
                finally
                {
                    needReset = true;

                    bool wasTerminated;
                    lock (terpThreadLock)
                    {
                        wasTerminated = (terpThread == null);
                        terpThread = null;
                    }

                    OnVMFinished(wasTerminated);

                    actionMap = null;
                    kindMap = null;
                    propMap = null;
                }
            }
            catch (Exception ex)
            {
                server.LogException(ex);
                throw;
            }
        }

        public void Dispose()
        {
            Deactivate();

            if (zfile != null)
                zfile.Close();
        }

        /// <summary>
        /// Informs the realm that it's about to be shut down, then shuts it down.
        /// </summary>
        public void PolitelyDispose()
        {
            try
            {
                if (IsActive)
                    SendAndGet("$shutdown");
            }
            catch { }

            Dispose();
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
        /// Adds a <see cref="Transaction"/> to the instance's input queue.
        /// </summary>
        /// <param name="trans">The transaction.</param>
        protected void QueueTransaction(Transaction trans)
        {
            io.QueueTransaction(trans);
        }

        /// <summary>
        /// Queues a line of input, waits for the instance to respond to it, and
        /// returns the response that was printed.
        /// </summary>
        /// <param name="line">The line of input to send.</param>
        /// <returns>The text that was printed in response to the line, or
        /// <b>null</b> if the realm timed out.</returns>
        public string SendAndGet(string line)
        {
            Transaction trans = new Transaction(line);

            lock (trans)
            {
                io.QueueTransaction(trans);
                Monitor.Wait(trans, Properties.Settings.Default.TransactionTimeout);
            }

            return trans.Response.ToString().Trim();
        }

        #region RealmIO

        private class RealmIO
        {
            private readonly Instance instance;
            private readonly Queue<string> inputQueue = new Queue<string>();
            private readonly Queue<Transaction> transQueue = new Queue<Transaction>();
            private readonly Queue<string> specialResponses = new Queue<string>();
            private readonly AutoResetEvent inputReady = new AutoResetEvent(false);
            private Transaction curTrans;

            public RealmIO(Instance instance)
            {
                this.instance = instance;
            }

            public void QueueInput(string line)
            {
                lock (inputQueue)
                {
                    inputQueue.Enqueue(line);

                    if (inputQueue.Count == 1)
                        inputReady.Set();
                }
            }

            public void QueueTransaction(Transaction trans)
            {
                lock (transQueue)
                {
                    transQueue.Enqueue(trans);

                    if (transQueue.Count == 1)
                        inputReady.Set();
                }
            }

            private string GetInputLine()
            {
                if (specialResponses.Count > 0)
                    return specialResponses.Dequeue();

                instance.DisableWatchdog();

                try
                {
                    instance.OnPreReadLine(curTrans);

                    // is there a previous transaction?
                    if (curTrans != null)
                    {
                        // response has already been collected, so just notify the waiting thread
                        lock (curTrans)
                        {
                            curTrans.OnFinished(instance);
                            Monitor.Pulse(curTrans);
                        }

                        curTrans = null;
                    }

                    do
                    {
                        // is there a transaction waiting?
                        lock (transQueue)
                        {
                            if (transQueue.Count > 0)
                            {
                                curTrans = transQueue.Dequeue();
                                instance.server.LogMessage(LogLevel.Spam,
                                    "Transaction in {0}: {1}",
                                    instance.name, curTrans.Query);
                                return curTrans.Query;
                            }
                        }

                        // is there a line of player input waiting?
                        lock (inputQueue)
                        {
                            if (inputQueue.Count > 0)
                            {
                                string line = inputQueue.Dequeue();
                                instance.server.LogMessage(LogLevel.Spam,
                                    "Processing in {0}: {1}",
                                    instance.name, line);

                                instance.OnPostReadLine(ref line, ref curTrans);

                                if (line.Length > MAX_LINE_LENGTH)
                                    line = line.Substring(0, MAX_LINE_LENGTH);

                                return line;
                            }
                        }

                        // wait for input and then continue the loop
                        inputReady.WaitOne();
                    } while (true);
                }
                finally
                {
                    instance.ResetWatchdog();
                }
            }

            public void FyreLineWanted(object sender, LineWantedEventArgs e)
            {
                e.Line = GetInputLine();
            }

            public void FyreKeyWanted(object sender, KeyWantedEventArgs e)
            {
                string line;
                do
                {
                    line = GetInputLine();
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
                        instance.OnHandleOutput(main);
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

        protected class Transaction
        {
            public Transaction(string query)
            {
                this.Query = query;
            }

            public readonly string Query;
            public readonly StringBuilder Response = new StringBuilder();

            public virtual void OnFinished(Instance instance)
            {
                // nada
            }
        }

        #endregion

        #region Server Registers

        // word registers available to the instance
        private int timerInterval;              // rteinterval

        // text registers available to the instance
        protected string chatMsgRegister;       // chatmsg
        protected string chatTargetRegister;    // chattarget

        // for attribute and realm storage queries
        protected string queriedAttr;           // pq_attr

        protected virtual bool TryGetWordRegister(string name, out int value)
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

                case "rteinterval":
                    // real-time event timer interval
                    value = timerInterval;
                    return true;

                default:
                    value = 0;
                    return false;
            }
        }

        protected virtual bool TryPutWordRegister(string name, int value)
        {
            switch (name)
            {
                case "rteinterval":
                    // change real-time event timer interval
                    timerInterval = value;
                    server.SetEventInterval(this, value);
                    break;
            }

            return false;
        }

        protected virtual string GetTextRegister(string name)
        {
            switch (name)
            {
                case "chatmsg":
                    // complete, capitalized chat text
                    return chatMsgRegister;

                case "chattarget":
                    // target of directed chats
                    return chatTargetRegister;

                case "ls_realmval":
                    // value of selected realm storage register
                    if (queriedAttr != null)
                        return realm.GetRealmStorage(queriedAttr);
                    else
                        return "";

            }

            return null;
        }

        protected virtual void PutTextRegister(string name, string value)
        {
            switch (name)
            {
                case "pq_attr":
                case "ls_attr":
                    // select player attribute or realm/player storage register to query
                    queriedAttr = value;
                    break;

                case "ls_realmval":
                    // change value of selected realm storage register
                    if (queriedAttr != null)
                        realm.SetRealmStorage(queriedAttr, value);
                    break;
            }
        }

        #endregion

        #region Action/Kind/Property Translation

        protected enum ArgType
        {
            Omitted,
            Other,
            Boolean,
            Number,
            Object,
            Text,
        }

        protected static bool TryParseArgType(string text, out ArgType value)
        {
            switch (text)
            {
                case ".":
                    value = ArgType.Omitted;
                    return true;
                case "b":
                    value = ArgType.Boolean;
                    return true;
                case "n":
                    value = ArgType.Number;
                    return true;
                case "o":
                    value = ArgType.Object;
                    return true;
                case "t":
                    value = ArgType.Text;
                    return true;
                case "?":
                    value = ArgType.Other;
                    return true;
                default:
                    value = default(ArgType);
                    return false;
            }
        }

        protected interface IProvideNumber
        {
            int Number { get; }
        }

        protected struct ActionInfo : IProvideNumber
        {
            public int Number;
            public ArgType ArgType1;
            public ArgType ArgType2;

            int IProvideNumber.Number
            {
                get { return Number; }
            }
        }

        protected struct IntInfo : IProvideNumber
        {
            public int Number;

            public static implicit operator IntInfo(int num)
            {
                IntInfo result;
                result.Number = num;
                return result;
            }

            int IProvideNumber.Number
            {
                get { return Number; }
            }
        }

        protected class TranslationMap<T>
            where T : struct, IProvideNumber
        {
            private readonly Dictionary<int, string> names = new Dictionary<int, string>();
            private readonly Dictionary<string, T> values = new Dictionary<string, T>();

            public void Add(string name, T value)
            {
                names[value.Number] = name;
                values[name] = value;
            }

            public string GetName(int number)
            {
                string result;
                names.TryGetValue(number, out result);
                return result;
            }

            public int? GetNumber(string name)
            {
                T info;
                if (values.TryGetValue(name, out info))
                    return info.Number;
                else
                    return null;
            }

            public T? GetInfo(string name)
            {
                T result;
                if (values.TryGetValue(name, out result))
                    return result;
                else
                    return null;
            }
        }

        protected class IntTranslationMap : TranslationMap<IntInfo> { }

        protected TranslationMap<ActionInfo> actionMap;
        protected IntTranslationMap kindMap, propMap;

        protected void RegisterProperty(string line)
        {
            // num type name
            // ... but we actually treat type+name as the name
            string word = GetToken(' ', ref line);
            int num;

            if (int.TryParse(word, out num))
                propMap.Add(line, num);
        }

        protected void RegisterKind(string line)
        {
            // num name
            string word = GetToken(' ', ref line);
            int num;

            if (int.TryParse(word, out num))
                kindMap.Add(line, num);
        }

        protected void RegisterAction(string line)
        {
            // num type1 type2 name
            // ... but we actually treat type1+type2+name as the name
            string word = GetToken(' ', ref line);
            ActionInfo info;

            if (int.TryParse(word, out info.Number))
            {
                string typedName = line;

                word = GetToken(' ', ref line);
                if (!TryParseArgType(word, out info.ArgType1))
                    return;

                word = GetToken(' ', ref line);
                if (!TryParseArgType(word, out info.ArgType2))
                    return;

                actionMap.Add(line, info);
            }
        }

        #endregion
    }

    class GameInstance : Instance
    {
        private bool rawMode;
        private Dictionary<int, Player> players = new Dictionary<int, Player>();
        private Dictionary<Player, int> playerIDs = new Dictionary<Player, int>();
        private int nextPlayerID = MIN_PLAYER_ID;

        private const int MIN_PLAYER_ID = 1;
        private const int MAX_PLAYER_ID = 32767;

        private int tagstate = 0;
        private Player curPlayer = null;
        private Stack<Player> prevPlayers = new Stack<Player>();
        private StringBuilder tagParam;

        private static readonly Player Announcer = new DummyPlayer("*Announcer*");
        private static Regex chatRegex = new Regex(@"^(-?\d+):\$(say|emote) (?:\>([^ ]*) )?(.*)$");

        public GameInstance(Server server, Realm realm, Stream zfile, string name)
            : base(server, realm, zfile, name)
        {
        }

        protected override void OnVMStarting()
        {
            QueueTransaction(new RealmGreeting());
        }

        protected override void OnVMFinished(bool wasTerminated)
        {
            curPlayer = null;
            Player[] abandoned;
            lock (players)
            {
                abandoned = new Player[players.Count];
                players.Values.CopyTo(abandoned, 0);
                players.Clear();
                playerIDs.Clear();
                nextPlayerID = MIN_PLAYER_ID;
            }

            foreach (Player p in abandoned)
                p.FlushOutput();

            server.InstanceFinished(this, abandoned, wasTerminated);
        }

        protected override void OnPreReadLine(Transaction curTrans)
        {
            prevPlayers.Clear();
            FlushAll();

            if (curTrans is DisambigHelper)
                curPlayer = ((DisambigHelper)curTrans).Player;
            else if (!rawMode)
                curPlayer = null;
        }

        protected override void OnPostReadLine(ref string line, ref Transaction curTrans)
        {
            if (!rawMode)
            {
                if (line.StartsWith("$silent "))
                {
                    // set up a fake transaction so the output will be hidden
                    // and the next line's output substituted instead
                    line = line.Substring(8);
                    curTrans = new DisambigHelper(this, line);
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
                        chatTargetRegister = target;
                        chatMsgRegister = msg;
                        line = id + ":$" + type;
                    }
                }
            }
        }

        private class DisambigHelper : Transaction
        {
            /// <summary>
            /// Constructs a new DisambigHelper.
            /// </summary>
            /// <param name="realm">The realm where disambiguation is occurring.</param>
            /// <param name="line">The command that will need disambiguation, starting with
            /// the player ID and a colon.</param>
            public DisambigHelper(GameInstance instance, string line)
                : base("")
            {
                string[] parts = line.Split(':');
                int num;
                if (parts.Length >= 2 && int.TryParse(parts[0], out num))
                    lock (instance.players)
                        instance.players.TryGetValue(num, out this.Player);
            }

            public readonly Player Player;
        }

        private class RealmGreeting : Transaction
        {
            public RealmGreeting()
                : base("$hello")
            {
            }

            private static readonly char[] lineDelim = { '\r', '\n' };

            public override void OnFinished(Instance instance)
            {
                GameInstance gi = (GameInstance)instance;

                string[] lines = Response.ToString().Split(lineDelim, StringSplitOptions.RemoveEmptyEntries);
                foreach (string l in lines)
                {
                    if (l[0] == '$')
                    {
                        string line = l;
                        string word = GetToken(' ', ref line);
                        switch (word)
                        {
                            case "$register":
                                switch (GetToken(' ', ref line))
                                {
                                    case "action":
                                        gi.RegisterAction(line);
                                        break;
                                    case "kind":
                                        gi.RegisterKind(line);
                                        break;
                                    case "prop":
                                        gi.RegisterProperty(line);
                                        break;
                                }
                                break;
                        }
                    }
                }
            }
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
        /// Constructs a line of input by combining a player ID with the player's
        /// command, and adds it to the queue.
        /// </summary>
        /// <param name="player">The player sending the command, who must have
        /// already been added with <see cref="AddPlayer"/>.</param>
        /// <param name="command">The player's command.</param>
        /// <param name="silent"><b>true</b> if the command's output should be
        /// suppressed.</param>
        public void QueueInput(Player player, string command, bool silent)
        {
            if (rawMode)
            {
                QueueInput(command);
            }
            else
            {
                QueueInput(
                    string.Format("{0}{1}:{2}",
                        silent ? "$silent " : "",
                        playerIDs[player],
                        command));
            }
        }

        /// <summary>
        /// Constructs a special input command by combining a bot's player ID,
        /// an action number, and action arguments, processes it, and returns
        /// the game's output.
        /// </summary>
        /// <param name="bot">The player attempting the action.</param>
        /// <param name="actname">The name of the action (including argument
        /// types).</param>
        /// <param name="arg1">The first action argument, or "$" if the first
        /// argument is text.</param>
        /// <param name="arg2">The second action argument, or "$" if the second
        /// argument is text.</param>
        /// <param name="text">The text of the first or second action argument,
        /// or <b>null</b> if neither is text.</param>
        /// <returns>The output from running the action, or <b>null</b> if the
        /// action is not supported.</returns>
        public string RunBotAction(BotPlayer bot, string actname, string arg1, string arg2, string text)
        {
            if (rawMode)
                throw new InvalidOperationException("Bot actions not supported in raw mode");

            int id;
            if (playerIDs.TryGetValue(bot, out id) == false)
                throw new ArgumentException("Bot is not connected to the instance", "bot");

            ActionInfo? info = actionMap.GetInfo(actname);
            if (info == null)
                return null;

            return SendAndGet(
                string.Format("$action {0} {1} {2} {3}{4}{5}",
                    id,
                    info.Value.Number,
                    arg1,
                    arg2,
                    text == null ? "" : " ",
                    text ?? ""));
        }

        /// <summary>
        /// Adds a player to the instance.
        /// </summary>
        /// <param name="player">The player who is joining.</param>
        /// <param name="position">A string describing the player's location,
        /// as returned by <see cref="ExportPlayerPositions"/>, or
        /// <b>null</b>.</param>
        public void AddPlayer(Player player, string position)
        {
            int id = GetNewPlayerID();

            lock (players)
            {
                players[id] = player;
                playerIDs[player] = id;
            }

            if (!rawMode)
                QueueInput(string.Format("${0} {1}={2}{3}",
                    (player is BotPlayer) ? "botjoin" : "join",
                    player.Name,
                    id,
                    position == null ? "" : "," + position));
        }

        private int GetNewPlayerID()
        {
            if (nextPlayerID > MAX_PLAYER_ID)
                nextPlayerID = MIN_PLAYER_ID;

            int result = nextPlayerID++;

            lock (players)
            {
                bool wrapped = false;
                while (players.ContainsKey(result))
                {
                    result++;
                    if (result > MAX_PLAYER_ID)
                    {
                        if (wrapped)
                            throw new InvalidOperationException("Too many players");

                        result = MIN_PLAYER_ID;
                        wrapped = true;
                    }
                }

                // reserve the ID
                players.Add(result, null);
            }

            return result;
        }

        /// <summary>
        /// Removes a player from the instance.
        /// </summary>
        /// <param name="player">The player who is leaving.</param>
        public void RemovePlayer(Player player)
        {
            int id;
            if (!playerIDs.TryGetValue(player, out id))
                throw new ArgumentException("Player not present", "player");

            if (!rawMode)
            {
                string result = SendAndGet(string.Format("$part {0}", id));
                OnHandleOutput(result);
                FlushAll();
            }

            lock (players)
            {
                players.Remove(id);
                playerIDs.Remove(player);
            }
        }

        /// <summary>
        /// Gets a list of all players who are in the instance.
        /// </summary>
        /// <returns>An array of players.</returns>
        public Player[] ListPlayers()
        {
            Player[] result;
            lock (players)
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
        /// <returns>The number of players exported.</returns>
        /// <remarks>If an exception occurs while retrieving any player's
        /// location string, that player will be added to the dictionary
        /// with a <b>null</b> value.</remarks>
        public int ExportPlayerPositions(IDictionary<Player, string> results)
        {
            if (results == null)
                throw new ArgumentNullException("results");
            if (results.IsReadOnly)
                throw new ArgumentException("Dictionary is read only", "results");

            Player[] temp;
            lock (players)
            {
                temp = new Player[players.Count];
                players.Values.CopyTo(temp, 0);
                players.Clear();
                playerIDs.Clear();
                nextPlayerID = MIN_PLAYER_ID;
            }

            foreach (Player p in temp)
            {
                lock (p)
                    p.Instance = null;

                string locationStr;
                try
                {
                    locationStr = SendAndGet("$locate " + playerIDs[p].ToString());
                }
                catch
                {
                    results.Add(p, null);
                    continue;
                }

                results.Add(p, locationStr);
            }

            return temp.Length;
        }

        private void FlushAll()
        {
            lock (players)
                foreach (Player p in players.Values)
                    p.FlushOutput();
        }

        protected override void OnHandleOutput(string text)
        {
            foreach (char c in text)
                HandleOutput(c);
        }

        private void HandleOutput(char c)
        {
            if (rawMode)
            {
                SendCurPlayer(c);
                return;
            }

            switch (tagstate)
            {
                case 0:
                    // outside any tag
                    if (c == '<')
                        tagstate++;
                    else
                        SendCurPlayer(c);
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
                        SendCurPlayer('<');
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
                        SendCurPlayer("<$");
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
                        SendCurPlayer("<$t");
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
                        SendCurPlayer("<$t ");
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
                        lock (players)
                            players.TryGetValue(playerNum, out curPlayer);
                        if (curPlayer != null)
                            curPlayer.WriteLine();
                        tagParam = null;
                        tagstate = 0;
                    }
                    else
                    {
                        SendCurPlayer("<$t ");
                        SendCurPlayer(tagParam.ToString());
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
                        SendCurPlayer("<$a");
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
                        SendCurPlayer("<$b");
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
                        SendCurPlayer("<$d");
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
                        SendCurPlayer("</");
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
                        SendCurPlayer("</$");
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
                        SendCurPlayer("</$t");
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
                        SendCurPlayer("</$a");
                        tagstate = 0;
                    }
                    break;
            }
        }

        private void SendCurPlayer(char c)
        {
            if (curPlayer == Announcer)
            {
                lock (players)
                {
                    if (c == '\n')
                    {
                        foreach (Player p in players.Values)
                            p.WriteLine();
                    }
                    else
                    {
                        foreach (Player p in players.Values)
                            p.Write(c);
                    }
                }
            }
            else if (curPlayer != null)
            {
                if (c == '\n')
                    curPlayer.WriteLine();
                else
                    curPlayer.Write(c);
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

        private void SendCurPlayer(string str)
        {
            str = str.Replace("\n", "\r\n");

            if (curPlayer == Announcer)
            {
                lock (players)
                    foreach (Player p in players.Values)
                        p.Write(str);
            }
            else if (curPlayer != null)
            {
                curPlayer.Write(str);
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
                server.LogMessage(LogLevel.Warning,
                    "Illegal transfer (curPlayer is {0}) attempted from {1} to {2}.",
                    curPlayer == Announcer ? "Announcer" : "null",
                    this.Name, spec);
            }
            else
            {
                server.TransferPlayer(curPlayer, spec);
            }
        }

        private void DisambiguateCurPlayer(string info)
        {
            if (curPlayer == Announcer || curPlayer == null)
            {
                server.LogMessage(LogLevel.Warning,
                    "Illegal (curPlayer is {0}) disambiguation mode request.",
                    curPlayer == Announcer ? "Announcer" : "null");
            }
            else
            {
                lock (curPlayer)
                    curPlayer.Disambiguating = info;
            }
        }

        #region Server Registers (for game instances)

        // for player queries
        private Player queriedPlayer;       // pq_id

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

        protected override bool TryGetWordRegister(string name, out int value)
        {
            switch (name)
            {
                case "pq_id":
                case "ls_playerid":
                    // ID for player queries
                    if (queriedPlayer == null || !playerIDs.ContainsKey(queriedPlayer))
                        value = 0;
                    else
                        value = playerIDs[queriedPlayer];
                    return true;
            }

            return base.TryGetWordRegister(name, out value);
        }

        protected override bool TryPutWordRegister(string name, int value)
        {
            switch (name)
            {
                case "pq_id":
                case "ls_playerid":
                    // select player to query
                    lock (players)
                        players.TryGetValue(value, out queriedPlayer);
                    break;
            }

            return base.TryPutWordRegister(name, value);
        }

        protected override string GetTextRegister(string name)
        {
            switch (name)
            {
                case "pq_attrval":
                    // value of selected player attribute
                    if (queriedPlayer != null && queriedAttr != null)
                    {
                        switch (queriedAttr)
                        {
                            case "accesslevel":
                                return this.Realm.GetAccessLevel(queriedPlayer).ToString();

                            case "idletime":
                                NetworkPlayer np = queriedPlayer as NetworkPlayer;
                                if (np != null)
                                    lock (np)
                                    {
                                        if (np.Connection != null)
                                            return Server.FormatTimeSpan(np.Connection.IdleTime);
                                    }
                                return "";
                        }

                        lock (queriedPlayer)
                            return queriedPlayer.GetAttribute(queriedAttr);
                    }
                    return null;

                case "ls_playerval":
                    // value of selected player storage register
                    if (queriedPlayer != null && queriedAttr != null)
                        return this.Realm.GetPlayerStorage(queriedPlayer, queriedAttr);
                    else
                        return "";
            }

            return base.GetTextRegister(name);
        }

        protected override void PutTextRegister(string name, string value)
        {
            switch (name)
            {
                case "pq_attrval":
                    // change value of selected player attribute
                    if (queriedPlayer != null && queriedAttr != null &&
                        AllowSetPlayerAttribute(queriedAttr, value))
                    {
                        lock (queriedPlayer)
                            queriedPlayer.SetAttribute(queriedAttr, value);
                    }
                    break;

                case "ls_playerval":
                    // change value of selected player storage register
                    if (queriedPlayer != null && queriedAttr != null)
                        this.Realm.SetPlayerStorage(queriedPlayer, queriedAttr, value);
                    break;
            }

            base.PutTextRegister(name, value);
        }

        #endregion
    }

    class BotInstance : Instance
    {
        private readonly Dictionary<int, BotPlayer> bots = new Dictionary<int, BotPlayer>();
        private readonly Dictionary<BotPlayer, int> botIDs = new Dictionary<BotPlayer, int>();

        public BotInstance(Server server, Realm realm, Stream zfile, string name)
            : base(server, realm, zfile, name)
        {
        }

        protected override void OnVMStarting()
        {
            // nada
        }

        protected override void OnVMFinished(bool wasTerminated)
        {
            foreach (BotPlayer bot in bots.Values)
                server.DisconnectBot(bot);

            bots.Clear();
            botIDs.Clear();
        }

        protected override void OnHandleOutput(string text)
        {
            while (text.Length > 0)
            {
                // split off a line
                string line = GetToken('\n', ref text);
                if (line.EndsWith("\r"))
                    line = line.Substring(0, line.Length - 1);

                // split off the first word
                string word = GetToken(' ', ref line);
                switch (word)
                {
                    case "$register":
                        switch (GetToken(' ', ref line))
                        {
                            case "action":
                                RegisterAction(line);
                                break;
                            case "kind":
                                RegisterKind(line);
                                break;
                            case "prop":
                                RegisterProperty(line);
                                break;
                        }
                        break;
                    case "$addbot":
                        AddBot(line);
                        break;
                    case "$delbot":
                        RemoveBot(line);
                        break;
                    case "$connect":
                        ConnectBot(line);
                        break;
                    case "$action":
                        SendBotAction(line);
                        break;
                }
            }
        }

        private void SendBotAction(string line)
        {
            // id num arg1 arg2 [text]
            string word, arg1, arg2;
            int id, actnum;

            word = GetToken(' ', ref line);
            if (!int.TryParse(word, out id))
                return;

            BotPlayer bot;
            if (!bots.TryGetValue(id, out bot))
                return;

            lock (bot)
            {
                if (bot.Instance == null)
                    return;

                word = GetToken(' ', ref line);
                if (!int.TryParse(word, out actnum))
                    return;

                string actName = actionMap.GetName(actnum);
                if (actName == null)
                    return;

                ActionInfo? info = actionMap.GetInfo(actName);
                if (info == null)
                    return;

                arg1 = GetToken(' ', ref line);
                if (!ValidateActionArg(arg1, info.Value.ArgType1))
                    return;

                arg2 = GetToken(' ', ref line);
                if (!ValidateActionArg(arg2, info.Value.ArgType2))
                    return;

                if (info.Value.ArgType1 != ArgType.Text && info.Value.ArgType2 != ArgType.Text)
                    line = null;

                bot.Instance.RunBotAction(bot, actName, arg1, arg2, line);
            }
        }

        private bool ValidateActionArg(string arg1, ArgType argType)
        {
            throw new NotImplementedException();    //XXX
        }

        private void ConnectBot(string line)
        {
            // id spec
            string word = GetToken(' ', ref line);
            int id;

            if (int.TryParse(word, out id) && bots.ContainsKey(id))
            {
                BotPlayer player = bots[id];
                server.TransferPlayer(player, line);
            }
        }

        private void RemoveBot(string line)
        {
            // id
            int id;
            if (int.TryParse(line, out id) && bots.ContainsKey(id))
            {
                BotPlayer player = bots[id];
                bots.Remove(id);
                botIDs.Remove(player);
                server.DisconnectBot(player);
            }
        }

        private void AddBot(string line)
        {
            // id name
            string word = GetToken(' ', ref line);
            int id;

            if (int.TryParse(word, out id) && ValidBotName(line) &&
                !bots.ContainsKey(id))
            {
                BotPlayer player = new BotPlayer(this, line);
                bots.Add(id, player);
                botIDs.Add(player, id);
            }
        }

        private static bool ValidBotName(string name)
        {
            return (name.Length <= 32) && (name.Trim() == name);
        }

        internal void ReceiveLine(BotPlayer bot, string line)
        {
            // this is called from the remote instance's thread
            int id;
            if (botIDs.TryGetValue(bot, out id))
            {
                throw new NotImplementedException();    //XXX
            }
        }
    }
}
