using MobileApp.ViewModels;

namespace MobileApp.Views;

[QueryProperty(nameof(LocationId), "locationId")]
public partial class EditLocationPage : ContentPage
{
    private readonly EditLocationViewModel _viewModel;
    private string? _locationId;

    public string? LocationId
    {
        get => _locationId;
        set
        {
            _locationId = value;
            if (!string.IsNullOrEmpty(value) && _viewModel != null)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await _viewModel.InitializeAsync(value);
                });
            }
        }
    }

    public EditLocationPage(EditLocationViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }
}