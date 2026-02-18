using MobileApp.Views;
using MobileApp.Services;

namespace MobileApp
{
    public partial class AppShell : Shell
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IAuthService _authService;

        // Constructor injection for AppShell
        public AppShell(IServiceProvider serviceProvider, IAuthService authService)
        {
            InitializeComponent();
            _serviceProvider = serviceProvider;
            _authService = authService;
            
            // Set initial content to DI-resolved SplashScreen
            var splashScreen = _serviceProvider.GetRequiredService<SplashScreen>();
            InitialContent.Content = splashScreen;
            
            // Register routes for navigation with factory methods for DI
            Routing.RegisterRoute(nameof(SplashScreen), typeof(SplashScreen));
            Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
            Routing.RegisterRoute(nameof(MainPage), typeof(MainPage));

            // Subscribe to navigation events for token validation
            this.Navigating += OnNavigating;
        }

        private async void OnNavigating(object? sender, ShellNavigatingEventArgs e)
        {
            // Skip validation for navigation to LoginPage or SplashScreen
            var targetRoute = e.Target.Location.OriginalString;
            if (targetRoute.Contains(nameof(LoginPage)) ||
                targetRoute.Contains(nameof(SplashScreen)) ||
                targetRoute.Contains("SplashScreen"))
            {
                return;
            }

            // Validate token for all other navigations
            try
            {
                var (accessToken, refreshToken) = _authService.GetStoredTokens();
                
                // Check if tokens exist
                if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
                {
                    // Cancel navigation and redirect to login
                    e.Cancel();
                    await GoToAsync($"/{nameof(LoginPage)}");
                    return;
                }

                // Check if token is expired
                if (await _authService.IsTokenExpiredAsync())
                {
                    // Try to refresh the token
                    var (success, newTokens, message) = await _authService.RefreshTokenAsync();
                    
                    if (!success || newTokens == null)
                    {
                        // Refresh failed, cancel navigation and redirect to login
                        e.Cancel();
                        _authService.ClearTokens();
                        await GoToAsync($"/{nameof(LoginPage)}");
                        return;
                    }
                    
                    // Token refreshed successfully, allow navigation to continue
                }
                
                // Token is valid, allow navigation to continue
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Navigation token validation error: {ex.Message}");
                // On error, cancel navigation and redirect to login for safety
                e.Cancel();
                await GoToAsync($"/{nameof(LoginPage)}");
            }
        }
    }
}
