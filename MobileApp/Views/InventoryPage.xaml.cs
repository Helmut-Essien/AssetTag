using MobileApp.ViewModels;
using Microsoft.Maui.ApplicationModel;

namespace MobileApp.Views
{
    /// <summary>
    /// Inventory List page - optimized for instant display and smooth tab switching
    /// </summary>
    public partial class InventoryPage : ContentPage
    {
        private readonly InventoryViewModel _viewModel;
        private bool _hasLoadedOnce = false;
        private bool _isCurrentlyLoading = false;

        public InventoryPage(InventoryViewModel viewModel)
        {
            InitializeComponent();
            
            _viewModel = viewModel;
            BindingContext = _viewModel;
            
            // CRITICAL: Ensure IsBusy is false initially to show cached content
            _viewModel.IsBusy = false;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            // CRITICAL: Only load data ONCE when page is first created
            // Since this page is cached and reused, we should NOT reload on every OnAppearing
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
                
                // Load inventory data on background thread
                await Task.Run(async () => await _viewModel.LoadAssetsAsync());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading inventory: {ex.Message}");
                await DisplayAlert("Error", "Failed to load assets. Please try again.", "OK");
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
            _viewModel.IsBusy = false;
        }
    }
}
