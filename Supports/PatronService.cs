using Newtonsoft.Json;
using NLog;
using PatronGamingMonitor.Models;
using System;
using System.Configuration;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace PatronGamingMonitor.Supports
{
    public class PatronService : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly HttpClient _httpClient;
        private readonly string _patronBaseUrl;
        private readonly string _patronInforEndpoint;
        private readonly string _cacheDirectory;
        private bool _disposed = false;
        private readonly int _fileRetentionDays;

        public PatronService()
        {
            try
            {
                _httpClient = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(30)
                };

                _patronBaseUrl = ConfigurationManager.AppSettings["LevyBaseUrl"]
                    ?? throw new InvalidOperationException("LevyBaseUrl is missing in app.config.");

                _patronInforEndpoint = ConfigurationManager.AppSettings["PatronInforEndpoint"]
                    ?? throw new InvalidOperationException("PatronInforEndpoint is missing in app.config.");

                var apiKey = ConfigurationManager.AppSettings["ApiKey"];
                if (string.IsNullOrWhiteSpace(apiKey))
                    throw new InvalidOperationException("ApiKey is missing in app.config.");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKey);
                _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
                _fileRetentionDays = int.Parse(ConfigurationManager.AppSettings["FileRetentionDays"] ?? "1");
                // Setup cache directory
                var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                _cacheDirectory = Path.Combine(baseDirectory, "PatronCache");
                if (!Directory.Exists(_cacheDirectory))
                {
                    Directory.CreateDirectory(_cacheDirectory);
                    Logger.Info("Created PatronCache directory at: {Path}", _cacheDirectory);
                }

                // Clean old cache files on initialization
                CleanOldCacheFiles();

                Logger.Info("PatronService initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "❌ PatronService initialization failed.");
                throw;
            }
        }

        /// <summary>
        /// Clean cache files older than 1 day
        /// </summary>
        public void CleanOldCacheFiles()
        {
            try
            {
                if (!Directory.Exists(_cacheDirectory))
                    return;

                var now = DateTime.Now;
                var files = Directory.GetFiles(_cacheDirectory, "*.json");
                var deletedCount = 0;
                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        var age = now - fileInfo.LastWriteTime;

                        // Delete files older than the configured retention period
                        if (age.TotalDays > _fileRetentionDays)
                        {
                            File.Delete(file);
                            deletedCount++;
                            Logger.Info("🗑️ Deleted old cache file: {FileName} (Age: {Age:F1} days)",
                                Path.GetFileName(file), age.TotalDays);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "❌ Error deleting cache file: {File}", file);
                    }
                }

                if (deletedCount > 0)
                {
                    Logger.Info("✅ Cleaned {Count} old cache files from PatronCache", deletedCount);
                }
                else
                {
                    Logger.Info("ℹ️ No old cache files to clean");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "❌ Error cleaning old cache files");
            }
        }

        /// <summary>
        /// Get patron information by PatronID, check cache first
        /// </summary>
        public async Task<PatronInformation> GetPatronInformationAsync(int patronId)
        {
            try
            {
                // Check cache first
                var cachedPatron = LoadFromCache(patronId);
                if (cachedPatron != null)
                {
                    Logger.Info("✅ Loaded patron {PatronId} from cache", patronId);
                    return cachedPatron;
                }

                // Call API if not in cache
                Logger.Info("🔄 Fetching patron {PatronId} from API", patronId);
                var url = $"{_patronBaseUrl}{_patronInforEndpoint}/{patronId}";

                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    Logger.Warn("GetPatronInformationAsync failed ({Status}): {Error}", response.StatusCode, error);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var patron = JsonConvert.DeserializeObject<PatronInformation>(json);

                if (patron != null && patron.playerID > 0)
                {
                    // Save to cache
                    SaveToCache(patron);
                    Logger.Info("✅ Fetched and cached patron {PatronId}", patronId);
                    return patron;
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "❌ Error fetching patron information for PatronID={PatronId}", patronId);
                return null;
            }
        }

        /// <summary>
        /// Load patron from cache
        /// </summary>
        private PatronInformation LoadFromCache(int patronId)
        {
            try
            {
                var filePath = GetCacheFilePath(patronId);
                if (!File.Exists(filePath))
                    return null;

                // Check if cache is still valid (less than X days old)
                var fileInfo = new FileInfo(filePath);
                var age = DateTime.Now - fileInfo.LastWriteTime;

                if (age.TotalDays > _fileRetentionDays)
                {
                    Logger.Info("⚠️ Cache expired for patron {PatronId}, deleting...", patronId);
                    File.Delete(filePath);
                    return null;
                }

                var json = File.ReadAllText(filePath);
                return JsonConvert.DeserializeObject<PatronInformation>(json);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "❌ Error loading patron {PatronId} from cache", patronId);
                return null;
            }
        }

        /// <summary>
        /// Save patron to cache
        /// </summary>
        private void SaveToCache(PatronInformation patron)
        {
            try
            {
                if (patron == null || patron.playerID <= 0)
                    return;

                var filePath = GetCacheFilePath(patron.playerID);
                var json = JsonConvert.SerializeObject(patron, Formatting.Indented);
                File.WriteAllText(filePath, json);

                Logger.Info("💾 Saved patron {PatronId} to cache", patron.playerID);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "❌ Error saving patron {PatronId} to cache", patron?.playerID);
            }
        }

        /// <summary>
        /// Get cache file path for patron
        /// </summary>
        private string GetCacheFilePath(int patronId)
        {
            return Path.Combine(_cacheDirectory, $"{patronId}.json");
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
                    _httpClient?.Dispose();
                }
                _disposed = true;
            }
        }
    }
}