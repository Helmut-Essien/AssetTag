using MobileApp.ViewModels;

namespace MobileApp
{
    /// <summary>
    /// Main dashboard page - now using MVVM pattern
    /// </summary>
    public partial class MainPage : ContentPage
    {
        private readonly MainPageViewModel _viewModel;

        public MainPage(MainPageViewModel viewModel)
        {
            InitializeComponent();
            
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            
            // Load dashboard data when page appears
            await _viewModel.LoadDashboardDataCommand.ExecuteAsync(null);
        }
    }
}
