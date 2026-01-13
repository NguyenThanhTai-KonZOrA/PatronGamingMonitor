using PatronGamingMonitor.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace PatronGamingMonitor.Views
{
    public partial class PatronDetailWindow : Window
    {
        public PatronDetailWindow(int patronId)
        {
            InitializeComponent();
            DataContext = new PatronDetailViewModel(patronId);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                // Allow dragging the window
                DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}