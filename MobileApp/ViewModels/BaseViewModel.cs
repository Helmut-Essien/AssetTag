using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using MobileApp.Services;

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
        /// Validates the current access token before an API call.
        /// Connectivity failures keep the stored session so offline SQLite still works.
        /// Invalid/revoked refresh tokens clear the session and show login.
        /// </summary>
        protected async Task<bool> ValidateTokenAsync(IAuthService authService)
        {
            try
            {
                var (accessToken, refreshToken) = await authService.GetStoredTokensAsync();
                
                if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
                {
                    await NavigateToLoginAsync();
                    return false;
                }

                if (await authService.IsTokenExpiredAsync())
                {
                    var refresh = await authService.RefreshTokenAsync();
                    if (refresh.Succeeded && refresh.Token != null)
                        return true;

                    if (refresh.IsTransientFailure)
                        return false;

                    await NavigateToLoginAsync();
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Token validation error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Background session check. Returns false only when the user must sign in again.
        /// Offline / timeout while a session is stored returns true so local data stays available.
        /// </summary>
        protected async Task<bool> TryValidateTokenSilentAsync(IAuthService authService)
        {
            try
            {
                var (accessToken, refreshToken) = await authService.GetStoredTokensAsync();

                if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
                {
                    return false;
                }

                if (await authService.IsTokenExpiredAsync())
                {
                    var refresh = await authService.RefreshTokenAsync();
                    if (refresh.Succeeded)
                        return true;

                    return refresh.IsTransientFailure;
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Silent token validation error: {ex.Message}");
                return true;
            }
        }

        /// <summary>
        /// Replace the tab session with the login page. Do not GoToAsync("/LoginPage").
        /// </summary>
        protected async Task NavigateToLoginAsync()
        {
            try
            {
                if (Shell.Current is AppShell appShell)
                {
                    await appShell.ShowLoginAsync();
                    return;
                }

                var navigation = Application.Current?.Handler?.MauiContext?.Services
                    .GetService<INavigationService>();
                if (navigation != null)
                    await navigation.ShowLoginAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Navigation to login failed: {ex.Message}");
            }
        }
    }
}
