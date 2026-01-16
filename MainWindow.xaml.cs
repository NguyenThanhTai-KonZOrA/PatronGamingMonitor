using PatronGamingMonitor.Models;
using PatronGamingMonitor.Supports;
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
        private readonly ApiClient _apiClient;
        private readonly PatronService _patronService;

        public MainWindow()
        {
            InitializeComponent();
            _apiClient = new ApiClient();
            _patronService = new PatronService();
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

        private async void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("DataGrid_MouseDoubleClick fired");

            var dataGrid = sender as DataGrid;
            if (dataGrid == null) return;

            var row = ItemsControl.ContainerFromElement(dataGrid,
                e.OriginalSource as DependencyObject) as DataGridRow;

            if (row == null)
            {
                System.Diagnostics.Debug.WriteLine("Row is null - click was not on a row");
                return;
            }

            var selectedTicket = row.Item as LevyTicket;
            if (selectedTicket == null || selectedTicket.PlayerID <= 0)
            {
                System.Diagnostics.Debug.WriteLine("No valid ticket in row");
                return;
            }

            // ✅ FIX: Set the row as selected
            dataGrid.SelectedItem = selectedTicket;
            dataGrid.Focus();
            row.IsSelected = true;

            System.Diagnostics.Debug.WriteLine($"Row selected for Player ID: {selectedTicket.PlayerID}");

            // Show loading indicator
            Mouse.OverrideCursor = Cursors.Wait;

            PatronInformation patronInfo = null;

            try
            {
                // Fetch patron information from API
                patronInfo = await _patronService.GetPatronInformationAsync(selectedTicket.PlayerID);
                if (patronInfo != null)
                {
                    if (patronInfo.gender == "F")
                    {
                        patronInfo.gender = "Female";
                    }
                    else
                    {
                        patronInfo.gender = "Male";
                    }
                }

                if (patronInfo == null)
                {
                    MessageBox.Show(
                        $"Could not find information for Player ID: {selectedTicket.PlayerID}",
                        "Player Not Found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(
                    $"Error loading player information:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }

            // Open dialog AFTER cursor is reset
            if (patronInfo != null)
            {
                var patronWindow = new PatronDetailWindow(patronInfo);

                // ✅ FIX: Set Owner directly to this window
                patronWindow.Owner = this;

                // 90% of MainWindow height
                double maxHeight = this.ActualHeight * 0.9;
                // 40% of MainWindow width
                double maxWidth = this.ActualWidth * 0.4;

                // Set max constraints to prevent popup from being larger than MainWindow
                patronWindow.MaxHeight = maxHeight;
                patronWindow.MaxWidth = maxWidth;

                // Adjust initial size if needed
                if (patronWindow.Height > maxHeight)
                {
                    patronWindow.Height = maxHeight;
                }
                if (patronWindow.Width > maxWidth)
                {
                    patronWindow.Width = maxWidth;
                }

                patronWindow.ShowDialog();
            }
        }

        private void TableFilter_Click(object sender, MouseButtonEventArgs e)
        {
            var viewModel = DataContext as MainWindowViewModel;
            if (viewModel == null) return;

            // Apply filter by Type = "Table"
            viewModel.ApplyTypeFilterCommand?.Execute("Table");
        }

        private void SlotFilter_Click(object sender, MouseButtonEventArgs e)
        {
            var viewModel = DataContext as MainWindowViewModel;
            if (viewModel == null) return;

            // Apply filter by Type = "Slot"
            viewModel.ApplyTypeFilterCommand?.Execute("Slot");
        }

        protected override void OnClosed(System.EventArgs e)
        {
            base.OnClosed(e);
            _apiClient?.Dispose();
            _patronService?.Dispose();
        }
    }
}