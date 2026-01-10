using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PatronGamingMonitor.ViewModels
{
    public class ControlBarChildWindowViewModel : BaseViewModel
    {
        #region Commands
        public ICommand CloseWindowCommand { get; set; }
        public ICommand MinimizeCommand { get; set; }
        public ICommand MouseMoveWindowCommand { get; set; }
        public ICommand ToggleMaximizeCommand { get; set; }
        #endregion

        public ControlBarChildWindowViewModel()
        {
            CloseWindowCommand = new RelayCommand<UserControl>(p =>
            {
                return p != null;
            },
            p =>
            {
                //var stockInTrade = new StockInTrade();
                //var mainViewModel = new MainWindowViewModel();
                //mainViewModel.LoadStockInTradeData();
                //OnPropertyChanged(nameof(mainViewModel));
                FrameworkElement window = Window.GetWindow(p);
                (window as Window)?.Close();
            });

            MinimizeCommand = new RelayCommand<UserControl>(
                p => p != null,
                p =>
                {
                    FrameworkElement window = Window.GetWindow(p);
                    if (window is Window w) w.WindowState = WindowState.Minimized;
                });

            MouseMoveWindowCommand = new RelayCommand<UserControl>(
                p => p != null,
                p =>
                {
                    FrameworkElement window = Window.GetWindow(p);
                    if (window is Window w) w.DragMove();
                });

            ToggleMaximizeCommand = new RelayCommand<UserControl>(
                p => p != null,
                p =>
                {
                    FrameworkElement window = Window.GetWindow(p);
                    if (window is Window w)
                        w.WindowState = (w.WindowState == WindowState.Maximized)
                            ? WindowState.Normal
                            : WindowState.Maximized;
                });
        }
    }
}