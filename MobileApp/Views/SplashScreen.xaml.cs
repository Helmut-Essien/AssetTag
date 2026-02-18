using MobileApp.ViewModels;
using MobileApp.Services;

namespace MobileApp.Views
{
    public partial class SplashScreen : ContentPage
    {
        private bool _isAnimating = false;
        private readonly IAuthService _authService;

        // Constructor injection - proper DI pattern
        public SplashScreen(IAuthService authService)
        {
            InitializeComponent();
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
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
            
            // Reduced wait time for faster startup (1.5 seconds instead of 2)
            // Animation loops during this time
            await Task.Delay(1500);
            
            // Stop animation before navigation
            _isAnimating = false;
            
            // Check if user is already logged in with valid tokens
            var (accessToken, refreshToken) = _authService.GetStoredTokens();
            
            if (!string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(refreshToken))
            {
                // Tokens exist, now check if they're expired
                if (await _authService.IsTokenExpiredAsync())
                {
                    // Token is expired, try to refresh
                    var (success, newTokens, message) = await _authService.RefreshTokenAsync();
                    
                    if (success && newTokens != null)
                    {
                        // Token refreshed successfully, navigate to main page
                        await Shell.Current.GoToAsync($"/{nameof(MainPage)}");
                    }
                    else
                    {
                        // Refresh failed, clear tokens and go to login
                        _authService.ClearTokens();
                        await Shell.Current.GoToAsync($"/{nameof(LoginPage)}");
                    }
                }
                else
                {
                    // Token is still valid, navigate to main page
                    await Shell.Current.GoToAsync($"/{nameof(MainPage)}");
                }
            }
            else
            {
                // No tokens, navigate to login page
                await Shell.Current.GoToAsync($"/{nameof(LoginPage)}");
            }
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