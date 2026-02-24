using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileApp.Services;
using System.Windows.Input;

namespace MobileApp.ViewModels
{
    public partial class LoginViewModel : BaseViewModel
    {
        private readonly IAuthService _authService;

        [ObservableProperty]
        private string email = string.Empty;

        [ObservableProperty]
        private string password = string.Empty;

        [ObservableProperty]
        private bool isPasswordVisible = false;

        [ObservableProperty]
        private string errorMessage = string.Empty;

        [ObservableProperty]
        private bool hasError = false;

        [ObservableProperty]
        private bool biometricAvailable = false;

        [ObservableProperty]
        private bool biometricEnabled = false;

        public LoginViewModel(IAuthService authService)
        {
            _authService = authService;
            Title = "Login";
            
            // Check biometric availability on initialization
            _ = CheckBiometricAvailabilityAsync();
        }

        private async Task CheckBiometricAvailabilityAsync()
        {
            BiometricAvailable = await BiometricAuthentication.IsBiometricAvailableAsync();
            BiometricEnabled = await _authService.IsBiometricEnabledAsync();
        }

        [RelayCommand]
        private void TogglePasswordVisibility()
        {
            IsPasswordVisible = !IsPasswordVisible;
        }

        [RelayCommand]
        private async Task LoginAsync()
        {
            // Clear previous errors
            HasError = false;
            ErrorMessage = string.Empty;

            // Validate inputs
            if (string.IsNullOrWhiteSpace(Email))
            {
                ShowError("Please enter your email address");
                return;
            }

            if (string.IsNullOrWhiteSpace(Password))
            {
                ShowError("Please enter your password");
                return;
            }

            if (!IsValidEmail(Email))
            {
                ShowError("Please enter a valid email address");
                return;
            }

            IsBusy = true;

            try
            {
                var (success, token, message) = await _authService.LoginAsync(Email, Password);

                if (success)
                {
                    // Always store credentials temporarily for potential biometric enablement
                    // This allows users to enable biometric without re-entering credentials
                    await _authService.EnableBiometricAuthenticationAsync(Email, Password);
                    
                    // If biometric was not previously enabled, disable it (just store credentials)
                    if (!BiometricEnabled)
                    {
                        await SecureStorage.SetAsync("biometric_enabled", "false");
                    }
                    
                    // Navigate to main tabs
                    if (Shell.Current is AppShell appShell)
                    {
                        await appShell.ShowMainTabsAsync();
                    }
                }
                else
                {
                    ShowError(message);
                }
            }
            catch (Exception ex)
            {
                ShowError($"An unexpected error occurred: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task BiometricLoginAsync()
        {
            if (!BiometricAvailable)
            {
                await Shell.Current.DisplayAlert("Not Available",
                    "Biometric authentication is not available on this device.",
                    "OK");
                return;
            }

            if (!BiometricEnabled)
            {
                await Shell.Current.DisplayAlert("Not Enabled",
                    "Please login with your email and password first, then enable biometric authentication.",
                    "OK");
                return;
            }

            IsBusy = true;

            try
            {
                var (success, token, message) = await _authService.BiometricLoginAsync();

                if (success)
                {
                    // Navigate to main tabs
                    if (Shell.Current is AppShell appShell)
                    {
                        await appShell.ShowMainTabsAsync();
                    }
                }
                else
                {
                    ShowError(message);
                }
            }
            catch (Exception ex)
            {
                ShowError($"Authentication error: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task ToggleBiometricAsync()
        {
            if (!BiometricAvailable)
            {
                await Shell.Current.DisplayAlert("Not Available",
                    "Biometric authentication is not available on this device.",
                    "OK");
                return;
            }

            if (BiometricEnabled)
            {
                // Disable biometric
                await _authService.DisableBiometricAuthenticationAsync();
                BiometricEnabled = false;
                await Shell.Current.DisplayAlert("Disabled",
                    "Biometric authentication has been disabled.",
                    "OK");
            }
            else
            {
                // Enable biometric - require credentials first
                if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
                {
                    await Shell.Current.DisplayAlert("Credentials Required",
                        "Please enter your email and password first to enable biometric authentication.",
                        "OK");
                    return;
                }

                // Authenticate with biometrics first
                var authenticated = await _authService.AuthenticateWithBiometricsAsync(
                    "Authenticate to enable biometric login");

                if (authenticated)
                {
                    await _authService.EnableBiometricAuthenticationAsync(Email, Password);
                    BiometricEnabled = true;
                    await Shell.Current.DisplayAlert("Enabled",
                        "Biometric authentication has been enabled. You can now login using biometrics even if your session expires.",
                        "OK");
                }
                else
                {
                    await Shell.Current.DisplayAlert("Failed",
                        "Biometric authentication failed. Please try again.",
                        "OK");
                }
            }
        }

        private void ShowError(string message)
        {
            ErrorMessage = message;
            HasError = true;
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        [RelayCommand]
        private async Task ForgotPasswordAsync()
        {
            // Prompt user for email
            string email = await Shell.Current.DisplayPromptAsync(
                "Forgot Password",
                "Enter your email address:",
                "Send Reset Link",
                "Cancel",
                "Email address",
                keyboard: Keyboard.Email);

            if (string.IsNullOrWhiteSpace(email))
            {
                return; // User cancelled
            }

            // Validate email format
            if (!IsValidEmail(email))
            {
                await Shell.Current.DisplayAlert("Invalid Email",
                    "Please enter a valid email address.",
                    "OK");
                return;
            }

            IsBusy = true;

            try
            {
                var (success, message) = await _authService.ForgotPasswordAsync(email);

                await Shell.Current.DisplayAlert(
                    success ? "Reset Link Sent" : "Error",
                    message,
                    "OK");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error",
                    $"An unexpected error occurred: {ex.Message}",
                    "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}