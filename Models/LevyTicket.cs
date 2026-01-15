using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace PatronGamingMonitor.Models
{
    public class LevyTicket : INotifyPropertyChanged
    {
        private int _remainingTime;
        private int _playingTime;
        private bool _isNew;
        private bool _isUpdated;

        public int PlayerID { get; set; }
        public string FullName { get; set; }
        public string Location { get; set; }
        public DateTime? StartTime { get; set; }
        public string Seat { get; set; }
        public string Row { get; set; }
        public string PitName { get; set; }
        public string Type { get; set; }
        public string TransactionNo { get; set; }
        public string LevyType { get; set; }
        public string UsedStatus { get; set; }
        public string Area { get; set; }
        public string LocalStatus { get; set; }


        public int RemainingTime
        {
            get => _remainingTime;
            set
            {
                if (_remainingTime != value)
                {
                    _remainingTime = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FormattedPlayingTime));
                    OnPropertyChanged(nameof(StatusColor));
                }
            }
        }

        public int PlayingTime
        {
            get => _playingTime;
            set
            {
                if (_playingTime != value)
                {
                    _playingTime = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FormattedPlayingTime));
                    OnPropertyChanged(nameof(StatusColor));
                }
            }
        }

        // Animation flags
        public bool IsNew
        {
            get => _isNew;
            set
            {
                if (_isNew != value)
                {
                    _isNew = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsUpdated
        {
            get => _isUpdated;
            set
            {
                if (_isUpdated != value)
                {
                    _isUpdated = value;
                    OnPropertyChanged();
                }
            }
        }

        // UPDATED: Formatted Playing Time Property
        public string FormattedPlayingTime
        {
            get
            {
                var absSeconds = Math.Abs(PlayingTime);
                var ts = TimeSpan.FromSeconds(absSeconds);

                string sign = PlayingTime < 0 ? "-" : "";

                if (ts.Days > 0)
                {
                    // Format: Day:HH:MM:SS
                    return $"{sign}{ts.Days} day {ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
                }
                else
                {
                    // Format: HH:MM:SS
                    return $"{sign}{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
                }
            }
        }

        public Brush StatusColor
        {
            get
            {
                // #FFE0B2 - Light orange
                if (PlayingTime >= 172800)
                    return new SolidColorBrush(Color.FromRgb(235, 150, 125));
                // #FFF5C8 - Light yellow
                if (PlayingTime >= 86400)
                    return new SolidColorBrush(Color.FromRgb(255, 200, 200));
                // #E6F5C8 - Light lime
                if (PlayingTime >= 43200)
                    return new SolidColorBrush(Color.FromRgb(245, 241, 137));
                // #C8F5D2 - Light green
                return new SolidColorBrush(Color.FromRgb(165, 210, 255));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}