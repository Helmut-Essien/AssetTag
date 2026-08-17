using Shared.DTOs;

namespace MobileApp.Services;

/// <summary>
/// Outcome of an access-token refresh. Transient failures (offline, timeout)
/// must not clear the stored session — local SQLite should keep working.
/// </summary>
public readonly record struct TokenRefreshResult(
    bool Succeeded,
    TokenResponseDTO? Token,
    string Message,
    bool IsTransientFailure)
{
    public static TokenRefreshResult Ok(TokenResponseDTO token, string message) =>
        new(true, token, message, false);

    public static TokenRefreshResult Transient(string message) =>
        new(false, null, message, true);

    public static TokenRefreshResult InvalidSession(string message) =>
        new(false, null, message, false);
}
