using MobileApp.ViewModels;

namespace MobileApp.Views;

public partial class AddLocationPage : ContentPage
{
    public AddLocationPage(AddLocationViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override bool OnBackButtonPressed()
    {
        if (BindingContext is not AddLocationViewModel viewModel)
            return base.OnBackButtonPressed();

        if (viewModel.CancelCommand.CanExecute(null))
            _ = viewModel.CancelCommand.ExecuteAsync(null);

        return true;
    }
}