using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PatronGamingMonitor.Models
{
    public class PatronInformation : INotifyPropertyChanged
    {
        public int playerID { get; set; }
        public string patronSecondImageBase64 { get; set; }
        public string patronPrimaryImageBase64 { get; set; }
        public string fullName { get; set; }
        public string birthday { get; set; }
        public string idCard { get; set; }
        public string address { get; set; }
        public string city { get; set; }
        public string country { get; set; }
        public string age { get; set; }
        public string gender { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}