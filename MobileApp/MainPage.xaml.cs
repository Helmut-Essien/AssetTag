using MobileApp.ViewModels;

namespace MobileApp
{
    /// <summary>
    /// Main dashboard page - optimized for instant skeleton display
    /// </summary>
    public partial class MainPage : ContentPage
    {
        private readonly MainPageViewModel _viewModel;
        private bool _hasLoadedOnce = false;

        public MainPage(MainPageViewModel viewModel)
        {
            InitializeComponent();
            
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            
            // PERFORMANCE: Start loading immediately on first appearance
            // Skeleton is already visible because IsBusy=true in ViewModel constructor
            if (!_hasLoadedOnce)
            {
                _hasLoadedOnce = true;
                // Fire and forget - let it run in background
                _ = LoadDataAsync();
            }
            else
            {
                // On subsequent appearances, refresh data
                _ = RefreshDataAsync();
            }
        }

        private async Task LoadDataAsync()
        {
            try
            {
                // Yield to let UI thread render the skeleton first
                await Task.Yield();
                
                // Load dashboard data
                await _viewModel.LoadDashboardDataAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading dashboard data: {ex.Message}");
            }
            finally
            {
                _viewModel.IsBusy = false;
            }
        }

        private async Task RefreshDataAsync()
        {
            try
            {
                _viewModel.IsBusy = true;
                await Task.Yield();
                await _viewModel.LoadDashboardDataAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing dashboard data: {ex.Message}");
            }
            finally
            {
                _viewModel.IsBusy = false;
            }
        }
    }
}
