using Plugin.Fingerprint;
using Plugin.Fingerprint.Abstractions;

namespace MobileApp.Services
{
    public class AuthenticationRequest
    {
        public string Title { get; set; } = "Authentication Required";
        public string Reason { get; set; } = "Please authenticate to continue";
        public bool AllowAlternativeAuthentication { get; set; } = true;
    }

    public class AuthenticationResult
    {
        public bool Authenticated { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public static class BiometricAuthentication
    {
        public static async Task<AuthenticationResult> AuthenticateAsync(AuthenticationRequest request)
        {
            try
            {
                // Check if biometric authentication is available on the device
                var availability = await CrossFingerprint.Current.GetAvailabilityAsync();
                
                if (availability != FingerprintAvailability.Available)
                {
                    return new AuthenticationResult
                    {
                        Authenticated = false,
                        ErrorMessage = GetAvailabilityMessage(availability)
                    };
                }

                // Configure authentication request
                var authRequest = new AuthenticationRequestConfiguration(request.Title, request.Reason)
                {
                    AllowAlternativeAuthentication = request.AllowAlternativeAuthentication,
                    CancelTitle = "Cancel",
                    FallbackTitle = "Use Password"
                };

                // Perform biometric authentication
                var result = await CrossFingerprint.Current.AuthenticateAsync(authRequest);
                
                return new AuthenticationResult
                {
                    Authenticated = result.Authenticated,
                    ErrorMessage = result.Authenticated ? string.Empty : result.ErrorMessage
                };
            }
            catch (Exception ex)
            {
                return new AuthenticationResult
                {
                    Authenticated = false,
                    ErrorMessage = $"Authentication failed: {ex.Message}"
                };
            }
        }

        public static async Task<bool> IsBiometricAvailableAsync()
        {
            try
            {
                var availability = await CrossFingerprint.Current.GetAvailabilityAsync();
                return availability == FingerprintAvailability.Available;
            }
            catch
            {
                return false;
            }
        }

        private static string GetAvailabilityMessage(FingerprintAvailability availability)
        {
            return availability switch
            {
                FingerprintAvailability.NoImplementation => "Biometric authentication is not implemented on this platform",
                FingerprintAvailability.NoApi => "Biometric API is not available on this device",
                FingerprintAvailability.NoPermission => "Permission to use biometric authentication has not been granted",
                FingerprintAvailability.NoSensor => "No biometric sensor found on this device",
                FingerprintAvailability.NoFingerprint => "No biometric data enrolled. Please set up biometric authentication in device settings",
                FingerprintAvailability.Unknown => "Biometric authentication availability is unknown",
                FingerprintAvailability.Denied => "Biometric authentication has been denied",
                _ => "Biometric authentication is not available"
            };
        }
    }
}