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

        private void OnNavigating(object? sender, ShellNavigatingEventArgs e)
        {
            // Skip validation for navigation to LoginPage or SplashScreen
            var targetRoute = e.Target.Location.OriginalString;
            if (targetRoute.Contains(nameof(LoginPage)) ||
                targetRoute.Contains(nameof(SplashScreen)) ||
                targetRoute.Contains("SplashScreen"))
            {
                return;
            }

            // FIXED: Removed token check during navigation to prevent race condition
            // The issue was that after login, tokens are saved asynchronously to SecureStorage,
            // but navigation happens immediately. This caused a race condition where sometimes
            // the tokens weren't available yet, causing navigation to be canceled and redirected
            // back to login page.
            //
            // Token validation is now handled by:
            // 1. SplashScreen - checks tokens on app startup
            // 2. TokenRefreshHandler - handles token refresh for API calls
            // 3. MainPage - will fail gracefully if tokens are missing
            //
            // This allows smooth navigation after login without the double-login issue.
        }
    }
}
