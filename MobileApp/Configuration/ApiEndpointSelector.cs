using Microsoft.Extensions.Options;

namespace MobileApp.Configuration;

/// <summary>
/// Current API base URL. Auth ping updates this so AuthClient and ApiClient
/// follow the fallback host instead of only pinging it.
/// </summary>
public sealed class ApiEndpointSelector
{
    private readonly object _lock = new();
    private string _baseUrl;

    public ApiEndpointSelector(IOptions<ApiSettings> settings)
    {
        PrimaryApiUrl = settings.Value.PrimaryApiUrl;
        _baseUrl = settings.Value.PrimaryApiUrl;
    }

    public string PrimaryApiUrl { get; }

    public string BaseUrl
    {
        get
        {
            lock (_lock)
                return _baseUrl;
        }
    }

    public void SetBaseUrl(string url)
    {
        lock (_lock)
            _baseUrl = url;
    }
}
