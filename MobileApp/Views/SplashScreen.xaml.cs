using MobileApp.ViewModels;
using MobileApp.Services;

namespace MobileApp.Views
{
    public partial class SplashScreen : ContentPage
    {
        private bool _isAnimating = false;
        private readonly IAuthService _authService;
        private readonly INavigationService _navigationService;
        private readonly MigrationBackgroundService _migrationService;

        // Constructor injection - proper DI pattern
        public SplashScreen(
            IAuthService authService,
            INavigationService navigationService,
            MigrationBackgroundService migrationService)
        {
            InitializeComponent();
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            _migrationService = migrationService ?? throw new ArgumentNullException(nameof(migrationService));
            BindingContext = new SplashScreenViewModel();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            
            // Small delay to ensure UI is rendered
            await Task.Delay(50);
            
            // Start the loading animation - it will loop continuously
            _isAnimating = true;
            _ = AnimateLoadingDots();
            
            // Run minimum display time and auth check concurrently for faster startup.
            // Auth check waits for DB migrations before opening main tabs.
            var minimumDisplayTask = Task.Delay(1500);
            var authCheckTask = PerformAuthenticationCheckAsync();
            
            await Task.WhenAll(minimumDisplayTask, authCheckTask);
            
            // Navigation happens inside PerformAuthenticationCheckAsync
            // Animation is stopped right before navigation for smooth UX
        }

        private async Task PerformAuthenticationCheckAsync()
        {
            try
            {
                // Ensure local SQLite schema exists before opening main tabs
                await _migrationService.WaitForCompletionAsync();

                // Check if user is already logged in with valid tokens
                var (accessToken, refreshToken) = await _authService.GetStoredTokensAsync();
                
                if (!string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(refreshToken))
                {
                    // Tokens exist, now check if they're expired
                    if (await _authService.IsTokenExpiredAsync())
                    {
                        // Token is expired, try to refresh
                        var (success, newTokens, message) = await _authService.RefreshTokenAsync();
                        
                        if (success && newTokens != null)
                        {
                            // Stop animation right before navigation
                            _isAnimating = false;
                            
                            // Token refreshed successfully, navigate to main tabs
                            await _navigationService.ShowMainTabsAsync();
                        }
                        else
                        {
                            // Stop animation right before navigation
                            _isAnimating = false;
                            
                            // Refresh failed, clear tokens and go to login
                            _authService.ClearTokens();
                            await _navigationService.NavigateToAsync($"/{nameof(LoginPage)}");
                        }
                    }
                    else
                    {
                        // Stop animation right before navigation
                        _isAnimating = false;
                        
                        // Token is still valid, navigate to main tabs
                        await _navigationService.ShowMainTabsAsync();
                    }
                }
                else
                {
                    // Stop animation right before navigation
                    _isAnimating = false;
                    
                    // No tokens, navigate to login page
                    await _navigationService.NavigateToAsync($"/{nameof(LoginPage)}");
                }
            }
            catch (Exception)
            {
                // Stop animation on error
                _isAnimating = false;
                
                // Log error if you have a logger, for now just navigate to login as fallback
                await _navigationService.NavigateToAsync($"/{nameof(LoginPage)}");
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            
            // Ensure animation stops when page disappears
            _isAnimating = false;
        }

        private async Task AnimateLoadingDots()
        {
            // Animation loops continuously while _isAnimating is true
            while (_isAnimating)
            {
                // Dot 1 pulses
                await Dot1.FadeTo(1, 200);
                await Task.Delay(100);
                
                // Dot 2 pulses
                await Dot2.FadeTo(1, 200);
                await Task.Delay(100);
                
                // Dot 3 pulses
                await Dot3.FadeTo(1, 200);
                await Task.Delay(200);
                
                // All fade back to dim state
                await Task.WhenAll(
                    Dot1.FadeTo(0.3, 200),
                    Dot2.FadeTo(0.3, 200),
                    Dot3.FadeTo(0.3, 200)
                );
                
                // Brief pause before next cycle
                await Task.Delay(100);
            }
        }
    }
}