using System.Threading;
using System.Threading.Tasks;
using Shared.DTOs;

namespace Portal.Services;

public interface IApiAuthService
{
    Task<TokenResponseDTO?> LoginAsync(LoginDTO dto, CancellationToken cancellationToken = default);
    Task<TokenResponseDTO?> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<bool> RegisterAsync(RegisterDTO registerDto, CancellationToken cancellationToken = default);
    //Task RegisterAsync(RegisterDTO registerDto);
    Task<bool> RevokeAsync(string refreshToken, CancellationToken cancellationToken = default);

    // Add these methods
    Task<ForgotPasswordResponse?> ForgotPasswordAsync(ForgotPasswordDTO dto, CancellationToken cancellationToken = default);
    Task<ResetPasswordResponse?> ResetPasswordAsync(ResetPasswordDTO dto, CancellationToken cancellationToken = default);
}