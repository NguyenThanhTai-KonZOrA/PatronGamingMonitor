using NLog;
using PatronGamingMonitor.Models;
using PatronGamingMonitor.Supports;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
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
        private readonly object _timerLock = new object();
        private readonly int _cacheExpirySeconds;
        private CancellationTokenSource _loadCancellation;
        private DispatcherTimer _filterDebounceTimer;
        private bool _isPendingFilter = false;
        private List<LevyTicket> _filteredAndSortedCache = new List<LevyTicket>();
        private bool _isUpdatingSort = false;

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

        // Cập nhật properties cho checkboxes
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

        // Save sort state
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

                CurrentVersion = $"Version: {ConfigurationManager.AppSettings["ApplicationVersion"]}";

                InitializeCommands();
                InitializeFilterDebounce();

                TicketsView = CollectionViewSource.GetDefaultView(AllTicketsSource);

                _apiClient = new ApiClient();
                _signalRService = new SignalRService();
                _cacheService = CacheService.Instance;

                if (!int.TryParse(ConfigurationManager.AppSettings["CacheExpirySeconds"], out _cacheExpirySeconds))
                    _cacheExpirySeconds = 30;

                //InitializeSignalR();
                InitializeClockTimer();
                InitializeAutoRefreshTimer();

                //Logger.Info("MainWindowViewModel initialized successfully");
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

                //Logger.Info("🕒 Clock timer initialized and started");
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
                        //Logger.Info("Auto-refresh triggered (elapsed: {Elapsed}s)", elapsed);
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
        }

        #endregion

        #region Data Loading Methods

        private async Task LoadTicketsFromApiAsync()
        {
            try
            {
                _loadCancellation?.Cancel();
                _loadCancellation = new CancellationTokenSource();

                StopCountdownTimer();

                //Logger.Info("🔄 Loading tickets from API/File...");

                var result = await _apiClient.GetLevyTicketsPagedAsync(
                    1, 50000, "All", _loadCancellation.Token);

                if (result == null || result.Items == null || !result.Items.Any())
                {
                    Logger.Warn("⚠️ No tickets returned from API");
                    _cacheService.ClearCache();
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        AllTicketsSource.Clear();
                    });
                    TotalCount = 0;
                    TotalPages = 0;
                    return;
                }

                _cacheService.SetCache(result.Items.ToList());
                Logger.Info("Cached {Count} tickets", result.Items.Count);

                ApplyClientSideFilter();

                StartCountdownTimer();

                //Logger.Info("Tickets loaded and cached successfully");
            }
            catch (OperationCanceledException)
            {
                Logger.Info("⚠️ Load operation was cancelled");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "❌ Error loading tickets from API");
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

                    // Free-text Search Filter
                    if (!string.IsNullOrWhiteSpace(SearchText))
                    {
                        var searchLower = SearchText.Trim().ToLowerInvariant();
                        filteredList = filteredList.Where(t =>
                            (t.PlayerID.ToString().Contains(searchLower)) ||
                            (t.FullName?.ToLowerInvariant().Contains(searchLower) == true) ||
                            (t.Location?.ToLowerInvariant().Contains(searchLower) == true) ||
                            (t.Type?.ToLowerInvariant().Contains(searchLower) == true) ||
                            (t.LevyType?.ToLowerInvariant().Contains(searchLower) == true) ||
                            (t.TransactionNo?.ToLowerInvariant().Contains(searchLower) == true) ||
                            (t.PitName?.ToLowerInvariant().Contains(searchLower) == true)
                        );
                    }

                    // Filter by FilterType (All Players hoặc Alerted Players)
                    if (FilterType == "<30") // Alerted Players
                    {
                        // Alerted Players: Playing > 43200 (12 hours)
                        filteredList = filteredList.Where(t => t.PlayingTime > 43200);
                    }
                    // Nếu FilterType == "All" thì không filter, lấy tất cả

                    // Filter by Playing Time checkboxes
                    if (Filter12Hours || Filter24Hours || Filter48Hours)
                    {
                        filteredList = filteredList.Where(t =>
                        {
                            bool match = false;

                            // 12H: 720-1439 phút (12-23.99 giờ)
                            if (Filter12Hours && t.PlayingTime >= 43200 && t.PlayingTime < 86400)
                                match = true;

                            // 24H: 1440-2879 phút (24-47.99 giờ)
                            if (Filter24Hours && t.PlayingTime >= 86400 && t.PlayingTime < 172800)
                                match = true;

                            // 48H: >= 2880 phút (>= 48 giờ)
                            if (Filter48Hours && t.PlayingTime >= 172800)
                                match = true;

                            return match;
                        });
                    }

                    var filtered = filteredList.ToList();

                    // APPLY SORTING
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

                    // Save filtered and sorted cache
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

                //Logger.Debug("📄 Page {Page}/{TotalPages} loaded with {Count} items",
                //    PageIndex, TotalPages, AllTicketsSource.Count);
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

                // Handle null values for proper sorting
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

        /// <summary>
        /// Public method called from code-behind when user clicks column header
        /// </summary>
        public void SortData(string columnName, ListSortDirection direction)
        {
            if (string.IsNullOrEmpty(columnName)) return;

            try
            {
                _currentSortColumn = columnName;
                _currentSortDirection = direction;

                Logger.Info("User clicked sort: {Column} {Direction}", columnName, direction);

                // Re-apply filter and sort on entire dataset
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

                _loadCancellation?.Cancel();
                _loadCancellation?.Dispose();

                _countdownTimer?.Stop();
                _clockTimer?.Stop();
                _autoRefreshTimer?.Stop();
                _filterDebounceTimer?.Stop();
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