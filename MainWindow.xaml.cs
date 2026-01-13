using PatronGamingMonitor.Models;
using PatronGamingMonitor.ViewModels;
using PatronGamingMonitor.Views;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PatronGamingMonitor
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void DataGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            // Prevent default DataGrid sorting
            e.Handled = true;

            var viewModel = DataContext as MainWindowViewModel;
            if (viewModel == null) return;

            var columnName = e.Column.SortMemberPath;
            if (string.IsNullOrEmpty(columnName)) return;

            // Determine new sort direction
            ListSortDirection newDirection;
            if (e.Column.SortDirection == null || e.Column.SortDirection == ListSortDirection.Descending)
            {
                newDirection = ListSortDirection.Ascending;
                e.Column.SortDirection = ListSortDirection.Ascending;
            }
            else
            {
                newDirection = ListSortDirection.Descending;
                e.Column.SortDirection = ListSortDirection.Descending;
            }

            // Clear other columns' sort indicators
            var dataGrid = sender as DataGrid;
            if (dataGrid != null)
            {
                foreach (var column in dataGrid.Columns)
                {
                    if (column != e.Column)
                        column.SortDirection = null;
                }
            }

            // Call ViewModel method to sort
            viewModel.SortData(columnName, newDirection);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                // Allow dragging the window
                DragMove();
            }
        }

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var dataGrid = sender as DataGrid;
            if (dataGrid == null || dataGrid.SelectedItem == null)
                return;

            var selectedTicket = dataGrid.SelectedItem as LevyTicket;
            if (selectedTicket == null || selectedTicket.PlayerID <= 0)
                return;

            // Open Patron Detail Window
            var patronWindow = new PatronDetailWindow(selectedTicket.PlayerID);
            patronWindow.ShowDialog();
        }
    }
}