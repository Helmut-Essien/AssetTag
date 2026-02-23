using MobileApp.ViewModels;

namespace MobileApp.Views
{
    /// <summary>
    /// Inventory List page displaying all assets with search, filter, and sync capabilities
    /// </summary>
    public partial class InventoryPage : ContentPage
    {
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
                await viewModel.LoadAssetsCommand.ExecuteAsync(null);
            }
        }
    }
}