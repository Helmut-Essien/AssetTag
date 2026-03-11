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
            
            // CRITICAL: Only load data ONCE when page is first created
            // Since this page is cached and reused across multiple tabs,
            // we should NOT reload on every OnAppearing
            if (!_hasLoadedOnce)
            {
                _hasLoadedOnce = true;
                _viewModel.IsBusy = true;
                _ = LoadDataAsync();
            }
            // else: Page is cached - show existing data immediately
            // User can manually refresh if needed via pull-to-refresh
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
