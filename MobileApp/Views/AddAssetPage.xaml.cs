using MobileApp.ViewModels;

namespace MobileApp.Views;

public partial class AddAssetPage : ContentPage
{
    public AddAssetPage(AddAssetViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        if (BindingContext is AddAssetViewModel viewModel)
        {
            await viewModel.InitializeAsync();
        }
    }
}