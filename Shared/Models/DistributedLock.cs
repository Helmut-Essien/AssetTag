namespace Shared.Models;

/// <summary>
/// ARCHITECTURAL FIX A1: Database record for distributed locking
/// Used to coordinate sync operations across multiple devices/servers
/// </summary>
public class DistributedLock
{
    public int Id { get; set; }
    
    /// <summary>
    /// Unique key identifying the lock (e.g., "sync:user:123")
    /// </summary>
    public string LockKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Unique identifier for the lock owner
    /// </summary>
    public string OwnerId { get; set; } = string.Empty;
    
    /// <summary>
    /// When the lock was acquired
    /// </summary>
    public DateTime AcquiredAt { get; set; }
    
    /// <summary>
    /// When the lock expires (for automatic cleanup of stale locks)
    /// </summary>
    public DateTime ExpiresAt { get; set; }
}
