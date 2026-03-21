using MobileApp.ViewModels;

namespace MobileApp.Views;

public partial class EditLocationPage : ContentPage
{
    private readonly EditLocationViewModel _viewModel;

    public EditLocationPage(EditLocationViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Get location ID from query parameters
        if (BindingContext is EditLocationViewModel viewModel)
        {
            var locationId = Shell.Current.CurrentState.Location.ToString().Split('=').LastOrDefault();
            if (!string.IsNullOrEmpty(locationId))
            {
                await viewModel.InitializeAsync(locationId);
            }
        }
    }
}