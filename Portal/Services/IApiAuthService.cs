using System.Threading;
using System.Threading.Tasks;
using Shared.DTOs;

namespace Portal.Services;

public interface IApiAuthService
{
    Task<TokenResponseDTO?> LoginAsync(LoginDTO dto, CancellationToken cancellationToken = default);
    Task<TokenResponseDTO?> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<bool> RevokeAsync(string refreshToken, CancellationToken cancellationToken = default);
}