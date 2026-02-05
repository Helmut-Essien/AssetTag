namespace MobileApp
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
            LoadDashboardData();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            LoadDashboardData();
        }

        private async void LoadDashboardData()
        {
            try
            {
                // TODO: Replace with actual data from your database/service
                // This is placeholder data for demonstration
                TotalAssetsLabel.Text = "0";
                ScannedTodayLabel.Text = "0";
                PendingSyncLabel.Text = "0";
                CategoriesLabel.Text = "0";
                LastSyncLabel.Text = "Never synced";

                // Example: Load from database
                // var totalAssets = await _assetService.GetTotalAssetsCount();
                // TotalAssetsLabel.Text = totalAssets.ToString();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to load dashboard data: {ex.Message}", "OK");
            }
        }

        private async void OnScanAssetTapped(object? sender, EventArgs e)
        {
            try
            {
                // TODO: Navigate to barcode/QR scanner page
                await DisplayAlert("Scan Asset", "Opening scanner...", "OK");
                // await Shell.Current.GoToAsync("//ScanPage");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private async void OnAddAssetTapped(object? sender, EventArgs e)
        {
            try
            {
                // TODO: Navigate to add asset page
                await DisplayAlert("Add Asset", "Opening asset registration form...", "OK");
                // await Shell.Current.GoToAsync("//AddAssetPage");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private async void OnViewAssetsTapped(object? sender, EventArgs e)
        {
            try
            {
                // TODO: Navigate to assets list page
                await DisplayAlert("View Assets", "Opening asset inventory...", "OK");
                // await Shell.Current.GoToAsync("//AssetsPage");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private async void OnSyncDataTapped(object? sender, EventArgs e)
        {
            try
            {
                // TODO: Implement sync logic
                await DisplayAlert("Sync Data", "Syncing with server...", "OK");
                
                // Example sync logic:
                // var pendingChanges = await _syncService.GetPendingChanges();
                // await _syncService.SyncToServer(pendingChanges);
                // LastSyncLabel.Text = DateTime.Now.ToString("MMM dd, yyyy HH:mm");
                // PendingSyncLabel.Text = "0";
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Sync failed: {ex.Message}", "OK");
            }
        }

        private void OnHomeTapped(object? sender, EventArgs e)
        {
            // Already on home page
            LoadDashboardData();
        }

        private async void OnAssetsTapped(object? sender, EventArgs e)
        {
            try
            {
                // TODO: Navigate to assets page
                await DisplayAlert("Assets", "Opening assets page...", "OK");
                // await Shell.Current.GoToAsync("//AssetsPage");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private async void OnReportsTapped(object? sender, EventArgs e)
        {
            try
            {
                // TODO: Navigate to reports page
                await DisplayAlert("Reports", "Opening reports page...", "OK");
                // await Shell.Current.GoToAsync("//ReportsPage");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private async void OnSettingsTapped(object? sender, EventArgs e)
        {
            try
            {
                // TODO: Navigate to settings page
                await DisplayAlert("Settings", "Opening settings page...", "OK");
                // await Shell.Current.GoToAsync("//SettingsPage");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }
    }
}
