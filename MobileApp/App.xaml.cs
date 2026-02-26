using MobileApp.Services;

namespace MobileApp
{
    public partial class App : Application
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly BackgroundSyncService _backgroundSyncService;
        private readonly MigrationBackgroundService _migrationService;

        // Constructor injection for App
        public App(
            IServiceProvider serviceProvider,
            BackgroundSyncService backgroundSyncService,
            MigrationBackgroundService migrationService)
        {
            InitializeComponent();
            _serviceProvider = serviceProvider;
            _backgroundSyncService = backgroundSyncService;
            _migrationService = migrationService;

            // Migration service starts automatically in its constructor
            // No need to call Start() - it runs migrations in background

            // Start background sync service
            _backgroundSyncService.Start();

            // Monitor network connectivity changes
            Connectivity.ConnectivityChanged += OnConnectivityChanged;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            // Resolve AppShell from DI container
            var shell = _serviceProvider.GetRequiredService<AppShell>();
            return new Window(shell);
        }

        /// <summary>
        /// Handle network connectivity changes
        /// Trigger immediate sync when internet is restored
        /// </summary>
        private async void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
        {
            if (e.NetworkAccess == NetworkAccess.Internet)
            {
                // Internet restored - trigger immediate sync
                System.Diagnostics.Debug.WriteLine("Internet connection restored - triggering sync");
                
                // Use fire-and-forget to avoid blocking
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _backgroundSyncService.TriggerImmediateSyncAsync();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Network restore sync failed: {ex.Message}");
                    }
                });
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Internet connection lost");
            }
        }

        protected override void CleanUp()
        {
            // Unsubscribe from events
            Connectivity.ConnectivityChanged -= OnConnectivityChanged;
            
            // Stop background sync service
            _backgroundSyncService?.Stop();
            
            base.CleanUp();
        }
    }
}