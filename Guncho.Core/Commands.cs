using Guncho.Connections;
using System;
using System.Threading.Tasks;

namespace Guncho
{
    partial class Server
    {
        private struct HandleSystemCommandResult
        {
            public bool Handled;
            public string Line;
        }

        /// <summary>
        /// Checks whether a command should be intercepted rather than passed into
        /// the realm, and handles the command if so.
        /// </summary>
        /// <param name="conn">The connection that issued the command.</param>
        /// <param name="line">The command.</param>
        /// <returns>A <see cref="HandleSystemCommandResult"/> whose <see cref="HandleSystemCommandResult.Handled"/>
        /// is <b>true</b> if the command was intercepted, or <b>false</b> if it should still be passed into the
        /// realm, and whose <see cref="HandleSystemCommandResult.Line"/> is the modified line.</returns>
        private async Task<HandleSystemCommandResult> HandleSystemCommand(Connection conn, string line)
        {
            string trimmed = line.Trim();
            string command = GetToken(ref trimmed, ' ').ToLower();

            var result = new HandleSystemCommandResult
            {
                Handled = true,
                Line = line,
            };

            // ignore blank lines
            if (command.Length == 0)
                return result;

            Player player;
            using (await conn.Lock.ReaderLockAsync())
                player = conn.Player;

            // check commands that can be used any time
            switch (command)
            {
                case "who":
                    await ShowWhoList(conn, player);
                    return result;

                case "quit":
                    await conn.WriteLineAsync("Goodbye.");
                    await conn.TerminateAsync();
                    return result;
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
                            await conn.WriteLineAsync("Usage: connect <name> <password>");
                        }
                        else if (name.ToLower() == "guest")
                        {
                            await LogInAsGuest(conn);
                        }
                        else
                        {
                            string password = GetToken(ref trimmed, ' ');
                            player = ValidateLogIn(name, password);

                            if (player == null || player.IsGuest)
                            {
                                await conn.WriteLineAsync("Incorrect login.");
                            }
                            else
                            {
                                await LogInAsPlayer(conn, player);
                            }
                        }
                        return result;
                }
            }
            else
            {
                // check in-realm commands
                switch (command)
                {
                    case "@shutdown":
                        await CmdShutdown(conn, player, trimmed);
                        return result;

                    case "@wall":
                        await CmdWall(conn, player, trimmed);
                        return result;

                    case "@teleport":
                    case "@tel":
                        await CmdTeleport(conn, player, trimmed);
                        return result;

                    case "@invite":
                        await CmdInvite(conn, player, trimmed);
                        return result;

                    case "page":
                    case "p":
                        await CmdPage(conn, player, trimmed);
                        return result;

                    case "again":
                    case "g":
                        using (await player.Lock.ReaderLockAsync())
                        {
                            if (player.LastCommand == null)
                            {
                                await conn.WriteLineAsync("No previous command to repeat.");
                                return result;
                            }

                            result.Handled = false;
                            result.Line = player.LastCommand;
                            return result;
                        }
                }

                // save command for 'again'
                using (await player.Lock.WriterLockAsync())
                    player.LastCommand = line;
            }

            result.Handled = false;
            return result;
        }

        private async Task LogInAsPlayer(Connection conn, Player player)
        {
            using (await conn.Lock.WriterLockAsync())
                conn.Player = player;

            Connection oldConn;
            using (await player.Lock.ReaderLockAsync())
                oldConn = player.Connection;

            if (oldConn != null)
            {
                Func<Task> notifyOldConn = async () =>
                {
                    await oldConn.WriteLineAsync("*** Connection superseded ***");
                    await oldConn.TerminateAsync();
                };
                Func<Task> notifyNewConn = async () =>
                {
                    await conn.WriteLineAsync("*** Connection resumed ***");
                    await conn.FlushOutputAsync();
                };
                await Task.WhenAll(notifyOldConn(), notifyNewConn());
            }

            using (await player.Lock.WriterLockAsync())
                player.Connection = conn;

            await SendTextFile(conn, player.Name, Properties.Settings.Default.MotdFileName);
            await EnterInstance(player, await GetDefaultInstance(GetRealm(Properties.Settings.Default.StartRealmName)));
        }

        private async Task LogInAsGuest(Connection conn)
        {
            Player guest;

            int guestNum = 0;
            string guestName, key;
            do
            {
                guestNum++;
                guestName = "Guest" + guestNum.ToString();
                key = guestName.ToLower();
            } while (!players.TryAdd(key, null));

            guest = new Player(-guestNum, guestName, false, true);
            guest.Connection = conn;
            players[key] = guest;
            playersById[-guestNum] = guest;

            using (await conn.Lock.WriterLockAsync())
                conn.Player = guest;

            await SendTextFile(conn, guest.Name, Properties.Settings.Default.GuestMotdFileName);
            await EnterInstance(guest, await GetDefaultInstance(GetRealm(Properties.Settings.Default.StartRealmName)));
        }

        private async Task CmdShutdown(Connection conn, Player player, string args)
        {
            if (player.IsAdmin)
            {
                string msg = args.Trim();
                if (msg.Length == 0)
                    msg = "no reason specified";

                await ShutdownAsync(msg);
            }
            else
            {
                await conn.WriteLineAsync("Permission denied.");
            }
        }

        private async Task CmdWall(Connection conn, Player player, string args)
        {
            if (player.IsAdmin)
            {
                string msg = args.Trim();
                if (msg.Length == 0)
                {
                    await conn.WriteLineAsync("No message.");
                }
                else
                {
                    msg = "*** " + player.Name + " announces, \"" + msg + "\" ***";
                    foreach (Connection c in openConnections.Keys)
                    {
                        await c.WriteLineAsync(msg);
                        await c.FlushOutputAsync();
                    }
                }
            }
            else
            {
                await conn.WriteLineAsync("Permission denied.");
            }
        }

        private async Task CmdTeleport(Connection conn, Player player, string args)
        {
            Realm dest = GetRealm(args);

            if (dest == null)
            {
                await conn.WriteLineAsync("No such realm.");
                return;
            }

            using (await player.Lock.ReaderLockAsync())
            {
                if (player.Realm == dest)
                {
                    await conn.WriteLineAsync("You're already in that realm.");
                    return;
                }
            }

            if (dest.GetAccessLevel(player) < RealmAccessLevel.Invited &&
                args.ToLower() != Properties.Settings.Default.StartRealmName.ToLower())
            {
                await conn.WriteLineAsync("Permission denied.");
                return;
            }

            if (dest.IsCondemned)
            {
                await conn.WriteLineAsync("That realm has been condemned.");
                return;
            }

            IInstance inst = await GetDefaultInstance(dest);
            await inst.Activate();

            string check = await inst.SendAndGet("$knock default");
            switch (check)
            {
                case "ok":
                    await EnterInstance(player, inst);
                    break;

                case "full":
                    await conn.WriteLineAsync("That realm is full.");
                    break;

                default:
                    await conn.WriteLineAsync("Teleporting failed mysteriously.");
                    break;
            }
        }

        private async Task CmdInvite(Connection conn, Player player, string args)
        {
            //XXX @invite
            await conn.WriteLineAsync("Not implemented.");
        }

        private async Task CmdPage(Connection conn, Player player, string args)
        {
            string target = GetToken(ref args, '=').Trim();
            string msg = args.Trim();

            if (target.Length == 0 || msg.Length == 0)
            {
                await conn.WriteLineAsync("Usage: page <player>=<message>");
                return;
            }

            Player targetPlayer = GetPlayerByName(target);
            if (targetPlayer == null)
            {
                await conn.WriteLineAsync("No such player.");
                return;
            }

            if (msg.StartsWith(":"))
            {
                msg = msg.Substring(1);
                using (await targetPlayer.Lock.ReaderLockAsync())
                {
                    if (targetPlayer.Connection != null)
                    {
                        await targetPlayer.Connection.WriteLineAsync(player.Name + " (paging you) " + msg);
                        await targetPlayer.Connection.FlushOutputAsync();
                        await conn.WriteLineAsync("You page-posed " + targetPlayer.Name + ": " +
                                       player.Name + " " + msg);
                    }
                    else
                    {
                        await conn.WriteLineAsync("That player is not connected.");
                    }
                }
            }
            else
            {
                using (await targetPlayer.Lock.ReaderLockAsync())
                {
                    if (targetPlayer.Connection != null)
                    {
                        await targetPlayer.Connection.WriteLineAsync(player.Name + " pages: " + msg);
                        await targetPlayer.Connection.FlushOutputAsync();
                        await conn.WriteLineAsync("You paged " + targetPlayer.Name + ": " + msg);
                    }
                    else
                    {
                        await conn.WriteLineAsync("That player is not connected.");
                    }
                }
            }
        }
    }
}
