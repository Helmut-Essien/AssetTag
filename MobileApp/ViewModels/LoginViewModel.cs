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
                    // Navigate to main page
                    await Shell.Current.GoToAsync("//MainPage");
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

            // Check if user has stored credentials
            var (accessToken, refreshToken) = _authService.GetStoredTokens();
            
            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
            {
                await Shell.Current.DisplayAlert("No Saved Session", 
                    "Please login with your email and password first to enable biometric login.", 
                    "OK");
                return;
            }

            IsBusy = true;

            try
            {
                var authenticated = await _authService.AuthenticateWithBiometricsAsync(
                    "Authenticate to access AssetTag");

                if (authenticated)
                {
                    // Check if token is still valid
                    if (await _authService.IsTokenExpiredAsync())
                    {
                        // Try to refresh the token
                        var (success, _, message) = await _authService.RefreshTokenAsync();
                        
                        if (!success)
                        {
                            ShowError("Session expired. Please login with your credentials.");
                            _authService.ClearTokens();
                            return;
                        }
                    }

                    // Navigate to main page
                    await Shell.Current.GoToAsync("//MainPage");
                }
                else
                {
                    ShowError("Biometric authentication failed. Please try again.");
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
                // Enable biometric - require authentication first
                var authenticated = await _authService.AuthenticateWithBiometricsAsync(
                    "Authenticate to enable biometric login");

                if (authenticated)
                {
                    await _authService.EnableBiometricAuthenticationAsync();
                    BiometricEnabled = true;
                    await Shell.Current.DisplayAlert("Enabled", 
                        "Biometric authentication has been enabled for future logins.", 
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