using Guncho.Connections;
using Guncho.Services;
using System.Text;

namespace Guncho
{
    /// <summary>
    /// Processes system commands from players (commands starting with special prefixes or system commands like "who", "quit").
    /// </summary>
    public class CommandProcessor
    {
        private readonly IServerConfiguration config;
        private readonly IPlayerService playerService;
        private readonly IConnectionService connectionService;
        private readonly IInstanceService instanceService;
        private readonly IRealmService realmService;

        public CommandProcessor(
            IServerConfiguration config,
            IPlayerService playerService,
            IConnectionService connectionService,
            IInstanceService instanceService,
            IRealmService realmService)
        {
            this.config = config;
            this.playerService = playerService;
            this.connectionService = connectionService;
            this.instanceService = instanceService;
            this.realmService = realmService;
        }

        public struct HandleCommandResult
        {
            public bool Handled;
            public string Line;
        }

        /// <summary>
        /// Checks whether a command should be intercepted rather than passed into
        /// the realm, and handles the command if so.
        /// </summary>
        public async Task<HandleCommandResult> HandleSystemCommandAsync(Connection conn, string line)
        {
            string trimmed = line.Trim();
            string command = GetToken(ref trimmed, ' ').ToLower();

            var result = new HandleCommandResult
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
                    if (player != null)
                        await ShowWhoListAsync(conn, player);
                    return result;

                case "quit":
                    await conn.WriteLineAsync("Goodbye.");
                    // Connection will be closed by the connection manager
                    return result;

                case "help":
                    await conn.WriteLineAsync("Commands: WHO, QUIT, HELP");
                    return result;

                default:
                    // Not a system command, pass through to the realm
                    result.Handled = false;
                    return result;
            }
        }

        private async Task ShowWhoListAsync(Connection conn, Player requestingPlayer)
        {
            var connections = connectionService.GetOpenConnections().ToList();
            
            await conn.WriteLineAsync($"Players online: {connections.Count}");
            
            foreach (var c in connections)
            {
                if (c.Player != null)
                {
                    var connectedTime = c.ConnectedTime;
                    var idleTime = c.IdleTime;
                    await conn.WriteLineAsync($"  {c.Player.Name} - connected {TextUtils.FormatTimeSpan(connectedTime)}, idle {TextUtils.FormatTimeSpan(idleTime)}");
                }
            }
        }

        private static string GetToken(ref string str, char delimiter)
        {
            str = str.TrimStart();
            int pos = str.IndexOf(delimiter);
            string result;
            if (pos < 0)
            {
                result = str;
                str = "";
            }
            else
            {
                result = str.Substring(0, pos);
                str = str.Substring(pos + 1);
            }
            return result;
        }
    }
}
