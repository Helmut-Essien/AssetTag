using Shared.DTOs;

namespace MobileApp.Services
{
    public interface IAuthService
    {
        Task<(bool Success, TokenResponseDTO? Token, string Message)> LoginAsync(string email, string password);
        Task<bool> IsConnectedToInternet();
        void SaveTokens(string accessToken, string refreshToken);
        (string? AccessToken, string? RefreshToken) GetStoredTokens();
        void ClearTokens();
        Task<(bool Success, string Message)> ForgotPasswordAsync(string email);
        Task<(bool Success, TokenResponseDTO? Token, string Message)> RefreshTokenAsync();
        Task<bool> IsTokenExpiredAsync();
        Task<bool> AuthenticateWithBiometricsAsync(string reason);
        Task EnableBiometricAuthenticationAsync();
        Task DisableBiometricAuthenticationAsync();
        Task<bool> IsBiometricEnabledAsync();
    }
}