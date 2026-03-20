using MobileApp.ViewModels;

namespace MobileApp.Views;

public partial class AddLocationPage : ContentPage
{
    public AddLocationPage(AddLocationViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}