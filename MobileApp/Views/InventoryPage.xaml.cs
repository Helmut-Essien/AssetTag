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
                
                // Yield to let UI thread render first; keep ViewModel on UI context
                await Task.Yield();
                await _viewModel.LoadAssetsAsync();
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

        protected override bool OnBackButtonPressed()
        {
            if (_viewModel.IsFilterPickerOpen)
            {
                _viewModel.CloseFilterPickerCommand.Execute(null);
                return true;
            }

            return base.OnBackButtonPressed();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            if (_viewModel.IsFilterPickerOpen)
                _viewModel.CloseFilterPickerCommand.Execute(null);

            // CRITICAL: Ensure IsBusy is false when leaving page
            _viewModel.IsBusy = false;
        }

        private void FilterOption_Tapped(object? sender, TappedEventArgs e)
        {
            if (BindingContext is not InventoryViewModel viewModel) return;
            var option = (sender as BindableObject)?.BindingContext as FilterOption;
            if (option == null) return;

            if (viewModel.SelectFilterOptionCommand.CanExecute(option))
                viewModel.SelectFilterOptionCommand.Execute(option);
        }
    }
}
