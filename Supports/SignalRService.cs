using Microsoft.AspNet.SignalR.Client;
using NLog;
using PatronGamingMonitor.Models;
using System;
using System.Configuration;
using System.Threading.Tasks;

namespace PatronGamingMonitor.Supports
{
    public class SignalRService : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private HubConnection _hubConnection;
        private IHubProxy _levyHub;
        private bool _disposed = false;

        // Events for real-time updates
        public event Action<LevyTicket> OnTicketUpdated;
        public event Action<LevyTicket> OnTicketAdded;
        public event Action<string> OnTicketRemoved;
        public event Action<string> OnConnectionStateChanged;

        public bool IsConnected => _hubConnection?.State == Microsoft.AspNet.SignalR.Client.ConnectionState.Connected;

        public async Task InitializeAsync()
        {
            try
            {
                var signalRUrl = ConfigurationManager.AppSettings["SignalRHubUrl"]
                    ?? throw new InvalidOperationException("SignalRHubUrl is missing in app.config.");

                _hubConnection = new HubConnection(signalRUrl);

                // Add API key to query string or headers
                var apiKey = ConfigurationManager.AppSettings["ApiKey"];
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    _hubConnection.Headers.Add("X-API-Key", apiKey);
                }

                _levyHub = _hubConnection.CreateHubProxy("LevyTicketHub");

                // Register server-side event handlers
                RegisterEventHandlers();

                // Connection state changed
                _hubConnection.StateChanged += change =>
                {
                    Logger.Info("SignalR connection state changed: {OldState} → {NewState}",
                        change.OldState, change.NewState);
                    OnConnectionStateChanged?.Invoke(change.NewState.ToString());
                };

                // Reconnection logic
                _hubConnection.Reconnecting += () =>
                {
                    Logger.Warn("SignalR reconnecting...");
                };

                _hubConnection.Reconnected += () =>
                {
                    Logger.Info("SignalR reconnected successfully");
                };

                _hubConnection.Closed += () =>
                {
                    Logger.Warn("❌ SignalR connection closed. Attempting to reconnect...");
                    Task.Run(async () => await TryReconnectAsync());
                };

                await _hubConnection.Start();
                Logger.Info("SignalR connected to {Url}", signalRUrl);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "❌ Failed to initialize SignalR connection");
                throw;
            }
        }

        private void RegisterEventHandlers()
        {
            // Ticket updated
            _levyHub.On<LevyTicket>("TicketUpdated", ticket =>
            {
                Logger.Info("Received TicketUpdated: {TransactionNo}", ticket.TransactionNo);
                OnTicketUpdated?.Invoke(ticket);
            });

            // New ticket added
            _levyHub.On<LevyTicket>("TicketAdded", ticket =>
            {
                Logger.Info("Received TicketAdded: {TransactionNo}", ticket.TransactionNo);
                OnTicketAdded?.Invoke(ticket);
            });

            // Ticket removed/completed
            _levyHub.On<string>("TicketRemoved", transactionNo =>
            {
                Logger.Info("Received TicketRemoved: {TransactionNo}", transactionNo);
                OnTicketRemoved?.Invoke(transactionNo);
            });
        }

        private async Task TryReconnectAsync()
        {
            int retryCount = 0;
            const int maxRetries = 5;

            while (retryCount < maxRetries && _hubConnection.State != Microsoft.AspNet.SignalR.Client.ConnectionState.Connected)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)));
                    await _hubConnection.Start();
                    Logger.Info("Reconnected to SignalR (attempt {Retry})", retryCount + 1);
                    return;
                }
                catch (Exception ex)
                {
                    retryCount++;
                    Logger.Warn(ex, "Reconnection attempt {Retry} failed", retryCount);
                }
            }

            Logger.Error("❌ Failed to reconnect after {MaxRetries} attempts", maxRetries);
        }

        public async Task DisconnectAsync()
        {
            if (_hubConnection != null)
            {
                _hubConnection.Stop();
                Logger.Info("SignalR disconnected");
            }
            await Task.CompletedTask;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _hubConnection?.Dispose();
                }
                _disposed = true;
            }
        }
    }
}