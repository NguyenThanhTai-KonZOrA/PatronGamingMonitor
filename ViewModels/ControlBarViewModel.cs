using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PatronGamingMonitor.ViewModels
{
    public class ControlBarViewModel : BaseViewModel
    {
        #region Commands
        public ICommand CloseWindowCommand { get; set; }
        public ICommand MaximizeCommand { get; set; }
        public ICommand MinimizeCommand { get; set; }
        public ICommand MouseMoveWindowCommand { get; set; }
        #endregion

        public ControlBarViewModel()
        {
            CloseWindowCommand = new RelayCommand<UserControl>(p => { return p == null ? false : true; }, p => { FrameworkElement window = Window.GetWindow(p); (window as Window).Close(); });

            MinimizeCommand = new RelayCommand<UserControl>(p => { return p == null ? false : true; }, p => { FrameworkElement window = Window.GetWindow(p); (window as Window).WindowState = WindowState.Minimized; });

            MouseMoveWindowCommand = new RelayCommand<UserControl>(p => { return p == null ? false : true; }, p =>
            {
                FrameworkElement window = Window.GetWindow(p);
                var temp = window as Window;
                temp.DragMove();
            });
        }
    }
}