using Newtonsoft.Json;
using NLog;
using PatronGamingMonitor.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PatronGamingMonitor.Supports
{
    public class ApiClient : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly HttpClient _httpClient;
        private readonly string _levyBaseUrl;
        private readonly string _patronBaseUrl;
        private readonly string _deploymentBaseUrl;
        private bool _disposed = false;

        public ApiClient()
        {
            try
            {
                _httpClient = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(30)
                };

                _deploymentBaseUrl = ConfigurationManager.AppSettings["DeploymentBaseUrl"]
                    ?? throw new InvalidOperationException("DeploymentBaseUrl is missing in app.config.");

                _levyBaseUrl = ConfigurationManager.AppSettings["LevyBaseUrl"]
                    ?? throw new InvalidOperationException("LevyBaseUrl is missing in app.config.");

                _patronBaseUrl = ConfigurationManager.AppSettings["LevyBaseUrl"]
                    ?? throw new InvalidOperationException("PatronBaseUrl is missing in app.config.");

                var apiKey = ConfigurationManager.AppSettings["ApiKey"];
                if (string.IsNullOrWhiteSpace(apiKey))
                    throw new InvalidOperationException("ApiKey is missing in app.config.");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKey);
                _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

                Logger.Info("ApiClient initialized successfully with BaseUrl={BaseUrl}", _levyBaseUrl);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "❌ ApiClient initialization failed.");
                throw;
            }
        }

        public async Task<ManifestApplicationResponse?> GetApplicationByCodeAsync(string appCode)
        {
            try
            {
                // Logger.Info("Fetching application {AppCode} from API", appCode);
                var app = await GetApiDataAsync<ManifestApplicationResponse>($"{_deploymentBaseUrl}/api/ApplicationManagement/manifest/latest/{appCode}");
                if (app != null)
                {
                    // Logger.Info("Successfully fetched manifest for application {AppCode}", appCode);
                }
                else
                {
                    // Logger.Warn("Application {AppCode} not found in API", appCode);
                }
                return app;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to fetch application {AppCode} from API", appCode);
                throw new Exception($"Failed to fetch application {appCode}: {ex.Message}", ex);
            }
        }

        public async Task<PatronInformation> GetPatronInformationAsync(int patronId, CancellationToken cancellationToken = default)
        {
            try
            {
                var endpoint = ConfigurationManager.AppSettings["PatronInforEndpoint"]
                               ?? "api/PatronProfile/patron-transaction/patron-profile";

                var url = $"{_patronBaseUrl}{endpoint}/{patronId}";
                Logger.Info("Fetching Patron Information from: {Url}", url);

                var response = await GetApiDataAsync<PatronInformation>(url);

                if (response != null)
                {
                    Logger.Info("Successfully fetched patron information for ID: {PatronId}", patronId);
                }
                else
                {
                    Logger.Warn("Patron information not found for ID: {PatronId}", patronId);
                }

                return response;
            }
            catch (OperationCanceledException)
            {
                Logger.Warn("Request was cancelled for PatronId={PatronId}", patronId);
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "GetPatronInformationAsync failed: PatronId={PatronId}", patronId);
                return null;
            }
        }
        public async Task<PagedResult<LevyTicket>> GetLevyTicketsPagedAsync(
            int pageIndex = 1,
            int pageSize = 50,
            string filterType = "<30",
            CancellationToken cancellationToken = default)
        {
            try
            {
                var useTestData = ConfigurationManager.AppSettings["UseTestData"];
                if (useTestData?.ToLower() == "true")
                {
                    Logger.Info("Using TEST DATA from JSON file");
                    return await LoadTestDataFromFileAsync(pageIndex, pageSize, filterType);
                }

                var endpoint = ConfigurationManager.AppSettings["GetLevyTicketsPagedEndpoint"]
                               ?? "api/LevyAccess/getLevyTicketsPaged";

                var url = $"{_levyBaseUrl}{endpoint}?pageIndex={pageIndex}&pageSize={pageSize}&filterType={Uri.EscapeDataString(filterType)}";
                Logger.Info("Fetching Levy Tickets from: {Url}", url);

                var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync();
                    Logger.Warn("GetLevyTicketsPagedAsync failed ({Status}): {Msg}", response.StatusCode, err);
                    return new PagedResult<LevyTicket>
                    {
                        Items = new List<LevyTicket>(),
                        TotalCount = 0,
                        TotalPages = 0
                    };
                }

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<PagedResult<LevyTicket>>(json);

                if (result == null)
                {
                    Logger.Warn("Deserialized result is null.");
                    return new PagedResult<LevyTicket>
                    {
                        Items = new List<LevyTicket>(),
                        TotalCount = 0,
                        TotalPages = 0
                    };
                }

                Logger.Info("Retrieved {Count} tickets (Page {Page}/{TotalPages})",
                    result.Items?.Count ?? 0, pageIndex, result.TotalPages);

                return result;
            }
            catch (OperationCanceledException)
            {
                Logger.Warn("Request was cancelled for PageIndex={PageIndex}", pageIndex);
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "GetLevyTicketsPagedAsync failed: PageIndex={PageIndex}", pageIndex);
                return new PagedResult<LevyTicket>
                {
                    Items = new List<LevyTicket>(),
                    TotalCount = 0,
                    TotalPages = 0
                };
            }
        }

        private async Task<T> GetApiDataAsync<T>(string endpoint)
        {
            //Logger.Debug("Calling API endpoint: {Endpoint}", endpoint);

            var response = await _httpClient.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonConvert.DeserializeObject<ApiBaseResponse<T>>(json);

            if (apiResponse?.Success == true)
            {
                //Logger.Debug("API call successful for endpoint: {Endpoint}", endpoint);
                return apiResponse.Data;
            }

            // Logger.Warn("API call returned unsuccessful response for endpoint: {Endpoint}", endpoint);
            return default;
        }


        private async Task<PagedResult<LevyTicket>> LoadTestDataFromFileAsync(
            int pageIndex,
            int pageSize,
            string filterType)
        {
            try
            {
                var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

                var possiblePaths = new[]
                {
                    Path.Combine(baseDirectory, "Resources", "Levy_ticket_response.json"),
                    Path.Combine(baseDirectory, "Levy_ticket_response.json"),
                    Path.Combine(Directory.GetParent(baseDirectory).Parent.Parent.FullName, "Resources", "Levy_ticket_response.json")
                };

                string filePath = null;
                foreach (var path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        filePath = path;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(filePath))
                {
                    Logger.Error("Test data file not found.");
                    return new PagedResult<LevyTicket>
                    {
                        Items = new List<LevyTicket>(),
                        TotalCount = 0,
                        TotalPages = 0
                    };
                }

                Logger.Info("Reading test data from: {FilePath}", filePath);

                var fileJsonContent = File.ReadAllText(filePath);

                // Parse JSON response structure
                var jsonResponse = JsonConvert.DeserializeObject<TestDataResponse>(fileJsonContent);

                if (jsonResponse == null || jsonResponse.Tickets == null)
                {
                    Logger.Warn("Failed to deserialize test data from file");
                    return new PagedResult<LevyTicket>
                    {
                        Items = new List<LevyTicket>(),
                        TotalCount = 0,
                        TotalPages = 0
                    };
                }

                // Return ALL data - filter on client side
                return new PagedResult<LevyTicket>
                {
                    Items = jsonResponse.Tickets,
                    TotalCount = jsonResponse.TotalCount,
                    TotalPages = (int)Math.Ceiling((double)jsonResponse.TotalCount / pageSize)
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load test data from file");
                return new PagedResult<LevyTicket>
                {
                    Items = new List<LevyTicket>(),
                    TotalCount = 0,
                    TotalPages = 0
                };
            }
        }

        // Helper class for JSON deserialization
        private class TestDataResponse
        {
            public List<LevyTicket> Tickets { get; set; }
            public int TotalCount { get; set; }
            public int TotalPages { get; set; }
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