using System.Windows;
using System.Windows.Controls;
using ChumsLister.WPF.ViewModels;

namespace ChumsLister.WPF.Views
{
    public partial class DashboardPage : Page
    {
        private readonly DashboardViewModel _viewModel;

        public DashboardPage(DashboardViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;

            // When the Page is loaded, kick off LoadDashboardAsync() exactly once.
            Loaded += DashboardPage_Loaded;
        }

        private async void DashboardPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Prevent multiple invocations if WPF raises Loaded more than once
            Loaded -= DashboardPage_Loaded;

            await _viewModel.LoadDashboardAsync();
        }
    }
}
