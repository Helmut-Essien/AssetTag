using MobileApp.ViewModels;
using SharedLocation = Shared.Models.Location;

namespace MobileApp.Views;

public partial class AddAssetPage : ContentPage, IQueryAttributable
{
    private string? _assetId;

    public AddAssetPage(AddAssetViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.ContainsKey("assetId"))
        {
            _assetId = query["assetId"]?.ToString();
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        if (BindingContext is AddAssetViewModel viewModel)
        {
            if (!string.IsNullOrEmpty(_assetId))
            {
                await viewModel.LoadAssetAsync(_assetId);
                _assetId = null; // Clear after loading to prevent reloading on subsequent appearances
            }
            else
            {
                await viewModel.InitializeAsync();
            }
        }
    }

    private void LocationResult_Tapped(object? sender, TappedEventArgs e)
    {
        if (BindingContext is not AddAssetViewModel viewModel) return;
        var selectedLocation = (sender as BindableObject)?.BindingContext as SharedLocation;
        if (selectedLocation == null) return;

        if (viewModel.SelectLocationCommand.CanExecute(selectedLocation))
        {
            viewModel.SelectLocationCommand.Execute(selectedLocation);
        }
    }
}