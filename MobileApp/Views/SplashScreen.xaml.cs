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
            var minimumDisplayTask = Task.Delay(400);
            var authCheckTask = PerformAuthenticationCheckAsync();
            
            await Task.WhenAll(minimumDisplayTask, authCheckTask);
            
            // Navigation happens inside PerformAuthenticationCheckAsync
            // Animation is stopped right before navigation for smooth UX
        }

        private async Task PerformAuthenticationCheckAsync()
        {
            while (true)
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
                            var refresh = await _authService.RefreshTokenAsync();
                            
                            if (refresh.Succeeded && refresh.Token != null)
                            {
                                _isAnimating = false;
                                await _navigationService.ShowMainTabsAsync();
                                return;
                            }
                            else if (refresh.IsTransientFailure)
                            {
                                // Offline or timeout — keep the stored session and open the app
                                _isAnimating = false;
                                await _navigationService.ShowMainTabsAsync();
                                return;
                            }
                            else
                            {
                                _isAnimating = false;
                                await _navigationService.ShowLoginAsync();
                                return;
                            }
                        }
                        else
                        {
                            _isAnimating = false;
                            await _navigationService.ShowMainTabsAsync();
                            return;
                        }
                    }
                    else
                    {
                        _isAnimating = false;
                        await _navigationService.ShowLoginAsync();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _isAnimating = false;
                    System.Diagnostics.Debug.WriteLine($"Startup failed: {ex.Message}");

                    var (accessToken, refreshToken) = await _authService.GetStoredTokensAsync();
                    var hasSession = !string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(refreshToken);

                    if (!hasSession)
                    {
                        await _navigationService.ShowLoginAsync();
                        return;
                    }

                    var retry = await DisplayAlert(
                        "Startup Error",
                        "The app could not finish starting. Your session is still saved.",
                        "Retry",
                        "Stay here");

                    if (!retry)
                        return;

                    _isAnimating = true;
                    _ = AnimateLoadingDots();
                }
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