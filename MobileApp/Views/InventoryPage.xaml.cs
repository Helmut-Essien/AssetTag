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

            // Load data on first appearance
            if (!_hasLoadedOnce)
            {
                _hasLoadedOnce = true;
                _viewModel.IsBusy = true;
                _ = LoadDataAsync();
            }
            else
            {
                // Reload data when returning to the page (e.g., after adding/updating an asset)
                // This ensures the list is always up-to-date
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

        private async void OnBackButtonClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            
            // CRITICAL: Ensure IsBusy is false when leaving page
            _viewModel.IsBusy = false;
        }
    }
}
