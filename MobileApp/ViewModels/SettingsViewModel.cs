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
        private readonly INavigationService _navigationService;
        private readonly IVersionCheckService _versionCheckService;

        [ObservableProperty]
        private bool biometricAvailable;

        [ObservableProperty]
        private bool biometricEnabled;

        [ObservableProperty]
        private string biometricStatusText = "Checking...";

        [ObservableProperty]
        private string appVersion = "Loading...";

        private bool _isInitializing = false;

        public SettingsViewModel(
            IAuthService authService,
            ISyncService syncService,
            INavigationService navigationService,
            IVersionCheckService versionCheckService)
        {
            _authService = authService;
            _syncService = syncService;
            _navigationService = navigationService;
            _versionCheckService = versionCheckService;
            Title = "Settings";
            
            // Set app version
            AppVersion = $"Version {_versionCheckService.GetCurrentVersion()}";
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
                _isInitializing = true;
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
            finally
            {
                _isInitializing = false;
            }
        }

        /// <summary>
        /// Handle biometric toggle change
        /// </summary>
        partial void OnBiometricEnabledChanged(bool value)
        {
            // Skip if we're initializing (loading saved state)
            if (_isInitializing)
                return;

            // Toggle biometric when switch is changed by user
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

                    // If no stored biometric credentials, try to use current session credentials
                    if (string.IsNullOrEmpty(storedEmail) || string.IsNullOrEmpty(storedPassword))
                    {
                        var (sessionEmail, sessionPassword) = await _authService.GetCurrentSessionCredentialsAsync();
                        
                        if (!string.IsNullOrEmpty(sessionEmail) && !string.IsNullOrEmpty(sessionPassword))
                        {
                            // Use session credentials and store them for biometric
                            storedEmail = sessionEmail;
                            storedPassword = sessionPassword;
                            // Pre-store credentials so they're available for biometric login
                            await _authService.EnableBiometricAuthenticationAsync(storedEmail, storedPassword);
                        }
                        else
                        {
                            // No credentials available - this shouldn't happen if user is logged in
                            // Show error and revert toggle
                            BiometricEnabled = false;
                            await _navigationService.DisplayAlertAsync(
                                "Error",
                                "Unable to enable biometric login. Please log out and log back in, then try again.",
                                "OK");
                            return;
                        }
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
                        // Enable biometric (credentials already stored above if needed)
                        await _authService.EnableBiometricAuthenticationAsync(storedEmail, storedPassword);
                        BiometricStatusText = "Use fingerprint or face to login";
                        await _navigationService.DisplayAlertAsync(
                            "Success",
                            "Biometric authentication has been enabled!",
                            "OK");
                    }
                    else
                    {
                        BiometricEnabled = false;
                        await _navigationService.DisplayAlertAsync(
                            "Failed",
                            "Biometric authentication failed. Please try again.",
                            "OK");
                    }
                }
                else
                {
                    // Disable biometric
                    var confirm = await _navigationService.DisplayConfirmAsync(
                        "Disable Biometric Login",
                        "Are you sure you want to disable biometric authentication?",
                        "Yes",
                        "No");

                    if (confirm)
                    {
                        await _authService.DisableBiometricAuthenticationAsync();
                        BiometricStatusText = "Enable for quick login";
                        await _navigationService.DisplayAlertAsync(
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
                await _navigationService.DisplayAlertAsync("Error", $"An error occurred: {ex.Message}", "OK");
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
                var confirm = await _navigationService.DisplayConfirmAsync(
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
                var doubleConfirm = await _navigationService.DisplayConfirmAsync(
                    "Are You Sure?",
                    "This is your last chance to cancel. All local data will be permanently deleted.",
                    "Delete Everything",
                    "Cancel");

                if (!doubleConfirm)
                    return;

                IsBusy = true;

                // Clear all local data
                await _syncService.ClearAllLocalDataAsync();

                await _navigationService.DisplayAlertAsync(
                    "Success",
                    "All local data has been cleared. Pull from server to restore data.",
                    "OK");

                // Go back to main page
                await _navigationService.GoBackAsync();
            }
            catch (Exception ex)
            {
                await _navigationService.DisplayAlertAsync("Error", $"Failed to clear data: {ex.Message}", "OK");
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
                var confirm = await _navigationService.DisplayConfirmAsync(
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
                
                // Perform full sync immediately (enqueue to central sync queue)
                var (success, message) = await _syncService.EnqueueFullSyncAsync();

                // Show result
                await _navigationService.DisplayAlertAsync(
                    success ? "Success" : "Sync Error",
                    success ? "Sync state reset and full sync completed successfully!" : $"Reset completed but sync failed: {message}",
                    "OK");
            }
            catch (Exception ex)
            {
                await _navigationService.DisplayAlertAsync("Error", $"Reset failed: {ex.Message}", "OK");
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
                var confirm = await _navigationService.DisplayConfirmAsync(
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
                    // Navigate back to login page using navigation service
                    await _navigationService.ShowLoginAsync();
                }
                else
                {
                    await _navigationService.DisplayAlertAsync("Logout", message, "OK");
                }
            }
            catch (Exception ex)
            {
                await _navigationService.DisplayAlertAsync("Error", $"Logout failed: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Go back to previous page
        /// </summary>
        /// <summary>
        /// Check for app updates manually
        /// </summary>
        [RelayCommand]
        private async Task CheckForUpdatesAsync()
        {
            if (IsBusy) return;

            try
            {
                IsBusy = true;

                var (updateAvailable, versionInfo, message) = await _versionCheckService.CheckForUpdateAsync();

                if (updateAvailable && versionInfo != null)
                {
                    var currentVersion = _versionCheckService.GetCurrentVersion();
                    var isMandatory = versionInfo.IsMandatory;

                    var title = isMandatory ? "Critical Update Required" : "Update Available";
                    var updateMessage = isMandatory
                        ? $"A critical update to version {versionInfo.LatestVersion} is required.\n\nCurrent version: {currentVersion}"
                        : $"Version {versionInfo.LatestVersion} is now available!\n\nCurrent version: {currentVersion}";

                    // Add features if available
                    if (versionInfo.Features.Length > 0)
                    {
                        updateMessage += "\n\nWhat's new:\n" + string.Join("\n", versionInfo.Features.Select(f => $"• {f}"));
                    }

                    var updateButton = isMandatory ? "Update Now" : "Update";
                    var cancelButton = isMandatory ? null : "Later";

                    var result = await _navigationService.DisplayConfirmAsync(
                        title,
                        updateMessage,
                        updateButton,
                        cancelButton ?? "Cancel");

                    if (result)
                    {
                        // User chose to update
                        await DownloadAndInstallUpdateAsync(versionInfo);
                    }
                }
                else
                {
                    await _navigationService.DisplayAlertAsync(
                        "No Updates",
                        $"You're running the latest version ({_versionCheckService.GetCurrentVersion()})!",
                        "OK");
                }
            }
            catch (Exception ex)
            {
                await _navigationService.DisplayAlertAsync(
                    "Error",
                    $"Failed to check for updates: {ex.Message}",
                    "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Download and install the update
        /// </summary>
        private async Task DownloadAndInstallUpdateAsync(Shared.DTOs.VersionCheckResponseDto versionInfo)
        {
            try
            {
                await _navigationService.DisplayAlertAsync(
                    "Downloading",
                    "The update is being downloaded. This may take a few moments...",
                    "OK");

                var progress = new Progress<double>(percent =>
                {
                    var percentInt = (int)(percent * 100);
                    System.Diagnostics.Debug.WriteLine($"Download progress: {percentInt}%");
                });

                var (success, downloadMessage) = await _versionCheckService.DownloadAndInstallUpdateAsync(versionInfo, progress);

                if (success)
                {
                    await _navigationService.DisplayAlertAsync(
                        "Update Ready",
                        "The update has been downloaded. Please complete the installation.",
                        "OK");
                }
                else
                {
                    await _navigationService.DisplayAlertAsync(
                        "Update Failed",
                        $"Failed to download update: {downloadMessage}",
                        "OK");
                }
            }
            catch (Exception ex)
            {
                await _navigationService.DisplayAlertAsync(
                    "Error",
                    $"An error occurred while downloading the update: {ex.Message}",
                    "OK");
            }
        }

        /// <summary>
        /// Go back to previous page
        /// </summary>
        [RelayCommand]
        private async Task GoBackAsync()
        {
            await _navigationService.GoBackAsync();
        }
    }
}