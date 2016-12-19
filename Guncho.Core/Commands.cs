using Guncho.Connections;
using System;
using System.Linq;
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
        private async Task<HandleSystemCommandResult> HandleSystemCommandAsync(Connection conn, string line)
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

            var player = conn.Player;

            // check commands that can be used any time
            switch (command)
            {
                case "who":
                    await ShowWhoListAsync(conn, player);
                    return result;

                case "quit":
                    await conn.WriteLineAsync("Goodbye.");
                    await conn.TerminateAsync();
                    return result;
            }

            IInstance instance;
            if (player == null || playerInstances.TryGetValue(player, out instance) == false)
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
                            await LogInAsGuestAsync(conn);
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
                                await LogInAsPlayerAsync(conn, player);
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
                        await CmdShutdownAsync(conn, player, trimmed);
                        return result;

                    case "@wall":
                        await CmdWallAsync(conn, player, trimmed);
                        return result;

                    case "@teleport":
                    case "@tel":
                        await CmdTeleportAsync(conn, player, trimmed);
                        return result;

                    case "@invite":
                        await CmdInviteAsync(conn, player, trimmed);
                        return result;

                    case "page":
                    case "p":
                        await CmdPageAsync(conn, player, trimmed);
                        return result;

                    case "again":
                    case "g":
                        var lastCmd = player.LastCommand;
                        if (lastCmd == null)
                        {
                            await conn.WriteLineAsync("No previous command to repeat.");
                            return result;
                        }

                        result.Handled = false;
                        result.Line = player.LastCommand;
                        return result;
                }

                // save command for 'again'
                using (await player.Lock.WriterLockAsync())
                    player.LastCommand = line;
            }

            result.Handled = false;
            return result;
        }

        private async Task LogInAsPlayerAsync(Connection conn, Player player)
        {
            logger.LogMessage(LogLevel.Spam, "Setting conn.Player");

            var oldConns = openConnections.Keys.Where(c => c.Player == player).ToArray();

            using (await conn.Lock.WriterLockAsync())
                conn.Player = player;

            if (oldConns.Length > 0)
            {
                await Task.WhenAll(oldConns.Select(async c =>
                {
                    logger.LogMessage(LogLevel.Spam, "notifyOldConn: Starting");
                    await c.WriteLineAsync("*** Connection superseded ***");
                    await c.TerminateAsync();
                    logger.LogMessage(LogLevel.Spam, "notifyOldConn: Done");
                }));

                logger.LogMessage(LogLevel.Spam, "notifyNewConn: Starting");
                await conn.WriteLineAsync("*** Connection resumed ***");
                await conn.FlushOutputAsync();
                logger.LogMessage(LogLevel.Spam, "notifyNewConn: Done");
            }

            logger.LogMessage(LogLevel.Spam, "Sending MOTD");
            await SendTextFileAsync(conn, player.Name, Properties.Settings.Default.MotdFileName);
            logger.LogMessage(LogLevel.Spam, "Entering instance");
            await EnterInstanceAsync(player, await GetDefaultInstanceAsync(GetRealm(Properties.Settings.Default.StartRealmName)));
            logger.LogMessage(LogLevel.Spam, "Login complete");
        }

        private async Task LogInAsGuestAsync(Connection conn)
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
            players[key] = guest;
            playersById[-guestNum] = guest;

            using (await conn.Lock.WriterLockAsync())
                conn.Player = guest;

            await SendTextFileAsync(conn, guest.Name, Properties.Settings.Default.GuestMotdFileName);
            await EnterInstanceAsync(guest, await GetDefaultInstanceAsync(GetRealm(Properties.Settings.Default.StartRealmName)));
        }

        private async Task CmdShutdownAsync(Connection conn, Player player, string args)
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

        private async Task CmdWallAsync(Connection conn, Player player, string args)
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

        private async Task CmdTeleportAsync(Connection conn, Player player, string args)
        {
            var dest = GetInstance(args);

            if (dest == null)
            {
                await conn.WriteLineAsync("No such realm.");
                return;
            }

            IInstance inst;

            if (playerInstances.TryGetValue(player, out inst) && inst == dest)
            {
                await conn.WriteLineAsync("You're already in that realm.");
                return;
            }

            var realm = dest.Realm;

            if (realm.GetAccessLevel(player) < RealmAccessLevel.Invited &&
                args.ToLower() != Properties.Settings.Default.StartRealmName.ToLower())
            {
                await conn.WriteLineAsync("Permission denied.");
                return;
            }

            if (realm.IsCondemned)
            {
                await conn.WriteLineAsync("That realm has been condemned.");
                return;
            }

            await inst.ActivateAsync();

            string check = await inst.SendAndGetAsync("$knock default");

            switch (check)
            {
                case "ok":
                    await EnterInstanceAsync(player, inst);
                    break;

                case "full":
                    await conn.WriteLineAsync("That realm is full.");
                    break;

                default:
                    await conn.WriteLineAsync("Teleporting failed mysteriously.");
                    break;
            }
        }

        private async Task CmdInviteAsync(Connection conn, Player player, string args)
        {
            //XXX @invite
            await conn.WriteLineAsync("Not implemented.");
        }

        private async Task CmdPageAsync(Connection conn, Player player, string args)
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

            if (msg.StartsWith(":", StringComparison.Ordinal))
            {
                msg = msg.Substring(1);

                var sendPage = WithPlayerConnectionsAsync(targetPlayer, async c =>
                {
                    await c.WriteLineAsync(player.Name + " (paging you) " + msg);
                    await c.FlushOutputAsync();
                });

                if (await sendPage)
                {
                    await conn.WriteLineAsync("You page-posed " + targetPlayer.Name + ": " +
                                   player.Name + " " + msg);
                }
                else
                {
                    await conn.WriteLineAsync("That player is not connected.");
                }
            }
            else
            {
                var sendPage = WithPlayerConnectionsAsync(targetPlayer, async c =>
                {
                    await c.WriteLineAsync(player.Name + " pages: " + msg);
                    await c.FlushOutputAsync();
                });

                if (await sendPage)
                {
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
