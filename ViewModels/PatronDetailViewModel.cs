using NLog;
using PatronGamingMonitor.Models;
using PatronGamingMonitor.Supports;
using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace PatronGamingMonitor.ViewModels
{
    public class PatronDetailViewModel : BaseViewModel
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly PatronService _patronService;
        private readonly int _patronId;

        private PatronInformation _patronInfo;
        public PatronInformation PatronInfo
        {
            get => _patronInfo;
            set
            {
                _patronInfo = value;
                OnPropertyChanged();
                UpdatePatronImage();
            }
        }

        private BitmapImage _patronImageSource;
        public BitmapImage PatronImageSource
        {
            get => _patronImageSource;
            set
            {
                _patronImageSource = value;
                OnPropertyChanged();
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }

        public PatronDetailViewModel(int patronId)
        {
            _patronId = patronId;
            _patronService = new PatronService();
            LoadPatronInformationAsync();
        }

        private async void LoadPatronInformationAsync()
        {
            try
            {
                IsLoading = true;
                Logger.Info("Loading patron information for PatronID={PatronId}", _patronId);

                var patron = await _patronService.GetPatronInformationAsync(_patronId);

                if (patron != null)
                {
                    if (patron.gender == "F")
                    {
                        patron.gender = "Female";
                    }
                    else
                    {
                        patron.gender = "Male";
                    }

                    PatronInfo = patron;
                    Logger.Info("✅ Patron information loaded successfully");
                }
                else
                {
                    Logger.Warn("⚠️ No patron information found for PatronID={PatronId}", _patronId);
                    PatronInfo = new PatronInformation
                    {
                        playerID = _patronId,
                        fullName = "No information available"
                    };
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "❌ Error loading patron information");
                PatronInfo = new PatronInformation
                {
                    playerID = _patronId,
                    fullName = "Error loading information"
                };
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void UpdatePatronImage()
        {
            try
            {
                if (PatronInfo == null || string.IsNullOrWhiteSpace(PatronInfo.patronSecondImageBase64))
                {
                    PatronImageSource = null;
                    return;
                }
                string base64Data = PatronInfo.patronSecondImageBase64.Split(',')[1];
                var imageBytes = Convert.FromBase64String(base64Data);
                var image = new BitmapImage();

                using (var mem = new MemoryStream(imageBytes))
                {
                    mem.Position = 0;
                    image.BeginInit();
                    image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = mem;
                    image.EndInit();
                }

                image.Freeze();
                PatronImageSource = image;
                IsLoading = false;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "❌ Error converting patron image from Base64");
                PatronImageSource = null;
            }
        }
    }
}