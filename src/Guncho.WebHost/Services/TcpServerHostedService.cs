using Guncho.Connections;
using Guncho.Services;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Guncho.WebHost.Services
{
    /// <summary>
    /// Background service that runs the TCP server for telnet-style connections.
    /// </summary>
    public class TcpServerHostedService : IHostedService
    {
        private readonly IServerConfiguration _config;
        private readonly Microsoft.Extensions.Logging.ILogger<TcpServerHostedService> _logger;
        private readonly GunchoServerServices _gunchoServices;
        private TcpConnectionManager? _tcpManager;
        private Task? _tcpListenTask;
        private CancellationTokenSource? _cancellationTokenSource;

        public TcpServerHostedService(
            IServerConfiguration config,
            Microsoft.Extensions.Logging.ILogger<TcpServerHostedService> logger,
            GunchoServerServices gunchoServices)
        {
            _config = config;
            _logger = logger;
            _gunchoServices = gunchoServices;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("TCP Server: Starting on port {0}...", _config.GameServerPort);

            _cancellationTokenSource = new CancellationTokenSource();
            _tcpManager = new TcpConnectionManager(IPAddress.Any, _config.GameServerPort);

            // Wire up connection events
            _tcpManager.ConnectionAccepted += OnTcpConnectionAccepted;
            _tcpManager.ConnectionClosed += OnTcpConnectionClosed;

            // Start the TCP listener in the background
            _tcpListenTask = _tcpManager.RunAsync(_cancellationTokenSource.Token);

            _logger.LogInformation("TCP Server: Listening on port {0}.", _config.GameServerPort);

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("TCP Server: Stopping...");

            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                
                if (_tcpListenTask != null)
                {
                    try
                    {
                        await _tcpListenTask;
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when canceling
                    }
                }

                _cancellationTokenSource.Dispose();
            }

            _logger.LogInformation("TCP Server: Stopped.");
        }

        private void OnTcpConnectionAccepted(object? sender, ConnectionAcceptedEventArgs<TcpConnection> e)
        {
            _logger.LogDebug("TCP: Accepting connection from {0}.", FormatEndPoint(e.Connection.OtherSide));
            
            // Register connection with GunchoServerServices
            _gunchoServices.RegisterConnection(e.Connection, e.AuthenticatedUserName);
        }

        private void OnTcpConnectionClosed(object? sender, ConnectionEventArgs<TcpConnection> e)
        {
            _logger.LogInformation("TCP: Connection closed from {0}.", FormatEndPoint(e.Connection.OtherSide));
            
            // Unregister connection from GunchoServerServices
            _gunchoServices.UnregisterConnection(e.Connection);
        }

        private static string FormatEndPoint(System.Net.EndPoint? endPoint)
        {
            if (endPoint == null)
                return "(unknown)";
            
            return endPoint.ToString() ?? "(unknown)";
        }
    }
}
