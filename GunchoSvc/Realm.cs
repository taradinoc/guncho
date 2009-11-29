// send non-player game output to the console?
#define CONSOLE_SPAM

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Serialization;
using Textfyre.VM;
using System.ComponentModel;

namespace Guncho
{
    /// <summary>
    /// Describes the level of access that a player has to a realm.
    /// </summary>
    public enum RealmAccessLevel
    {
        /// <summary>
        /// The realm or player doesn't exist.
        /// </summary>
        Invalid,
        /// <summary>
        /// The player isn't allowed to enter the realm.
        /// </summary>
        Banned,
        /// <summary>
        /// The player isn't told of the realm's existence.
        /// </summary>
        Hidden,
        /// <summary>
        /// The player can see that the realm exists.
        /// </summary>
        Visible,
        /// <summary>
        /// The player can teleport into the realm.
        /// </summary>
        Invited,
        /// <summary>
        /// The player can view the realm's source and index.
        /// </summary>
        ViewSource,
        /// <summary>
        /// The player can make changes to the realm source.
        /// </summary>
        EditSource,
        /// <summary>
        /// The player can change the realm's metadata.
        /// </summary>
        EditSettings,
        /// <summary>
        /// The player can change other players' access to the realm.
        /// </summary>
        EditAccess,
        /// <summary>
        /// The player can make changes that are drastic or permanent,
        /// such as deleting the realm.
        /// </summary>
        SafetyOff,

        [Browsable(false)]
        OWNER = SafetyOff,
        [Browsable(false)]
        ADMIN = SafetyOff
    }

    /// <summary>
    /// Describes the default visibility and accessibility of a realm
    /// to players other than the owner.
    /// </summary>
    public enum RealmPrivacyLevel
    {
        /// <summary>
        /// Players aren't allowed to enter the realm.
        /// </summary>
        Private,
        /// <summary>
        /// Players aren't told of the realm's existence.
        /// </summary>
        Hidden,
        /// <summary>
        /// Players can see that the realm exists.
        /// </summary>
        Public,
        /// <summary>
        /// Players can jump to the realm with @teleport.
        /// </summary>
        Joinable,
        /// <summary>
        /// Players can view the realm's source code.
        /// </summary>
        Viewable,
    }

    struct RealmAccessListEntry
    {
        public readonly Player Player;
        public readonly RealmAccessLevel Level;

        public RealmAccessListEntry(Player player, RealmAccessLevel level)
        {
            this.Player = player;
            this.Level = level;
        }
    }

    class Realm : IDisposable
    {
        private readonly Server server;
        private readonly RealmFactory factory;
        private Engine vm;
        private readonly RealmIO io;
        private readonly Stream zfile;
        private readonly string name, sourceFile;
        private readonly Player owner;
        private RealmAccessListEntry[] accessList;

        private bool rawMode;
        private RealmPrivacyLevel privacy = RealmPrivacyLevel.Public;
        private bool needReset;
        private DateTime watchdogTime = DateTime.MaxValue;
        private object watchdogLock = new object();

        private bool condemned, restartRequested;
        private int failureCount;
        private DateTime activationTime;

        private Thread terpThread;
        private object terpThreadLock = new object();
        private Dictionary<int, Player> players = new Dictionary<int, Player>();

        private int tagstate = 0;
        private Player curPlayer = null;
        private Stack<Player> prevPlayers = new Stack<Player>();
        private StringBuilder tagParam;

        private const int MAX_LINE_LENGTH = 120;
        private const double WATCHDOG_SECONDS = 10.0;
        private static readonly Player Announcer = new Player(-1, "*Announcer*", false);

        public Realm(Server server, RealmFactory factory, Stream zfile, string name, string sourceFile,
            Player owner)
        {
            this.server = server;
            this.factory = factory;
            this.zfile = zfile;
            this.name = name;
            this.sourceFile = sourceFile;
            this.owner = owner;
            this.accessList = new RealmAccessListEntry[0];

            this.io = new RealmIO(this);
            this.vm = new Engine(zfile);
            vm.MaxHeapSize = Properties.Settings.Default.MaxHeapSize;
            vm.OutputReady += io.FyreOutputReady;
            vm.KeyWanted += io.FyreKeyWanted;
            vm.LineWanted += io.FyreLineWanted;

            /*vm.CodeCacheSize = Properties.Settings.Default.CodeCacheSize;
            vm.MaxUndoDepth = 0;*/

            LoadStorage();
        }

        public Realm(Realm other, string newName)
            : this(other.server, other.factory, other.zfile, newName, other.sourceFile, other.owner)
        {
            CopySettingsFrom(other);
        }

        public RealmFactory Factory
        {
            get { return factory; }
        }

        public bool RawMode
        {
            get { return rawMode; }
            set { rawMode = value; if (value) curPlayer = Announcer; }
        }

        public string Name
        {
            get { return name; }
        }

        public bool IsActive
        {
            get { return terpThread != null; }
        }

        public RealmPrivacyLevel PrivacyLevel
        {
            get { return privacy; }
            set { privacy = value; }
        }

        public RealmAccessListEntry[] AccessList
        {
            get { return (RealmAccessListEntry[])accessList.Clone(); }
            set { accessList = (RealmAccessListEntry[])value.Clone(); }
        }

        public Player Owner
        {
            get { return owner; }
        }

        public string SourceFile
        {
            get { return sourceFile; }
        }

        /// <summary>
        /// Gets a value indicating when the realm should be considered frozen
        /// (if the watchdog time hasn't been pushed forward by then).
        /// </summary>
        public DateTime WatchdogTime
        {
            get { lock (watchdogLock) return watchdogTime; }
        }

        public bool IsCondemned
        {
            get { return condemned; }
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
        /// Increments the realm's failure count, and condemns the realm if
        /// the configured threshold is reached.
        /// </summary>
        /// <returns><b>true</b> if the realm has exceeded the allowed number
        /// of failures and been condemned</returns>
        public bool IncrementFailureCount()
        {
            if (++failureCount > Properties.Settings.Default.RealmFailuresAllowed)
            {
                condemned = true;
                return true;
            }

            return false;
        }

        public void CopySettingsFrom(Realm other)
        {
            // copy property values (via the property setters)
            this.RawMode = other.RawMode;
            this.PrivacyLevel = other.PrivacyLevel;
            this.AccessList = other.accessList;

            // note: we don't copy IsCondemned, because editing a realm un-condemns it
        }

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
                    lock (players)
                    {
                        abandoned = new Player[players.Count];
                        players.Values.CopyTo(abandoned, 0);
                        players.Clear();
                    }

                    foreach (Player p in abandoned)
                        lock (p)
                            if (p.Connection != null)
                                p.Connection.FlushOutput();

                    server.RealmFinished(this, abandoned, wasTerminated);
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

        public void QueueInput(string line)
        {
            io.QueueInput(line);
        }

        /// <summary>
        /// Queues a line of input, waits for the realm to respond to it, and
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

        public void AddPlayer(Player player, string position)
        {
            lock (players)
                players.Add(player.ID, player);

            if (!rawMode)
                QueueInput(string.Format("$join {0}={1}{2}",
                    player.Name,
                    player.ID,
                    position == null ? "" : "," + position));
        }

        public void RemovePlayer(Player player)
        {
            if (!rawMode)
            {
                string result = SendAndGet(string.Format("$part {0}", player.ID));
                HandleOutput(result);
                FlushAll();
            }

            lock (players)
                players.Remove(player.ID);
        }

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
            }

            foreach (Player p in temp)
            {
                lock (p)
                    p.Realm = null;

                string locationStr;
                try
                {
                    locationStr = SendAndGet("$locate " + p.ID.ToString());
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
                    lock (p)
                        if (p.Connection != null)
                            p.Connection.FlushOutput();
        }

        private void HandleOutput(string text)
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
                            lock (curPlayer)
                                if (curPlayer.Connection != null)
                                    curPlayer.Connection.WriteLine();
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
                        tagstate=210;
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
                            lock (p)
                                if (p.Connection != null)
                                    p.Connection.Write("\r\n");
                    }
                    else
                    {
                        foreach (Player p in players.Values)
                            lock (p)
                                if (p.Connection != null)
                                    p.Connection.Write(c);
                    }
                }
            }
            else if (curPlayer != null)
            {
                lock (curPlayer)
                {
                    if (curPlayer.Connection != null)
                    {
                        if (c == '\n')
                            curPlayer.Connection.Write("\r\n");
                        else
                            curPlayer.Connection.Write(c);
                    }
                }
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
                        lock (p)
                            if (p.Connection != null)
                                p.Connection.Write(str);
            }
            else if (curPlayer != null)
            {
                lock (curPlayer)
                    if (curPlayer.Connection != null)
                        curPlayer.Connection.Write(str);
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
                    name, spec);
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

        public RealmAccessLevel GetAccessLevel(Player player)
        {
            if (player != null)
            {
                if (player.IsAdmin)
                    return RealmAccessLevel.ADMIN;

                if (player == this.Owner)
                    return RealmAccessLevel.OWNER;
            }

            for (int i = 0; i < accessList.Length; i++)
                if (accessList[i].Player == player)
                    return accessList[i].Level;

            switch (privacy)
            {
                case RealmPrivacyLevel.Private:
                    return RealmAccessLevel.Banned;

                case RealmPrivacyLevel.Hidden:
                    return RealmAccessLevel.Hidden;

                case RealmPrivacyLevel.Public:
                    return RealmAccessLevel.Visible;

                case RealmPrivacyLevel.Joinable:
                    return RealmAccessLevel.Invited;

                case RealmPrivacyLevel.Viewable:
                    return RealmAccessLevel.ViewSource;

                default:
                    // shouldn't happen
                    throw new Exception("BUG");
            }
        }

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
                    lock (players)
                        players.TryGetValue(value, out queriedPlayer);
                    break;

                case "rteinterval":
                    // change real-time event timer interval
                    timerInterval = value;
                    server.SetEventInterval(this, value);
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
                                return GetAccessLevel(queriedPlayer).ToString();

                            case "idletime":
                                lock (queriedPlayer)
                                {
                                    if (queriedPlayer.Connection != null)
                                        return Server.FormatTimeSpan(queriedPlayer.Connection.IdleTime);
                                }
                                return "";
                        }

                        lock (queriedPlayer)
                            return queriedPlayer.GetAttribute(queriedAttr);
                    }
                    return null;

                case "ls_realmval":
                    // value of selected realm storage register
                    if (queriedAttr != null)
                        return GetRealmStorage(queriedAttr);
                    else
                        return "";

                case "ls_playerval":
                    // value of selected player storage register
                    if (queriedPlayer != null && queriedAttr != null)
                        return GetPlayerStorage(queriedPlayer, queriedAttr);
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
                        lock (queriedPlayer)
                            queriedPlayer.SetAttribute(queriedAttr, value);
                    }
                    break;

                case "ls_realmval":
                    // change value of selected realm storage register
                    if (queriedAttr != null)
                        SetRealmStorage(queriedAttr, value);
                    break;

                case "ls_playerval":
                    // change value of selected player storage register
                    if (queriedPlayer != null && queriedAttr != null)
                        SetPlayerStorage(queriedPlayer, queriedAttr, value);
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

        #region Persistent Storage

        private readonly Dictionary<string, string> localStorage = new Dictionary<string, string>();

        private void LoadStorage()
        {
            string filename = Path.Combine(
                Properties.Settings.Default.RealmDataPath,
                this.name + ".storage.xml");

            localStorage.Clear();

            if (File.Exists(filename))
            {
                XML.realmStorage root;

                XmlSerializer ser = new XmlSerializer(typeof(XML.realmStorage));
                using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read))
                {
                    root = (XML.realmStorage)ser.Deserialize(fs);
                }

                if (root != null)
                {
                    if (root.realm.ToLower() != this.name.ToLower())
                        throw new Exception("Storage file doesn't match this realm");

                    if (root.item != null)
                        foreach (XML.storageItemType item in root.item)
                            localStorage.Add(item.key, item.Value);

                    if (root.player != null)
                        foreach (XML.realmStoragePlayer player in root.player)
                            if (player.item != null)
                                foreach (XML.storageItemType item in player.item)
                                    localStorage.Add(player.name + "\0" + item.key, item.Value);
                }
            }
        }

        private void SaveStorage()
        {
            XML.realmStorage root = new Guncho.XML.realmStorage();
            List<XML.storageItemType> items = new List<XML.storageItemType>();
            Dictionary<string, List<XML.storageItemType>> playerDict = new Dictionary<string, List<XML.storageItemType>>();

            char[] zero = { '\0' };
            foreach (KeyValuePair<string, string> pair in localStorage)
            {
                string[] parts = pair.Key.Split(zero, 2);
                if (parts.Length == 0)
                    continue; // shouldn't happen

                XML.storageItemType item = new XML.storageItemType();
                item.Value = pair.Value;

                if (parts.Length == 1)
                {
                    // realm storage
                    item.key = pair.Key;
                    items.Add(item);
                }
                else
                {
                    // player storage
                    List<XML.storageItemType> playerItems;
                    if (playerDict.TryGetValue(parts[0], out playerItems) == false)
                    {
                        playerItems = new List<XML.storageItemType>();
                        playerDict.Add(parts[0], playerItems);
                    }

                    item.key = parts[1];
                    playerItems.Add(item);
                }
            }

            root.realm = this.name;
            root.item = items.ToArray();

            List<XML.realmStoragePlayer> players = new List<XML.realmStoragePlayer>();
            foreach (KeyValuePair<string, List<XML.storageItemType>> pair in playerDict)
            {
                XML.realmStoragePlayer player = new Guncho.XML.realmStoragePlayer();
                player.name = pair.Key;
                player.item = pair.Value.ToArray();
                players.Add(player);
            }

            root.player = players.ToArray();

            string filename = Path.Combine(
                Properties.Settings.Default.RealmDataPath,
                this.name + ".storage.xml");

            XmlSerializer ser = new XmlSerializer(typeof(XML.realmStorage));
            using (FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write))
            {
                ser.Serialize(fs, root);
            }
        }

        private string GetRealmStorage(string key)
        {
            string value;
            if (localStorage.TryGetValue(key, out value))
                return value;
            else
                return "";
        }

        private void SetRealmStorage(string key, string value)
        {
            if (value == null || value.Length == 0)
                localStorage.Remove(key);
            else
                localStorage[key] = value;

            SaveStorage();
        }

        private string GetPlayerStorage(Player player, string key)
        {
            return GetRealmStorage(player.Name + "\0" + key);
        }

        private void SetPlayerStorage(Player player, string key, string value)
        {
            SetRealmStorage(player.Name + "\0" + key, value);
        }

        #endregion

        #region RealmIO

        private class RealmIO
        {
            private readonly Realm realm;
            private readonly Queue<string> inputQueue = new Queue<string>();
            private readonly Queue<Transaction> transQueue = new Queue<Transaction>();
            private readonly Queue<string> specialResponses = new Queue<string>();
            private readonly AutoResetEvent inputReady = new AutoResetEvent(false);
            private Transaction curTrans;

            public RealmIO(Realm realm)
            {
                this.realm = realm;
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

            private static Regex chatRegex = new Regex(@"^(-?\d+):\$(say|emote) (?:\>([^ ]*) )?(.*)$");

            private string GetInputLine()
            {
                if (specialResponses.Count > 0)
                    return specialResponses.Dequeue();

                realm.DisableWatchdog();

                try
                {
                    realm.prevPlayers.Clear();
                    realm.FlushAll();

                    if (curTrans is DisambigHelper)
                        realm.curPlayer = ((DisambigHelper)curTrans).Player;
                    else if (!realm.rawMode)
                        realm.curPlayer = null;

                    // is there a previous transaction?
                    if (curTrans != null)
                    {
                        // response has already been collected, so just unblock the waiting thread
                        lock (curTrans)
                            Monitor.Pulse(curTrans);

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
                                realm.server.LogMessage(LogLevel.Spam,
                                    "Transaction in {0}: {1}",
                                    realm.name, curTrans.Query);
                                return curTrans.Query;
                            }
                        }

                        // is there a line of player input waiting?
                        lock (inputQueue)
                        {
                            if (inputQueue.Count > 0)
                            {
                                string line = inputQueue.Dequeue();
                                realm.server.LogMessage(LogLevel.Spam,
                                    "Processing in {0}: {1}",
                                    realm.name, line);

                                if (!realm.rawMode)
                                {
                                    if (line.StartsWith("$silent "))
                                    {
                                        // set up a fake transaction so the output will be hidden
                                        // and the next line's output substituted instead
                                        line = line.Substring(8);
                                        curTrans = new DisambigHelper(realm, line);
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
                                            realm.chatTargetRegister = target;
                                            realm.chatMsgRegister = msg;
                                            line = id + ":$" + type;
                                        }
                                    }
                                }

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
                    realm.ResetWatchdog();
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
                        realm.HandleOutput(main);
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
                                if (realm.TryGetWordRegister(name, out word))
                                    specialResponses.Enqueue(word.ToString());
                                else
                                    specialResponses.Enqueue("-1");
                                break;

                            case "gettext":
                                str = realm.GetTextRegister(name) ?? "";
                                specialResponses.Enqueue(str.Length.ToString());
                                if (!int.TryParse(rest, out chunkSize))
                                    chunkSize = str.Length;
                                for (int offset = 0; offset < str.Length; offset += chunkSize)
                                    specialResponses.Enqueue(
                                        str.Substring(offset, Math.Min(chunkSize, str.Length - offset)));
                                break;

                            case "putword":
                                if (int.TryParse(rest, out word) && realm.TryPutWordRegister(name, word))
                                    specialResponses.Enqueue("1");
                                else
                                    specialResponses.Enqueue("0");
                                break;

                            case "puttext":
                                realm.PutTextRegister(name, rest);
                                specialResponses.Enqueue("?");
                                break;
                        }
                    }
                }
            }
        }

        private class Transaction
        {
            public Transaction(string query)
            {
                this.Query = query;
            }

            public readonly string Query;
            public readonly StringBuilder Response = new StringBuilder();
        }

        private class DisambigHelper : Transaction
        {
            /// <summary>
            /// Constructs a new DisambigHelper.
            /// </summary>
            /// <param name="realm">The realm where disambiguation is occurring.</param>
            /// <param name="line">The command that will need disambiguation, starting with
            /// the player ID and a colon.</param>
            public DisambigHelper(Realm realm, string line)
                : base("")
            {
                string[] parts = line.Split(':');
                int num;
                if (parts.Length >= 2 && int.TryParse(parts[0], out num))
                    lock (realm.players)
                        realm.players.TryGetValue(num, out this.Player);
            }

            public readonly Player Player;
        }

#endregion
    }
}
