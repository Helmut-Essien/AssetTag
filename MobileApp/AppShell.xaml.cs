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
            
            // Register LoginPage route for Shell navigation
            Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
            
            // Set initial content to DI-resolved SplashScreen
            var splashScreen = _serviceProvider.GetRequiredService<SplashScreen>();
            InitialContent.Content = splashScreen;

            // Subscribe to navigation events for token validation
            this.Navigating += OnNavigating;
        }

        /// <summary>
        /// Show the main tab bar after successful login
        /// </summary>
        public async Task ShowMainTabsAsync()
        {
            // Hide the initial splash/login content
            InitialContent.IsVisible = false;
            
            // Show the main tab bar
            MainTabBar.IsVisible = true;
            
            // Navigate to the Home tab using absolute routing
            await Shell.Current.GoToAsync("///MainTabs/Home");
        }

        /// <summary>
        /// Show the login page (hide tabs)
        /// </summary>
        public Task ShowLoginAsync()
        {
            // Hide the tab bar
            MainTabBar.IsVisible = false;
            
            // Show the initial content
            InitialContent.IsVisible = true;
            
            // Set the login page as the current content
            var loginPage = _serviceProvider.GetRequiredService<LoginPage>();
            InitialContent.Content = loginPage;
            
            return Task.CompletedTask;
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
