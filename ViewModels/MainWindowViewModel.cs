using NLog;
using PatronGamingMonitor.Models;
using PatronGamingMonitor.Supports;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace PatronGamingMonitor.ViewModels
{
    public class MainWindowViewModel : BaseViewModel, IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly ApiClient _apiClient;
        private readonly SignalRService _signalRService;
        private readonly CacheService _cacheService;
        private DispatcherTimer _countdownTimer;
        private DispatcherTimer _clockTimer;
        private DispatcherTimer _autoRefreshTimer;
        private DispatcherTimer _networkCheckTimer;
        private readonly object _timerLock = new object();
        private readonly int _cacheExpirySeconds;
        private CancellationTokenSource _loadCancellation;
        private DispatcherTimer _filterDebounceTimer;
        private bool _isPendingFilter = false;
        private List<LevyTicket> _filteredAndSortedCache = new List<LevyTicket>();
        private bool _isUpdatingSort = false;
        private int _reconnectAttempt = 0;
        private int MAX_RECONNECT_ATTEMPTS = ConfigurationManager.AppSettings["MaxReconnectAttempts"] != null
                ? int.Parse(ConfigurationManager.AppSettings["MaxReconnectAttempts"]) : 5;
        private int RECONNECT_DELAY_MS = ConfigurationManager.AppSettings["ReconnectDelayMilliseconds"] != null
                ? int.Parse(ConfigurationManager.AppSettings["ReconnectDelayMilliseconds"]) : 10000;

        #region Properties

        private string _currentTime;
        private string _currentDate;
        private string _currentVersion;
        public string CurrentTime
        {
            get => _currentTime;
            set { _currentTime = value; OnPropertyChanged(); }
        }

        public string CurrentVersion
        {
            get => _currentVersion;
            set { _currentVersion = value; OnPropertyChanged(); }
        }

        public string CurrentDate
        {
            get => DateTime.Now.ToString("dddd, dd MMMM yyyy");
            set { _currentDate = value; OnPropertyChanged(); }
        }

        private string _connectionStatus = "Disconnected";
        public string ConnectionStatus
        {
            get => _connectionStatus;
            set { _connectionStatus = value; OnPropertyChanged(); }
        }

        private bool _isNetworkDisconnected = false;
        public bool IsNetworkDisconnected
        {
            get => _isNetworkDisconnected;
            set { _isNetworkDisconnected = value; OnPropertyChanged(); }
        }

        private bool _isReconnecting = false;
        public bool IsReconnecting
        {
            get => _isReconnecting;
            set { _isReconnecting = value; OnPropertyChanged(); }
        }

        private string _networkStatusMessage = "Reconnecting...";
        public string NetworkStatusMessage
        {
            get => _networkStatusMessage;
            set { _networkStatusMessage = value; OnPropertyChanged(); }
        }

        public ObservableCollection<LevyTicket> AllTicketsSource { get; set; } = new ObservableCollection<LevyTicket>();

        private ICollectionView _ticketsView;
        public ICollectionView TicketsView
        {
            get => _ticketsView;
            set { _ticketsView = value; OnPropertyChanged(); }
        }

        public int PageSize { get; set; } = 15;

        private int _pageIndex = 1;
        public int PageIndex
        {
            get => _pageIndex;
            set
            {
                if (_pageIndex != value)
                {
                    _pageIndex = value;
                    OnPropertyChanged();
                    Logger.Debug("PageIndex changed to {PageIndex}", _pageIndex);
                }
            }
        }

        private int _totalTable;
        public int TotalTable
        {
            get => _totalTable;
            set { _totalTable = value; OnPropertyChanged(); }
        }

        private int _totalSlot;
        public int TotalSlot
        {
            get => _totalSlot;
            set { _totalSlot = value; OnPropertyChanged(); }
        }

        private int _totalPages;
        public int TotalPages
        {
            get => _totalPages;
            set { _totalPages = value; OnPropertyChanged(); }
        }

        public string RowCountDisplay => $"{TotalCount}";

        private int _totalCount;
        public int TotalCount
        {
            get => _totalCount;
            set
            {
                _totalCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RowCountDisplay));
            }
        }

        private bool _filter12Hours = false;
        private bool _filter24Hours = false;
        private bool _filter48Hours = false;

        public bool Filter12Hours
        {
            get => _filter12Hours;
            set
            {
                if (_filter12Hours != value)
                {
                    _filter12Hours = value;
                    OnPropertyChanged();
                    Logger.Info("Filter12Hours changed → {Value}", value);
                    ApplyClientSideFilterDebounced();
                }
            }
        }

        public bool Filter24Hours
        {
            get => _filter24Hours;
            set
            {
                if (_filter24Hours != value)
                {
                    _filter24Hours = value;
                    OnPropertyChanged();
                    Logger.Info("Filter24Hours changed → {Value}", value);
                    ApplyClientSideFilterDebounced();
                }
            }
        }

        public bool Filter48Hours
        {
            get => _filter48Hours;
            set
            {
                if (_filter48Hours != value)
                {
                    _filter48Hours = value;
                    OnPropertyChanged();
                    Logger.Info("Filter48Hours changed → {Value}", value);
                    ApplyClientSideFilterDebounced();
                }
            }
        }

        public bool IsFilter12HoursChecked
        {
            get => Filter12Hours;
            set => Filter12Hours = value;
        }

        public bool IsFilter24HoursChecked
        {
            get => Filter24Hours;
            set => Filter24Hours = value;
        }

        public bool IsFilter48HoursChecked
        {
            get => Filter48Hours;
            set => Filter48Hours = value;
        }

        private string _filterType = "<30";
        public string FilterType
        {
            get => _filterType;
            set
            {
                if (_filterType != value)
                {
                    _filterType = value;
                    OnPropertyChanged();
                    Logger.Info("FilterType changed to {FilterType}", _filterType);
                }
            }
        }

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged();
                    PageIndex = 1;
                    Logger.Info("Search text changed: {SearchText}", _searchText);
                    ApplyClientSideFilterDebounced();
                }
            }
        }

        private bool? _statusFilterInUse = null;
        public bool? StatusFilterInUse
        {
            get => _statusFilterInUse;
            set
            {
                if (_statusFilterInUse != value)
                {
                    _statusFilterInUse = value;
                    OnPropertyChanged();
                    Logger.Info("StatusFilter changed → {Status}",
                        value == true ? "InUse" : value == false ? "Used" : "All");
                    ApplyClientSideFilterDebounced();
                }
            }
        }

        private bool? _statusFilterPlay = null;
        public bool? StatusFilterPlaying
        {
            get => _statusFilterPlay;
            set
            {
                if (_statusFilterPlay != value)
                {
                    _statusFilterPlay = value;
                    OnPropertyChanged();
                    Logger.Info("PlayingFilter changed → {Status}",
                        value == true ? "Playing" : value == false ? "Not Playing" : "All");
                    ApplyClientSideFilterDebounced();
                }
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        private string _currentSortColumn;
        private ListSortDirection _currentSortDirection = ListSortDirection.Ascending;

        #endregion

        #region Commands

        public ICommand LoadTicketsCommand { get; private set; }
        public ICommand NextPageCommand { get; private set; }
        public ICommand PrevPageCommand { get; private set; }
        public ICommand RefreshCommand { get; private set; }
        public ICommand ApplyFilterCommand { get; private set; }
        public ICommand ClearSearchCommand { get; private set; }
        public ICommand RetryConnectionCommand { get; private set; }

        #endregion

        #region Constructor

        public MainWindowViewModel()
        {
            try
            {
                if (IsInDesignMode())
                {
                    CurrentTime = DateTime.Now.ToString("HH:mm:ss");
                    PageSize = 15;
                    return;
                }

                CurrentVersion = "Version: Loading...";

                InitializeCommands();
                InitializeFilterDebounce();

                TicketsView = CollectionViewSource.GetDefaultView(AllTicketsSource);

                _apiClient = new ApiClient();
                _signalRService = new SignalRService();
                _cacheService = CacheService.Instance;

                if (!int.TryParse(ConfigurationManager.AppSettings["CacheExpirySeconds"], out _cacheExpirySeconds))
                    _cacheExpirySeconds = 30;

                _ = LoadVersionAsync();
                InitializeClockTimer();
                InitializeAutoRefreshTimer();
                InitializeNetworkMonitoring();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "❌ Failed to initialize MainWindowViewModel");
                if (!IsInDesignMode())
                {
                    MessageBox.Show($"Initialization error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion

        #region Initialization Methods

        private static bool IsInDesignMode()
        {
            return (bool)DesignerProperties.IsInDesignModeProperty
                .GetMetadata(typeof(DependencyObject))
                .DefaultValue;
        }

        private void InitializeClockTimer()
        {
            try
            {
                _clockTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };

                _clockTimer.Tick += (s, e) =>
                {
                    CurrentTime = DateTime.Now.ToString("HH:mm:ss");
                };

                _clockTimer.Start();
                CurrentTime = DateTime.Now.ToString("HH:mm:ss");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "❌ Failed to initialize clock timer");
            }
        }

        private void InitializeFilterDebounce()
        {
            _filterDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };

            _filterDebounceTimer.Tick += (s, e) =>
            {
                _filterDebounceTimer.Stop();
                if (_isPendingFilter)
                {
                    _isPendingFilter = false;
                    ApplyClientSideFilter();
                }
            };
        }

        private void InitializeAutoRefreshTimer()
        {
            try
            {
                _autoRefreshTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(_cacheExpirySeconds)
                };

                _autoRefreshTimer.Tick += async (s, e) =>
                {
                    var lastUpdate = _cacheService.GetLastCacheUpdateTime();
                    var elapsed = (DateTime.Now - lastUpdate).TotalSeconds;

                    if (elapsed >= _cacheExpirySeconds)
                    {
                        await LoadTicketsFromApiAsync();
                    }
                };

                _autoRefreshTimer.Start();
                Logger.Info("Auto-refresh timer started ({Interval}s interval)", _cacheExpirySeconds);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "❌ Failed to initialize auto-refresh timer");
            }
        }

        private void InitializeNetworkMonitoring()
        {
            try
            {
                NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;

                _networkCheckTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(5)
                };

                _networkCheckTimer.Tick += async (s, e) => await CheckNetworkStatusAsync();
                _networkCheckTimer.Start();

                Logger.Info("🌐 Network monitoring initialized");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "❌ Failed to initialize network monitoring");
            }
        }

        private async void OnNetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                if (e.IsAvailable)
                {
                    Logger.Info("✅ Network connection restored");
                    await OnNetworkRestored();
                }
                else
                {
                    Logger.Warn("⚠️ Network connection lost");
                    OnNetworkLost();
                }
            });
        }

        private async Task CheckNetworkStatusAsync()
        {
            try
            {
                bool isConnected = NetworkInterface.GetIsNetworkAvailable();

                if (!isConnected && !IsNetworkDisconnected)
                {
                    OnNetworkLost();
                }
                else if (isConnected && IsNetworkDisconnected)
                {
                    await OnNetworkRestored();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "❌ Error checking network status");
            }
        }

        private void OnNetworkLost()
        {
            IsNetworkDisconnected = true;
            IsReconnecting = false;
            _reconnectAttempt = 0;
            NetworkStatusMessage = "Network Disconnected - Connection Lost";
            Logger.Warn("⚠️ Network disconnected - showing notification");
        }

        private async Task OnNetworkRestored()
        {
            if (!IsNetworkDisconnected)
                return;

            IsReconnecting = true;
            _reconnectAttempt++;

            NetworkStatusMessage = $"Reconnecting... (Attempt {_reconnectAttempt}/{MAX_RECONNECT_ATTEMPTS})";
            Logger.Info($"🔄 Attempting to reconnect ({_reconnectAttempt}/{MAX_RECONNECT_ATTEMPTS})");

            try
            {
                await LoadTicketsFromApiAsync();

                IsNetworkDisconnected = false;
                IsReconnecting = false;
                _reconnectAttempt = 0;
                Logger.Info("✅ Successfully reconnected");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "❌ Reconnection failed");

                if (_reconnectAttempt >= MAX_RECONNECT_ATTEMPTS)
                {
                    IsReconnecting = false;
                    NetworkStatusMessage = "Reconnection failed - Please check your network";
                    Logger.Error("❌ Max reconnection attempts reached");
                }
                else
                {
                    await Task.Delay(RECONNECT_DELAY_MS);
                    await OnNetworkRestored();
                }
            }
        }

        private async void InitializeSignalR()
        {
            try
            {
                await _signalRService.InitializeAsync();

                _signalRService.OnTicketUpdated += ticket =>
                {
                    Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        _cacheService.UpdateTicket(ticket);
                        ApplyClientSideFilter();
                        Logger.Info("✨ Ticket updated via SignalR: {TransactionNo}", ticket.TransactionNo);
                    });
                };

                _signalRService.OnTicketAdded += ticket =>
                {
                    Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        _cacheService.AddTicket(ticket);
                        ApplyClientSideFilter();
                        Logger.Info("✨ New ticket added via SignalR: {TransactionNo}", ticket.TransactionNo);
                    });
                };

                _signalRService.OnTicketRemoved += transactionNo =>
                {
                    Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        _cacheService.RemoveTicket(transactionNo);
                        ApplyClientSideFilter();
                        Logger.Info("✨ Ticket removed via SignalR: {TransactionNo}", transactionNo);
                    });
                };

                _signalRService.OnConnectionStateChanged += state =>
                {
                    Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        ConnectionStatus = state;
                        Logger.Info("📡 SignalR connection state: {State}", state);
                    });
                };

                ConnectionStatus = "Connected";
                Logger.Info("SignalR initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "❌ Failed to initialize SignalR");
                ConnectionStatus = "Failed";
            }
        }

        private void ApplyClientSideFilterDebounced()
        {
            _isPendingFilter = true;
            _filterDebounceTimer.Stop();
            _filterDebounceTimer.Start();
        }

        private void InitializeCommands()
        {
            LoadTicketsCommand = new RelayCommand<Window>(
                p => true,
                async p =>
                {
                    Logger.Info("Loading initial data...");
                    IsLoading = true;
                    FilterType = "All";
                    await LoadTicketsFromApiAsync();
                    IsLoading = false;
                    Logger.Info("Initial data loaded successfully");
                });

            NextPageCommand = new RelayCommand<object>(
                _ => PageIndex < TotalPages && !IsLoading,
                _ =>
                {
                    PageIndex++;
                    UpdatePagedView();
                });

            PrevPageCommand = new RelayCommand<object>(
                _ => PageIndex > 1 && !IsLoading,
                _ =>
                {
                    PageIndex--;
                    UpdatePagedView();
                });

            RefreshCommand = new RelayCommand<object>(
                p => !IsLoading,
                async p =>
                {
                    Logger.Info("Manual refresh triggered");
                    IsLoading = true;
                    await LoadTicketsFromApiAsync();
                    IsLoading = false;
                });

            ApplyFilterCommand = new RelayCommand<string>(
                p => !IsLoading,
                filter =>
                {
                    FilterType = filter;
                    PageIndex = 1;
                    Logger.Info("Applying filter → {FilterType}", FilterType);
                    ApplyClientSideFilterDebounced();
                });

            ClearSearchCommand = new RelayCommand<object>(
                p => !string.IsNullOrEmpty(SearchText),
                p =>
                {
                    SearchText = string.Empty;
                    Logger.Info("🧹 Search cleared");
                });

            RetryConnectionCommand = new RelayCommand<object>(
                p => IsNetworkDisconnected && !IsReconnecting,
                async p =>
                {
                    Logger.Info("🔄 Manual retry connection triggered");
                    _reconnectAttempt = 0;
                    await OnNetworkRestored();
                });
        }

        #endregion

        #region Data Loading Methods

        private async Task LoadVersionAsync()
        {
            try
            {
                Logger.Info("Loading ClientLauncher version from API...");

                var versionResponse = await _apiClient.GetApplicationByCodeAsync("PatronGamingMonitor");

                if (versionResponse != null && !string.IsNullOrEmpty(versionResponse.Version))
                {
                    CurrentVersion = $"Version: {versionResponse.Version}";
                    Logger.Info("Loaded version: {Version}", versionResponse.Version);
                }
                else
                {
                    string configVersion = ConfigurationManager.AppSettings["ApplicationVersion"] ?? "1.1.0";
                    CurrentVersion = $"Version: {configVersion}";
                    Logger.Warn("API returned null/empty version, using config: {Version}", configVersion);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load version from API");
                string configVersion = ConfigurationManager.AppSettings["ApplicationVersion"] ?? "1.1.0";
                CurrentVersion = $"Version: {configVersion}";
                Logger.Warn("Using fallback version: {Version}", configVersion);
            }
        }

        private async Task LoadTicketsFromApiAsync()
        {
            try
            {
                _loadCancellation?.Cancel();
                _loadCancellation = new CancellationTokenSource();

                StopCountdownTimer();

                var result = await _apiClient.GetLevyTicketsPagedAsync(
                    1, 50000, "All", _loadCancellation.Token);

                if (result == null || result.Items == null || !result.Items.Any())
                {
                    Logger.Warn("⚠️ No tickets returned from API");
                    // Keep the data from cache to show on the UI
                    return;
                    _cacheService.ClearCache();
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        AllTicketsSource.Clear();
                    });
                    TotalCount = 0;
                    TotalPages = 0;
                }

                _cacheService.SetCache(result.Items.ToList());
                Logger.Info("Cached {Count} tickets", result.Items.Count);

                ApplyClientSideFilter();
                StartCountdownTimer();
            }
            catch (OperationCanceledException)
            {
                Logger.Info("⚠️ Load operation was cancelled");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "❌ Error loading tickets from API");

                if (!NetworkInterface.GetIsNetworkAvailable())
                {
                    OnNetworkLost();
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Error loading tickets:\n\n{ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        private async void ApplyClientSideFilter()
        {
            try
            {
                await Task.Run(() =>
                {
                    var allTickets = _cacheService.GetAllTickets();

                    if (allTickets == null || !allTickets.Any())
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            Logger.Debug("No tickets in cache to filter");
                            _filteredAndSortedCache.Clear();
                            TotalCount = 0;
                            TotalPages = 0;
                            UpdatePagedView();
                        });
                        return;
                    }

                    var filteredList = allTickets.AsEnumerable();

                    if (!string.IsNullOrWhiteSpace(SearchText))
                    {
                        var searchLower = SearchText.Trim().ToLowerInvariant();
                        filteredList = filteredList.Where(t =>
                            (t.PlayerID.ToString().Contains(searchLower)) ||
                            (t.FullName?.ToLowerInvariant().Contains(searchLower) == true) ||
                            (t.Location?.ToLowerInvariant().Contains(searchLower) == true) ||
                            (t.Type?.ToLowerInvariant().Contains(searchLower) == true) ||
                            (t.Area?.ToLowerInvariant().Contains(searchLower) == true) ||
                            (t.LocalStatus?.ToLowerInvariant().Contains(searchLower) == true) ||
                            (t.PitName?.ToLowerInvariant().Contains(searchLower) == true)
                        );
                    }

                    if (FilterType == "<30")
                    {
                        filteredList = filteredList.Where(t => t.PlayingTime > 43200);
                    }

                    if (Filter12Hours || Filter24Hours || Filter48Hours)
                    {
                        filteredList = filteredList.Where(t =>
                        {
                            bool match = false;
                            if (Filter12Hours && t.PlayingTime >= 43200 && t.PlayingTime < 86400)
                                match = true;
                            if (Filter24Hours && t.PlayingTime >= 86400 && t.PlayingTime < 172800)
                                match = true;
                            if (Filter48Hours && t.PlayingTime >= 172800)
                                match = true;
                            return match;
                        });
                    }

                    var filtered = filteredList.ToList();
                    TotalSlot = filtered.Count(t => t.Type.Equals("Slot", StringComparison.OrdinalIgnoreCase));
                    TotalTable = filtered.Count(t => t.Type.Equals("Table", StringComparison.OrdinalIgnoreCase));

                    if (!string.IsNullOrEmpty(_currentSortColumn))
                    {
                        if (_currentSortDirection == ListSortDirection.Ascending)
                        {
                            filtered = filtered.OrderBy(t => GetPropertyValue(t, _currentSortColumn)).ToList();
                        }
                        else
                        {
                            filtered = filtered.OrderByDescending(t => GetPropertyValue(t, _currentSortColumn)).ToList();
                        }
                    }

                    _filteredAndSortedCache = filtered;

                    var totalCount = filtered.Count;
                    var totalPages = totalCount > 0 ? (int)Math.Ceiling((double)totalCount / PageSize) : 0;

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        TotalCount = totalCount;
                        TotalPages = totalPages;

                        if (PageIndex > totalPages && totalPages > 0)
                        {
                            PageIndex = totalPages;
                        }
                        else if (PageIndex < 1 && totalPages > 0)
                        {
                            PageIndex = 1;
                        }

                        UpdatePagedView();
                    });
                });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "❌ Error applying client-side filter");
            }
        }

        private void UpdatePagedView()
        {
            try
            {
                if (_filteredAndSortedCache == null || !_filteredAndSortedCache.Any())
                {
                    Logger.Debug("No cached filtered data to page");
                    AllTicketsSource.Clear();
                    return;
                }

                var pagedItems = _filteredAndSortedCache
                    .Skip((PageIndex - 1) * PageSize)
                    .Take(PageSize)
                    .ToList();

                AllTicketsSource.Clear();
                foreach (var ticket in pagedItems)
                {
                    AllTicketsSource.Add(ticket);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "❌ Error updating paged view");
            }
        }

        private object GetPropertyValue(object obj, string propertyName)
        {
            try
            {
                var prop = obj.GetType().GetProperty(propertyName);
                var value = prop?.GetValue(obj);

                if (value == null)
                {
                    if (prop?.PropertyType == typeof(string))
                        return string.Empty;
                    if (prop?.PropertyType == typeof(int) || prop?.PropertyType == typeof(int?))
                        return 0;
                    if (prop?.PropertyType == typeof(DateTime?) || prop?.PropertyType == typeof(DateTime))
                        return DateTime.MinValue;
                }

                return value;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "❌ Error getting property value for {PropertyName}", propertyName);
                return null;
            }
        }

        #endregion

        #region Timer Methods

        private void StartCountdownTimer()
        {
            lock (_timerLock)
            {
                if (_countdownTimer != null)
                {
                    _countdownTimer.Stop();
                    _countdownTimer.Tick -= CountdownTick;
                    _countdownTimer = null;
                }

                _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _countdownTimer.Tick += CountdownTick;
                _countdownTimer.Start();

                Logger.Info("⏱ Countdown timer started");
            }
        }

        private void StopCountdownTimer()
        {
            lock (_timerLock)
            {
                if (_countdownTimer != null)
                {
                    _countdownTimer.Stop();
                    _countdownTimer.Tick -= CountdownTick;
                    _countdownTimer = null;
                    Logger.Debug("⏱ Countdown timer stopped");
                }
            }
        }

        private void CountdownTick(object sender, EventArgs e)
        {
            var allTickets = _cacheService.GetAllTickets();
            if (allTickets == null || !allTickets.Any())
                return;

            foreach (var ticket in allTickets)
            {
                ticket.PlayingTime++;

                if (ticket.PlayingTime <= 0 &&
                    !ticket.UsedStatus.Equals("Overstayed", StringComparison.OrdinalIgnoreCase))
                {
                    ticket.UsedStatus = "Overstayed";
                }
            }

            if (AllTicketsSource.Any())
            {
                var visibleTickets = AllTicketsSource.ToList();
                var hasChanges = visibleTickets.Any(t => t.PlayingTime <= 0);

                if (hasChanges)
                {
                    Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        TicketsView?.Refresh();
                    });
                }
            }
        }

        #endregion

        #region Public Methods for Sorting

        public void SortData(string columnName, ListSortDirection direction)
        {
            if (string.IsNullOrEmpty(columnName)) return;

            try
            {
                _currentSortColumn = columnName;
                _currentSortDirection = direction;

                Logger.Info("User clicked sort: {Column} {Direction}", columnName, direction);

                ApplyClientSideFilter();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "❌ Error in SortData");
            }
        }

        #endregion

        #region Dispose

        public void Dispose()
        {
            try
            {
                Logger.Info("🧹 Disposing MainWindowViewModel...");

                NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;

                _loadCancellation?.Cancel();
                _loadCancellation?.Dispose();

                _countdownTimer?.Stop();
                _clockTimer?.Stop();
                _autoRefreshTimer?.Stop();
                _filterDebounceTimer?.Stop();
                _networkCheckTimer?.Stop();
                _signalRService?.Dispose();
                _apiClient?.Dispose();

                Logger.Info("MainWindowViewModel disposed successfully");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "❌ Error during disposal");
            }
        }

        #endregion
    }
}