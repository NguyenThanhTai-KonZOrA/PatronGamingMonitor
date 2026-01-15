using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PatronGamingMonitor.Models;

namespace PatronGamingMonitor.Views
{
    public partial class PatronDetailWindow : Window
    {
        private List<BitmapImage> _images;
        private int _currentImageIndex = 0;

        public PatronInformation PatronInfo { get; set; }
        public BitmapImage PatronImageSource { get; set; }
        public bool IsLoading { get; set; }

        public PatronDetailWindow(PatronInformation patronInfo)
        {
            InitializeComponent();
            PatronInfo = patronInfo;
            DataContext = this;

            LoadImages();
            UpdateCarousel();
        }

        private void LoadImages()
        {
            _images = new List<BitmapImage>();

            // Load first image (patronImageBase64)
            if (!string.IsNullOrEmpty(PatronInfo.patronImageBase64))
            {
                _images.Add(Base64ToImage(PatronInfo.patronImageBase64));
            }

            // Load second image (patronPrimaryImageBase64)
            //if (!string.IsNullOrEmpty(PatronInfo.patronPrimaryImageBase64))
            //{
            //    _images.Add(Base64ToImage(PatronInfo.patronPrimaryImageBase64));
            //}
            if (!string.IsNullOrEmpty(PatronInfo.patronImageBase64))
            {
                _images.Add(Base64ToImage(PatronInfo.patronImageBase64));
            }

            // If no images, add placeholder
            if (_images.Count == 0)
            {
                _images.Add(GetPlaceholderImage());
            }
        }

        private BitmapImage Base64ToImage(string base64String)
        {
            try
            {
                base64String = base64String.Split(',')[1];
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
            // Create a simple placeholder image
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri("pack://application:,,,/Assets/Images/user.png", UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
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

            // Update dots
            Dot1.Fill = _currentImageIndex == 0
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A3650"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC"));

            Dot2.Fill = _currentImageIndex == 1
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A3650"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC"));

            // Show/hide navigation buttons based on image count
            if (_images.Count <= 1)
            {
                PrevButton.Visibility = Visibility.Collapsed;
                NextButton.Visibility = Visibility.Collapsed;
                Dot1.Visibility = Visibility.Collapsed;
                Dot2.Visibility = Visibility.Collapsed;
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
}