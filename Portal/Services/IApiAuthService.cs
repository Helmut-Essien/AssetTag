using AssetTag.DTOs;
using Shared.DTOs;

namespace Portal.Services;

public interface IApiAuthService
{
    Task<TokenResponseDTO?> LoginAsync(LoginDTO dto, CancellationToken cancellationToken = default);
}