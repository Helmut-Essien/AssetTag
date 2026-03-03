using MobileApp.ViewModels;
using Microsoft.Maui.ApplicationModel;

namespace MobileApp.Views
{
    /// <summary>
    /// Inventory List page - optimized for instant skeleton display
    /// </summary>
    public partial class InventoryPage : ContentPage
    {
        private readonly InventoryViewModel _viewModel;
        private bool _hasLoadedOnce = false;

        public InventoryPage(InventoryViewModel viewModel)
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
                
                // Load inventory data
                await _viewModel.LoadAssetsAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading inventory: {ex.Message}");
                await DisplayAlert("Error", "Failed to load assets. Please try again.", "OK");
            }
        }

        private async Task RefreshDataAsync()
        {
            try
            {
                _viewModel.IsBusy = true;
                await Task.Yield();
                await _viewModel.LoadAssetsAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing inventory: {ex.Message}");
                await DisplayAlert("Error", "Failed to refresh assets. Please try again.", "OK");
            }
        }
    }
}
