using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Guncho.Connections;

namespace Guncho.Services
{
    /// <summary>
    /// Service for managing player connections.
    /// </summary>
    public interface IConnectionService
    {
        IEnumerable<Connection> GetOpenConnections();
        Task<bool> WithPlayerConnectionsAsync(Player player, Func<Connection, Task> action);
        Task SendTextFileAsync(Connection connection, string filePath);
    }
}
