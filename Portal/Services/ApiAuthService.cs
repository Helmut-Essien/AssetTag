using System.Net.Http.Json;

using Shared.DTOs;

namespace Portal.Services;

public sealed class ApiAuthService : IApiAuthService
{
    private readonly IHttpClientFactory _http;

    public ApiAuthService(IHttpClientFactory http) => _http = http;

    public async Task<TokenResponseDTO?> LoginAsync(LoginDTO dto, CancellationToken cancellationToken = default)
    {
        var client = _http.CreateClient("AssetTagApi");
        using var res = await client.PostAsJsonAsync("api/auth/login", dto, cancellationToken).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadFromJsonAsync<TokenResponseDTO>(cancellationToken: cancellationToken).ConfigureAwait(false);
        //var token = await res.Content.ReadFromJsonAsync<TokenResponseDTO>(cancellationToken: cancellationToken);
        //return token;
    }

    public async Task<TokenResponseDTO?> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var client = _http.CreateClient("AssetTagApi");
        var req = new TokenResponseDTO(string.Empty, refreshToken);
        using var res = await client.PostAsJsonAsync("api/auth/refresh-token", req, cancellationToken).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadFromJsonAsync<TokenResponseDTO>(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> RegisterAsync(RegisterDTO registerDto, CancellationToken cancellationToken = default)
    {
        var client = _http.CreateClient("AssetTagApi");
        using var res = await client.PostAsJsonAsync("api/auth/register", registerDto, cancellationToken).ConfigureAwait(false);
        return res.IsSuccessStatusCode;
    }

    public async Task<bool> RevokeAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var client = _http.CreateClient("AssetTagApi");
        var req = new TokenResponseDTO(string.Empty, refreshToken);
        using var res = await client.PostAsJsonAsync("api/auth/revoke", req, cancellationToken).ConfigureAwait(false);
        return res.IsSuccessStatusCode;
    }
}
   