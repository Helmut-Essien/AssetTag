using MobileApp.ViewModels;

namespace MobileApp.Views
{
    /// <summary>
    /// Inventory List page displaying all assets with search, filter, and sync capabilities
    /// </summary>
    public partial class InventoryPage : ContentPage
    {
        private bool _isFirstLoad = true;

        public InventoryPage(InventoryViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Load assets when page appears
            if (BindingContext is InventoryViewModel viewModel)
            {
                // OPTIMIZATION: Only load on first appearance or if explicitly needed
                // This prevents reloading every time user navigates back to this page
                if (_isFirstLoad || viewModel.Assets.Count == 0)
                {
                    await viewModel.LoadAssetsCommand.ExecuteAsync(null);
                    _isFirstLoad = false;
                }
                else
                {
                    // Just update sync status without full reload
                    // This is much faster than reloading all assets
                    await viewModel.RefreshCommand.ExecuteAsync(null);
                }
            }
        }
    }
}