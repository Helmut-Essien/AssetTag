namespace MobileApp.Services;

/// <summary>
/// Abstraction over platform secure storage so auth can be unit-tested.
/// </summary>
public interface ISecureStorageService
{
    Task SetAsync(string key, string value);
    Task<string?> GetAsync(string key);
    bool Remove(string key);
}
