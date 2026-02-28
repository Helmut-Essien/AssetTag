using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileApp.Services;

namespace MobileApp.ViewModels
{
    /// <summary>
    /// ViewModel for the Settings page
    /// </summary>
    public partial class SettingsViewModel : BaseViewModel
    {
        private readonly IAuthService _authService;
        private readonly ISyncService _syncService;

        [ObservableProperty]
        private bool biometricAvailable;

        [ObservableProperty]
        private bool biometricEnabled;

        [ObservableProperty]
        private string biometricStatusText = "Checking...";

        public SettingsViewModel(
            IAuthService authService,
            ISyncService syncService)
        {
            _authService = authService;
            _syncService = syncService;
            Title = "Settings";
        }

        /// <summary>
        /// Load settings when page appears
        /// </summary>
        public async Task InitializeAsync()
        {
            await LoadBiometricStatusAsync();
        }

        /// <summary>
        /// Load biometric authentication status
        /// </summary>
        private async Task LoadBiometricStatusAsync()
        {
            try
            {
                BiometricAvailable = await BiometricAuthentication.IsBiometricAvailableAsync();
                BiometricEnabled = await _authService.IsBiometricEnabledAsync();

                if (BiometricAvailable)
                {
                    BiometricStatusText = BiometricEnabled
                        ? "Use fingerprint or face to login"
                        : "Enable for quick login";
                }
                else
                {
                    BiometricStatusText = "Not available on this device";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading biometric status: {ex.Message}");
                BiometricAvailable = false;
                BiometricStatusText = "Error checking availability";
            }
        }

        /// <summary>
        /// Handle biometric toggle change
        /// </summary>
        partial void OnBiometricEnabledChanged(bool value)
        {
            // Toggle biometric when switch is changed
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await ToggleBiometricAsync(value);
            });
        }

        /// <summary>
        /// Toggle biometric authentication on/off
        /// </summary>
        private async Task ToggleBiometricAsync(bool enable)
        {
            if (IsBusy) return;

            try
            {
                IsBusy = true;

                if (enable)
                {
                    // Enable biometric
                    var (storedEmail, storedPassword) = await _authService.GetStoredCredentialsAsync();

                    // If no stored credentials, ask for them
                    if (string.IsNullOrEmpty(storedEmail) || string.IsNullOrEmpty(storedPassword))
                    {
                        var email = await Shell.Current.DisplayPromptAsync(
                            "Enable Biometric Login",
                            "Enter your email address:",
                            "Next",
                            "Cancel",
                            keyboard: Keyboard.Email);

                        if (string.IsNullOrWhiteSpace(email))
                        {
                            BiometricEnabled = false;
                            return;
                        }

                        var password = await Shell.Current.DisplayPromptAsync(
                            "Enable Biometric Login",
                            "Enter your password:",
                            "Enable",
                            "Cancel",
                            keyboard: Keyboard.Default);

                        if (string.IsNullOrWhiteSpace(password))
                        {
                            BiometricEnabled = false;
                            return;
                        }

                        // Verify credentials
                        var (loginSuccess, _, loginMessage) = await _authService.LoginAsync(email, password);

                        if (!loginSuccess)
                        {
                            await Shell.Current.DisplayAlert(
                                "Error",
                                $"Invalid credentials: {loginMessage}",
                                "OK");
                            BiometricEnabled = false;
                            return;
                        }

                        storedEmail = email;
                        storedPassword = password;
                    }

                    // Authenticate with biometrics to confirm
                    var authRequest = new AuthenticationRequest
                    {
                        Title = "Enable Biometric Login",
                        Reason = "Scan your fingerprint or face to enable biometric login",
                        AllowAlternativeAuthentication = true
                    };

                    var result = await BiometricAuthentication.AuthenticateAsync(authRequest);

                    if (result.Authenticated)
                    {
                        await _authService.EnableBiometricAuthenticationAsync(storedEmail, storedPassword);
                        BiometricStatusText = "Use fingerprint or face to login";
                        await Shell.Current.DisplayAlert(
                            "Success",
                            "Biometric authentication has been enabled!",
                            "OK");
                    }
                    else
                    {
                        BiometricEnabled = false;
                        await Shell.Current.DisplayAlert(
                            "Failed",
                            "Biometric authentication failed. Please try again.",
                            "OK");
                    }
                }
                else
                {
                    // Disable biometric
                    var confirm = await Shell.Current.DisplayAlert(
                        "Disable Biometric Login",
                        "Are you sure you want to disable biometric authentication?",
                        "Yes",
                        "No");

                    if (confirm)
                    {
                        await _authService.DisableBiometricAuthenticationAsync();
                        BiometricStatusText = "Enable for quick login";
                        await Shell.Current.DisplayAlert(
                            "Success",
                            "Biometric authentication has been disabled.",
                            "OK");
                    }
                    else
                    {
                        BiometricEnabled = true;
                    }
                }
            }
            catch (Exception ex)
            {
                BiometricEnabled = !enable;
                await Shell.Current.DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Clear all local data from the device
        /// </summary>
        [RelayCommand]
        private async Task ClearLocalDataAsync()
        {
            if (IsBusy) return;

            try
            {
                var confirm = await Shell.Current.DisplayAlert(
                    "Clear Local Data",
                    "⚠️ WARNING: This will permanently delete ALL locally stored data including:\n\n" +
                    "• All assets\n" +
                    "• Categories, locations, departments\n" +
                    "• Pending sync queue\n\n" +
                    "This action cannot be undone. You will need to sync from the server to restore data.\n\n" +
                    "Continue?",
                    "Yes, Clear All Data",
                    "Cancel");

                if (!confirm)
                    return;

                // Double confirmation for safety
                var doubleConfirm = await Shell.Current.DisplayAlert(
                    "Are You Sure?",
                    "This is your last chance to cancel. All local data will be permanently deleted.",
                    "Delete Everything",
                    "Cancel");

                if (!doubleConfirm)
                    return;

                IsBusy = true;

                // Clear all local data
                await _syncService.ClearAllLocalDataAsync();

                await Shell.Current.DisplayAlert(
                    "Success",
                    "All local data has been cleared. Pull from server to restore data.",
                    "OK");

                // Go back to main page
                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", $"Failed to clear data: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Reset sync state to force full re-sync
        /// </summary>
        [RelayCommand]
        private async Task ResetSyncStateAsync()
        {
            if (IsBusy) return;

            try
            {
                var confirm = await Shell.Current.DisplayAlert(
                    "Reset Sync State",
                    "This will reset the sync timestamp and fetch ALL data from the server on next sync. " +
                    "This is useful if you're missing data. Continue?",
                    "Yes, Reset",
                    "Cancel");

                if (!confirm)
                    return;

                IsBusy = true;

                // Reset sync state
                await _syncService.ResetSyncStateAsync();

                // Perform full sync immediately
                var (success, message) = await _syncService.FullSyncAsync();

                // Show result
                await Shell.Current.DisplayAlert(
                    success ? "Success" : "Sync Error",
                    success ? "Sync state reset and full sync completed successfully!" : $"Reset completed but sync failed: {message}",
                    "OK");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", $"Reset failed: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Logout the current user
        /// </summary>
        [RelayCommand]
        private async Task LogoutAsync()
        {
            try
            {
                var confirm = await Shell.Current.DisplayAlert(
                    "Logout",
                    "Are you sure you want to logout?",
                    "Yes",
                    "No");

                if (!confirm)
                    return;

                IsBusy = true;

                var (success, message) = await _authService.LogoutAsync();

                if (success)
                {
                    // Navigate back to login page
                    if (Shell.Current is AppShell appShell)
                    {
                        await appShell.ShowLoginAsync();
                    }
                }
                else
                {
                    await Shell.Current.DisplayAlert("Logout", message, "OK");
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", $"Logout failed: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Go back to previous page
        /// </summary>
        [RelayCommand]
        private async Task GoBackAsync()
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}