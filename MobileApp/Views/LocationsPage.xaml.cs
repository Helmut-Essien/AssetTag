using MobileApp.ViewModels;

namespace MobileApp.Views;

public partial class LocationsPage : ContentPage
{
    private readonly LocationsViewModel _viewModel;

    public LocationsPage(LocationsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Load locations when page appears
        await _viewModel.LoadLocationsCommand.ExecuteAsync(null);
    }
}