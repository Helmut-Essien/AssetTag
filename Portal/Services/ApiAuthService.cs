using System.Net.Http.Json;
using AssetTag.DTOs;
using Shared.DTOs;

namespace Portal.Services;

public sealed class ApiAuthService : IApiAuthService
{
    private readonly IHttpClientFactory _http;

    public ApiAuthService(IHttpClientFactory http) => _http = http;

    public async Task<TokenResponseDTO?> LoginAsync(LoginDTO dto, CancellationToken cancellationToken = default)
    {
        var client = _http.CreateClient("AssetTagApi");
        using var res = await client.PostAsJsonAsync("api/auth/login", dto, cancellationToken);
        if (!res.IsSuccessStatusCode) return null;
        var token = await res.Content.ReadFromJsonAsync<TokenResponseDTO>(cancellationToken: cancellationToken);
        return token;
    }
}
   