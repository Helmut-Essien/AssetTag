namespace MobileApp.Services;

/// <summary>
/// Abstraction over platform connectivity so auth and sync can be unit-tested.
/// Does not ping the API — that stays in AuthService so health checks
/// can use a handler-free HttpClient.
/// </summary>
public interface INetworkAccessService
{
    bool HasInternetAccess { get; }
}
