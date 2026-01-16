using PatronGamingMonitor.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace PatronGamingMonitor.Views
{
    public partial class PatronDetailWindow : Window, INotifyPropertyChanged
    {
        private List<BitmapImage> _images;
        private int _currentImageIndex = 0;

        public PatronInformation PatronInfo { get; set; }
        public BitmapImage PatronImageSource { get; set; }
        public bool IsLoading { get; set; }

        // Observable collection for thumbnails
        public ObservableCollection<ThumbnailItem> Thumbnails { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public PatronDetailWindow(PatronInformation patronInfo)
        {
            InitializeComponent();
            PatronInfo = patronInfo;
            Thumbnails = new ObservableCollection<ThumbnailItem>();
            DataContext = this;

            LoadImages();
            LoadThumbnails();
            UpdateCarousel();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Get screen working area using WPF native API
            var workingArea = SystemParameters.WorkArea;

            // Limit to 90% of screen height
            double maxHeight = workingArea.Height * 0.9;
            if (this.ActualHeight > maxHeight)
            {
                this.Height = maxHeight;
            }

            // Limit to 40% of screen width (optional)
            double maxWidth = workingArea.Width * 0.4;
            if (maxWidth < 400) maxWidth = 400; // Ensure minimum width
            if (this.ActualWidth > maxWidth)
            {
                this.Width = maxWidth;
            }

            // Center window after size adjustment
            this.Left = workingArea.Left + (workingArea.Width - this.ActualWidth) / 2;
            this.Top = workingArea.Top + (workingArea.Height - this.ActualHeight) / 2;
        }

        private void LoadImages()
        {
            _images = new List<BitmapImage>();

            // Load first image(patronImageBase64)
            if (!string.IsNullOrEmpty(PatronInfo.patronSecondImageBase64))
            {
                _images.Add(Base64ToImage(PatronInfo.patronSecondImageBase64));
            }

            if (!string.IsNullOrEmpty(PatronInfo.patronPrimaryImageBase64))
            {
                _images.Add(Base64ToImage(PatronInfo.patronPrimaryImageBase64));
            }


            // If no images, add placeholder
            if (_images.Count == 0)
            {
                _images.Add(GetPlaceholderImage());
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Adjust Viewbox behavior based on window size for better responsiveness
            if (e.NewSize.Height < 600)
            {
                // For very small screens, scale down only
                ContentViewbox.StretchDirection = StretchDirection.DownOnly;
            }
            else if (e.NewSize.Height < 750)
            {
                // For medium screens, allow both directions
                ContentViewbox.StretchDirection = StretchDirection.Both;
            }
            else
            {
                // For large screens, maintain original size
                ContentViewbox.StretchDirection = StretchDirection.DownOnly;
            }
        }

        // Load thumbnails
        private void LoadThumbnails()
        {
            Thumbnails.Clear();

            for (int i = 0; i < _images.Count; i++)
            {
                Thumbnails.Add(new ThumbnailItem
                {
                    Index = i,
                    ImageSource = _images[i],
                    IsSelected = i == 0 // First image is selected by default
                });
            }

            ThumbnailsContainer.ItemsSource = Thumbnails;
        }

        private BitmapImage Base64ToImage(string base64String)
        {
            try
            {
                // Remove data URI prefix if exists
                if (base64String.Contains(","))
                {
                    base64String = base64String.Split(',')[1];
                }

                byte[] imageBytes = Convert.FromBase64String(base64String);
                using (var ms = new MemoryStream(imageBytes))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
            }
            catch
            {
                return GetPlaceholderImage();
            }
        }

        private BitmapImage GetPlaceholderImage()
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri("pack://application:,,,/Assets/Images/user.png", UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                // Return null if placeholder also fails
                return null;
            }
        }

        private void UpdateCarousel()
        {
            if (_images.Count == 0) return;

            // Update image
            CarouselImage.Source = _images[_currentImageIndex];

            // Update counter
            ImageCounter.Text = $"{_currentImageIndex + 1} / {_images.Count}";

            // Update title
            ImageTitle.Text = _currentImageIndex == 0 ? "Profile Photo" : "ID Card Photo";

            // Update thumbnail selection
            UpdateThumbnailSelection();
        }

        // Update thumbnail selection
        private void UpdateThumbnailSelection()
        {
            for (int i = 0; i < Thumbnails.Count; i++)
            {
                Thumbnails[i].IsSelected = (i == _currentImageIndex);
            }
        }

        // Handle thumbnail click
        private void Thumbnail_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is int index)
            {
                _currentImageIndex = index;
                UpdateCarousel();
            }
        }

        private void PrevImage_Click(object sender, RoutedEventArgs e)
        {
            _currentImageIndex--;
            if (_currentImageIndex < 0)
                _currentImageIndex = _images.Count - 1;

            UpdateCarousel();
        }

        private void NextImage_Click(object sender, RoutedEventArgs e)
        {
            _currentImageIndex++;
            if (_currentImageIndex >= _images.Count)
                _currentImageIndex = 0;

            UpdateCarousel();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    // Thumbnail Item Model
    public class ThumbnailItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public int Index { get; set; }
        public BitmapImage ImageSource { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}