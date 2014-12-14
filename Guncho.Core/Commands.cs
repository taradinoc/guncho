using Guncho.Connections;

namespace Guncho
{
    partial class Server
    {
        /// <summary>
        /// Checks whether a command should be intercepted rather than passed into
        /// the realm, and handles the command if so.
        /// </summary>
        /// <param name="conn">The connection that issued the command.</param>
        /// <param name="line">The command. This may be modified if HandleSystemCommand
        /// returns <b>false</b>.</param>
        /// <returns><b>true</b> if the command was intercepted, or <b>false</b> if it
        /// should still be passed into the realm.</returns>
        private bool HandleSystemCommand(Connection conn, ref string line)
        {
            string trimmed = line.Trim();
            string command = GetToken(ref trimmed, ' ').ToLower();

            // ignore blank lines
            if (command.Length == 0)
                return true;

            Player player;
            lock (conn)
                player = conn.Player;

            // check commands that can be used any time
            switch (command)
            {
                case "who":
                    ShowWhoList(conn, player);
                    return true;

                case "quit":
                    conn.WriteLine("Goodbye.");
                    conn.Terminate(wait: false);
                    return true;
            }

            if (player == null || player.Realm == null)
            {
                // check out-of-realm commands
                switch (command)
                {
                    case "connect":
                    case "connec":
                    case "conne":
                    case "conn":
                    case "con":
                    case "co":
                        string name = GetToken(ref trimmed, ' ');
                        if (name.Length == 0)
                        {
                            conn.WriteLine("Usage: connect <name> <password>");
                        }
                        else if (name.ToLower() == "guest")
                        {
                            LogInAsGuest(conn);
                        }
                        else
                        {
                            string password = GetToken(ref trimmed, ' ');
                            player = ValidateLogIn(name, password);

                            if (player == null || player.IsGuest)
                            {
                                conn.WriteLine("Incorrect login.");
                            }
                            else
                            {
                                LogInAsPlayer(conn, player);
                            }
                        }
                        return true;
                }
            }
            else
            {
                // check in-realm commands
                switch (command)
                {
                    case "@shutdown":
                        CmdShutdown(conn, player, trimmed);
                        return true;

                    case "@wall":
                        CmdWall(conn, player, trimmed);
                        return true;

                    case "@teleport":
                    case "@tel":
                        CmdTeleport(conn, player, trimmed);
                        return true;

                    case "@invite":
                        CmdInvite(conn, player, trimmed);
                        return true;

                    case "page":
                    case "p":
                        CmdPage(conn, player, trimmed);
                        return true;

                    case "again":
                    case "g":
                        lock (player)
                        {
                            if (player.LastCommand == null)
                            {
                                conn.WriteLine("No previous command to repeat.");
                                return true;
                            }

                            line = player.LastCommand;
                            return false;
                        }
                }

                // save command for 'again'
                lock (player)
                    player.LastCommand = line;
            }

            return false;
        }

        private void LogInAsPlayer(Connection conn, Player player)
        {
            lock (conn)
                conn.Player = player;

            Connection oldConn;
            lock (player)
                oldConn = player.Connection;

            if (oldConn != null)
            {
                oldConn.WriteLine("*** Connection superseded ***");
                conn.WriteLine("*** Connection resumed ***");
                oldConn.Terminate(wait: true);
            }

            lock (player)
                player.Connection = conn;

            SendTextFile(conn, player.Name, Properties.Settings.Default.MotdFileName);
            EnterInstance(player, GetDefaultInstance(GetRealm(Properties.Settings.Default.StartRealmName)));
        }

        private void LogInAsGuest(Connection conn)
        {
            Player guest;

            lock (players)
            {
                int guestNum = 0;
                string guestName, key;
                do
                {
                    guestNum++;
                    guestName = "Guest" + guestNum.ToString();
                    key = guestName.ToLower();
                } while (players.ContainsKey(key));

                guest = new Player(-guestNum, guestName, false, true);
                guest.Connection = conn;
                players.Add(key, guest);
            }

            lock (conn)
                conn.Player = guest;

            SendTextFile(conn, guest.Name, Properties.Settings.Default.GuestMotdFileName);
            EnterInstance(guest, GetDefaultInstance(GetRealm(Properties.Settings.Default.StartRealmName)));
        }

        private void CmdShutdown(Connection conn, Player player, string args)
        {
            if (player.IsAdmin)
            {
                string msg = args.Trim();
                if (msg.Length == 0)
                    msg = "no reason specified";

                Shutdown(msg);
            }
            else
            {
                conn.WriteLine("Permission denied.");
            }
        }

        private void CmdWall(Connection conn, Player player, string args)
        {
            if (player.IsAdmin)
            {
                string msg = args.Trim();
                if (msg.Length == 0)
                {
                    conn.WriteLine("No message.");
                }
                else
                {
                    msg = "*** " + player.Name + " announces, \"" + msg + "\" ***";
                    foreach (Connection c in openConnections.Keys)
                    {
                        c.WriteLine(msg);
                        c.FlushOutput();
                    }
                }
            }
            else
            {
                conn.WriteLine("Permission denied.");
            }
        }

        private void CmdTeleport(Connection conn, Player player, string args)
        {
            Realm dest = GetRealm(args);

            if (dest == null)
            {
                conn.WriteLine("No such realm.");
                return;
            }

            lock (player)
                if (player.Realm == dest)
                {
                    conn.WriteLine("You're already in that realm.");
                    return;
                }

            if (dest.GetAccessLevel(player) < RealmAccessLevel.Invited &&
                args.ToLower() != Properties.Settings.Default.StartRealmName.ToLower())
            {
                conn.WriteLine("Permission denied.");
                return;
            }

            if (dest.IsCondemned)
            {
                conn.WriteLine("That realm has been condemned.");
                return;
            }

            Instance inst = GetDefaultInstance(dest);
            inst.Activate();

            string check = inst.SendAndGet("$knock default");
            switch (check)
            {
                case "ok":
                    EnterInstance(player, inst);
                    break;

                case "full":
                    conn.WriteLine("That realm is full.");
                    break;

                default:
                    conn.WriteLine("Teleporting failed mysteriously.");
                    break;
            }
        }

        private void CmdInvite(Connection conn, Player player, string args)
        {
            //XXX @invite
            conn.WriteLine("Not implemented.");
        }

        private void CmdPage(Connection conn, Player player, string args)
        {
            string target = GetToken(ref args, '=').Trim();
            string msg = args.Trim();

            if (target.Length == 0 || msg.Length == 0)
            {
                conn.WriteLine("Usage: page <player>=<message>");
                return;
            }

            Player targetPlayer = GetPlayerByName(target);
            if (targetPlayer == null)
            {
                conn.WriteLine("No such player.");
                return;
            }

            if (msg.StartsWith(":"))
            {
                msg = msg.Substring(1);
                lock (targetPlayer)
                {
                    if (targetPlayer.Connection != null)
                    {
                        targetPlayer.Connection.WriteLine(player.Name + " (paging you) " + msg);
                        targetPlayer.Connection.FlushOutput();
                        conn.WriteLine("You page-posed " + targetPlayer.Name + ": " +
                                       player.Name + " " + msg);
                    }
                    else
                    {
                        conn.WriteLine("That player is not connected.");
                    }
                }
            }
            else
            {
                lock (targetPlayer)
                {
                    if (targetPlayer.Connection != null)
                    {
                        targetPlayer.Connection.WriteLine(player.Name + " pages: " + msg);
                        targetPlayer.Connection.FlushOutput();
                        conn.WriteLine("You paged " + targetPlayer.Name + ": " + msg);
                    }
                    else
                    {
                        conn.WriteLine("That player is not connected.");
                    }
                }
            }
        }
    }
}
