using MobileApp.ViewModels;

namespace MobileApp
{
    /// <summary>
    /// Main dashboard page - optimized for instant skeleton display and smooth tab switching
    /// </summary>
    public partial class MainPage : ContentPage
    {
        private readonly MainPageViewModel _viewModel;
        private bool _hasLoadedOnce = false;
        private bool _isCurrentlyLoading = false;

        public MainPage(MainPageViewModel viewModel)
        {
            InitializeComponent();
            
            _viewModel = viewModel;
            BindingContext = _viewModel;
            
            // CRITICAL: Set IsBusy to false initially to show cached content immediately
            // This prevents black screen on rapid tab switches
            _viewModel.IsBusy = false;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            
            // Load data on first appearance
            if (!_hasLoadedOnce)
            {
                _hasLoadedOnce = true;
                _viewModel.IsBusy = true;
                _ = LoadDataAsync();
            }
            else
            {
                // Reload data when returning to the page to show updated pending sync count
                // This ensures dashboard stats are always current
                _ = LoadDataAsync();
            }
        }

        private async Task LoadDataAsync()
        {
            if (_isCurrentlyLoading) return;
            
            try
            {
                _isCurrentlyLoading = true;
                
                // Yield to let UI thread render first
                await Task.Yield();
                
                // Load dashboard data on background thread
                await Task.Run(async () => await _viewModel.LoadDashboardDataAsync());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading dashboard data: {ex.Message}");
            }
            finally
            {
                _viewModel.IsBusy = false;
                _isCurrentlyLoading = false;
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            
            // CRITICAL: Ensure IsBusy is false when leaving page
            // This prevents black screen when returning
            _viewModel.IsBusy = false;
        }
    }
}
