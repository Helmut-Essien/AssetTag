namespace MobileApp.Services;

public sealed class MauiSecureStorageService : ISecureStorageService
{
    public Task SetAsync(string key, string value) => SecureStorage.SetAsync(key, value);

    public Task<string?> GetAsync(string key) => SecureStorage.GetAsync(key);

    public bool Remove(string key) => SecureStorage.Remove(key);
}
