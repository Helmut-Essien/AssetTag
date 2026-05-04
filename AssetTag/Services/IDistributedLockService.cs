namespace AssetTag.Services;

/// <summary>
/// ARCHITECTURAL FIX A1: Distributed lock service for multi-device sync coordination
/// Prevents race conditions when multiple devices sync simultaneously
/// </summary>
public interface IDistributedLockService
{
    /// <summary>
    /// Attempts to acquire a distributed lock with the specified key
    /// </summary>
    /// <param name="lockKey">Unique identifier for the lock</param>
    /// <param name="timeout">How long to wait for the lock before giving up</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if lock was acquired, false otherwise</returns>
    Task<bool> TryAcquireAsync(string lockKey, TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases a previously acquired lock
    /// </summary>
    /// <param name="lockKey">Unique identifier for the lock</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ReleaseAsync(string lockKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a lock is currently held
    /// </summary>
    /// <param name="lockKey">Unique identifier for the lock</param>
    /// <returns>True if lock is held, false otherwise</returns>
    Task<bool> IsLockedAsync(string lockKey);
}
