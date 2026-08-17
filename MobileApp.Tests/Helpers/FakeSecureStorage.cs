using MobileApp.Services;

namespace MobileApp.Tests.Helpers;

public sealed class FakeSecureStorage : ISecureStorageService
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    public Task SetAsync(string key, string value)
    {
        _values[key] = value;
        return Task.CompletedTask;
    }

    public Task<string?> GetAsync(string key)
    {
        _values.TryGetValue(key, out var value);
        return Task.FromResult<string?>(value);
    }

    public bool Remove(string key) => _values.Remove(key);

    public bool Contains(string key) => _values.ContainsKey(key);
}
