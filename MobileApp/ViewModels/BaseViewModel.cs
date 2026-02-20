using CommunityToolkit.Mvvm.ComponentModel;
using MobileApp.Services;
using MobileApp.Views;

namespace MobileApp.ViewModels
{
    /// <summary>
    /// Base class for all ViewModels providing common functionality
    /// </summary>
    public partial class BaseViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotBusy))]
        private bool isBusy;

        [ObservableProperty]
        private string title = string.Empty;

        public bool IsNotBusy => !IsBusy;

        /// <summary>
        /// Validates the current access token and redirects to login if expired
        /// </summary>
        /// <param name="authService">The authentication service</param>
        /// <returns>True if token is valid, false if expired</returns>
        protected async Task<bool> ValidateTokenAsync(IAuthService authService)
        {
            try
            {
                // Check if tokens exist
                var (accessToken, refreshToken) = await authService.GetStoredTokensAsync();
                
                if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
                {
                    // No tokens, redirect to login
                    await NavigateToLoginAsync();
                    return false;
                }

                // Check if token is expired
                if (await authService.IsTokenExpiredAsync())
                {
                    // Try to refresh the token
                    var (success, newTokens, message) = await authService.RefreshTokenAsync();
                    
                    if (!success || newTokens == null)
                    {
                        // Refresh failed, clear tokens and redirect to login
                        authService.ClearTokens();
                        await NavigateToLoginAsync();
                        return false;
                    }
                    
                    // Token refreshed successfully
                    return true;
                }

                // Token is valid
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Token validation error: {ex.Message}");
                // On error, redirect to login for safety
                await NavigateToLoginAsync();
                return false;
            }
        }

        /// <summary>
        /// Navigate to login page using the same pattern as the rest of the app
        /// </summary>
        private async Task NavigateToLoginAsync()
        {
            try
            {
                await Shell.Current.GoToAsync($"/{nameof(LoginPage)}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Navigation to login failed: {ex.Message}");
            }
        }
    }
}