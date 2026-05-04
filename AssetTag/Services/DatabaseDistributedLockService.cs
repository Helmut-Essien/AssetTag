using AssetTag.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AssetTag.Services;

/// <summary>
/// ARCHITECTURAL FIX A1: Database-based distributed lock implementation
/// Uses database records to coordinate locks across multiple server instances
/// </summary>
public class DatabaseDistributedLockService : IDistributedLockService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DatabaseDistributedLockService> _logger;
    private const int LOCK_TIMEOUT_SECONDS = 30;

    public DatabaseDistributedLockService(
        ApplicationDbContext context,
        ILogger<DatabaseDistributedLockService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> TryAcquireAsync(string lockKey, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var endTime = startTime.Add(timeout);

        while (DateTime.UtcNow < endTime)
        {
            try
            {
                // Clean up expired locks first
                await CleanupExpiredLocksAsync(lockKey, cancellationToken);

                // Try to acquire the lock
                var lockRecord = await _context.DistributedLocks
                    .FirstOrDefaultAsync(l => l.LockKey == lockKey, cancellationToken);

                if (lockRecord == null)
                {
                    // Lock doesn't exist, create it
                    lockRecord = new Shared.Models.DistributedLock
                    {
                        LockKey = lockKey,
                        AcquiredAt = DateTime.UtcNow,
                        ExpiresAt = DateTime.UtcNow.AddSeconds(LOCK_TIMEOUT_SECONDS),
                        OwnerId = Guid.NewGuid().ToString() // Unique owner ID for this acquisition
                    };

                    _context.DistributedLocks.Add(lockRecord);
                    await _context.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation("Acquired distributed lock: {LockKey}", lockKey);
                    return true;
                }
                else if (lockRecord.ExpiresAt < DateTime.UtcNow)
                {
                    // Lock exists but expired, take it over
                    lockRecord.AcquiredAt = DateTime.UtcNow;
                    lockRecord.ExpiresAt = DateTime.UtcNow.AddSeconds(LOCK_TIMEOUT_SECONDS);
                    lockRecord.OwnerId = Guid.NewGuid().ToString();

                    await _context.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation("Acquired expired distributed lock: {LockKey}", lockKey);
                    return true;
                }

                // Lock is held by someone else, wait and retry
                _logger.LogDebug("Lock {LockKey} is held, waiting...", lockKey);
                await Task.Delay(100, cancellationToken); // Wait 100ms before retry
            }
            catch (DbUpdateException ex)
            {
                // Concurrent insert/update - someone else got the lock
                _logger.LogDebug(ex, "Concurrent lock acquisition detected for {LockKey}", lockKey);
                await Task.Delay(100, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Lock acquisition cancelled for {LockKey}", lockKey);
                return false;
            }
        }

        _logger.LogWarning("Failed to acquire lock {LockKey} within timeout {Timeout}", lockKey, timeout);
        return false;
    }

    public async Task ReleaseAsync(string lockKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var lockRecord = await _context.DistributedLocks
                .FirstOrDefaultAsync(l => l.LockKey == lockKey, cancellationToken);

            if (lockRecord != null)
            {
                _context.DistributedLocks.Remove(lockRecord);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Released distributed lock: {LockKey}", lockKey);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing lock {LockKey}", lockKey);
        }
    }

    public async Task<bool> IsLockedAsync(string lockKey)
    {
        var lockRecord = await _context.DistributedLocks
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.LockKey == lockKey);

        return lockRecord != null && lockRecord.ExpiresAt > DateTime.UtcNow;
    }

    private async Task CleanupExpiredLocksAsync(string lockKey, CancellationToken cancellationToken)
    {
        try
        {
            var expiredLocks = await _context.DistributedLocks
                .Where(l => l.LockKey == lockKey && l.ExpiresAt < DateTime.UtcNow)
                .ToListAsync(cancellationToken);

            if (expiredLocks.Any())
            {
                _context.DistributedLocks.RemoveRange(expiredLocks);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Cleaned up {Count} expired locks for {LockKey}", 
                    expiredLocks.Count, lockKey);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up expired locks for {LockKey}", lockKey);
        }
    }
}
