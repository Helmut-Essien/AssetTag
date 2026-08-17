using MobileApp.ViewModels;
using SharedLocation = Shared.Models.Location;

namespace MobileApp.Views;

public partial class AddAssetPage : ContentPage, IQueryAttributable
{
    private string? _assetId;
    private bool _hasInitialized;

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

        if (BindingContext is not AddAssetViewModel viewModel)
            return;

        // Scanner modal and Add Location both re-fire OnAppearing. Only initialize once
        // so in-progress form state (and edit mode) is not wiped.
        if (!string.IsNullOrEmpty(_assetId))
        {
            await viewModel.LoadAssetAsync(_assetId);
            _assetId = null;
            _hasInitialized = true;
            return;
        }

        if (!_hasInitialized)
        {
            await viewModel.InitializeAsync();
            _hasInitialized = true;
            return;
        }

        await viewModel.RefreshFormLookupsAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        if (BindingContext is AddAssetViewModel viewModel && viewModel.IsLocationPickerOpen)
            viewModel.CloseLocationPickerCommand.Execute(null);
    }

    protected override bool OnBackButtonPressed()
    {
        if (BindingContext is not AddAssetViewModel viewModel)
            return base.OnBackButtonPressed();

        if (viewModel.IsLocationPickerOpen)
        {
            viewModel.CloseLocationPickerCommand.Execute(null);
            return true;
        }

        if (viewModel.CancelCommand.CanExecute(null))
            _ = viewModel.CancelCommand.ExecuteAsync(null);

        return true;
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