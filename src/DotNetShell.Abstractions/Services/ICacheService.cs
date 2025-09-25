namespace DotNetShell.Abstractions.Services;

/// <summary>
/// Service interface for caching operations supporting multiple cache types and advanced features.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Gets a value from the cache.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The cached value, or null if not found.</returns>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a value from the cache or creates it using the provided factory if not found.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="factory">The factory function to create the value if not cached.</param>
    /// <param name="expiry">Optional expiration time for the cached value.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The cached or newly created value.</returns>
    Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets multiple values from the cache in a single operation.
    /// </summary>
    /// <typeparam name="T">The type of the cached values.</typeparam>
    /// <param name="keys">The cache keys to retrieve.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A dictionary mapping keys to their cached values (missing keys will not be included).</returns>
    Task<IDictionary<string, T>> GetManyAsync<T>(IEnumerable<string> keys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a value in the cache.
    /// </summary>
    /// <typeparam name="T">The type of the value to cache.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="expiry">Optional expiration time for the cached value.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the caching operation.</returns>
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a value in the cache with absolute expiration.
    /// </summary>
    /// <typeparam name="T">The type of the value to cache.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="absoluteExpiration">The absolute expiration time.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the caching operation.</returns>
    Task SetAsync<T>(string key, T value, DateTimeOffset absoluteExpiration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets multiple values in the cache in a single operation.
    /// </summary>
    /// <typeparam name="T">The type of the values to cache.</typeparam>
    /// <param name="items">The key-value pairs to cache.</param>
    /// <param name="expiry">Optional expiration time for all cached values.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the caching operation.</returns>
    Task SetManyAsync<T>(IDictionary<string, T> items, TimeSpan? expiry = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a value in the cache only if the key doesn't already exist.
    /// </summary>
    /// <typeparam name="T">The type of the value to cache.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="expiry">Optional expiration time for the cached value.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>True if the value was set; false if the key already exists.</returns>
    Task<bool> SetIfNotExistsAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a value from the cache.
    /// </summary>
    /// <param name="key">The cache key to remove.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>True if the key was found and removed; otherwise, false.</returns>
    Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes multiple values from the cache in a single operation.
    /// </summary>
    /// <param name="keys">The cache keys to remove.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The number of keys that were found and removed.</returns>
    Task<int> RemoveManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all values from the cache that match the specified pattern.
    /// </summary>
    /// <param name="pattern">The pattern to match keys against (supports wildcards like * and ?).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The number of keys that were found and removed.</returns>
    Task<int> RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a key exists in the cache.
    /// </summary>
    /// <param name="key">The cache key to check.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>True if the key exists; otherwise, false.</returns>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the remaining time to live for a cached value.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The remaining time to live, or null if the key doesn't exist or has no expiration.</returns>
    Task<TimeSpan?> GetTimeToLiveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extends the expiration time for a cached value.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="expiry">The new expiration time from now.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>True if the expiration was updated; false if the key doesn't exist.</returns>
    Task<bool> ExpireAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the absolute expiration time for a cached value.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="absoluteExpiration">The absolute expiration time.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>True if the expiration was updated; false if the key doesn't exist.</returns>
    Task<bool> ExpireAtAsync(string key, DateTimeOffset absoluteExpiration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes the expiration time for a cached value, extending it by its original TTL.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>True if the key was refreshed; false if the key doesn't exist.</returns>
    Task<bool> RefreshAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets statistics about the cache.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>Cache statistics information.</returns>
    Task<CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all values from the cache.
    /// Warning: This operation may be destructive and should be used with caution.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the clear operation.</returns>
    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all keys in the cache that match the specified pattern.
    /// </summary>
    /// <param name="pattern">The pattern to match keys against (supports wildcards like * and ?).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An enumerable collection of matching keys.</returns>
    Task<IEnumerable<string>> GetKeysAsync(string pattern = "*", CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a distributed lock using the cache infrastructure.
    /// </summary>
    /// <param name="lockKey">The key for the lock.</param>
    /// <param name="expiry">How long the lock should be held.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A distributed lock instance, or null if the lock could not be acquired.</returns>
    Task<IDistributedLock?> AcquireLockAsync(string lockKey, TimeSpan expiry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Increments a numeric value in the cache.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="delta">The amount to increment by (default is 1).</param>
    /// <param name="initialValue">The initial value if the key doesn't exist (default is 0).</param>
    /// <param name="expiry">Optional expiration time for the value.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The new value after incrementing.</returns>
    Task<long> IncrementAsync(string key, long delta = 1, long initialValue = 0, TimeSpan? expiry = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Decrements a numeric value in the cache.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="delta">The amount to decrement by (default is 1).</param>
    /// <param name="initialValue">The initial value if the key doesn't exist (default is 0).</param>
    /// <param name="expiry">Optional expiration time for the value.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The new value after decrementing.</returns>
    Task<long> DecrementAsync(string key, long delta = 1, long initialValue = 0, TimeSpan? expiry = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a cache instance scoped to a specific prefix.
    /// </summary>
    /// <param name="prefix">The prefix to use for all keys in the scoped cache.</param>
    /// <returns>A scoped cache service instance.</returns>
    ICacheService WithPrefix(string prefix);

    /// <summary>
    /// Creates a cache instance with default expiration settings.
    /// </summary>
    /// <param name="defaultExpiry">The default expiration time for cached values.</param>
    /// <returns>A cache service instance with default expiration.</returns>
    ICacheService WithDefaultExpiry(TimeSpan defaultExpiry);

    /// <summary>
    /// Gets the cache provider name (e.g., "Redis", "InMemory", "Distributed").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Gets a value indicating whether the cache supports distributed operations.
    /// </summary>
    bool IsDistributed { get; }

    /// <summary>
    /// Gets a value indicating whether the cache supports transactions.
    /// </summary>
    bool SupportsTransactions { get; }
}

/// <summary>
/// Represents a distributed lock acquired through the cache service.
/// </summary>
public interface IDistributedLock : IDisposable
{
    /// <summary>
    /// Gets the lock key.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// Gets the lock expiration time.
    /// </summary>
    DateTimeOffset ExpiresAt { get; }

    /// <summary>
    /// Gets a value indicating whether the lock is still valid.
    /// </summary>
    bool IsValid { get; }

    /// <summary>
    /// Extends the lock expiration time.
    /// </summary>
    /// <param name="extension">The time to extend the lock by.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>True if the lock was extended; false if the lock is no longer valid.</returns>
    Task<bool> ExtendAsync(TimeSpan extension, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases the lock early.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the release operation.</returns>
    Task ReleaseAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Contains cache statistics and performance metrics.
/// </summary>
public class CacheStatistics
{
    /// <summary>
    /// Gets or sets the total number of cache hits.
    /// </summary>
    public long Hits { get; set; }

    /// <summary>
    /// Gets or sets the total number of cache misses.
    /// </summary>
    public long Misses { get; set; }

    /// <summary>
    /// Gets or sets the total number of items in the cache.
    /// </summary>
    public long Count { get; set; }

    /// <summary>
    /// Gets or sets the cache hit ratio (0.0 to 1.0).
    /// </summary>
    public double HitRatio => Hits + Misses > 0 ? (double)Hits / (Hits + Misses) : 0.0;

    /// <summary>
    /// Gets or sets the approximate memory usage of the cache in bytes.
    /// </summary>
    public long? MemoryUsageBytes { get; set; }

    /// <summary>
    /// Gets or sets the maximum memory limit for the cache in bytes.
    /// </summary>
    public long? MemoryLimitBytes { get; set; }

    /// <summary>
    /// Gets or sets the number of evicted items.
    /// </summary>
    public long EvictedItems { get; set; }

    /// <summary>
    /// Gets or sets the number of expired items.
    /// </summary>
    public long ExpiredItems { get; set; }

    /// <summary>
    /// Gets or sets the average operation duration.
    /// </summary>
    public TimeSpan? AverageOperationDuration { get; set; }

    /// <summary>
    /// Gets or sets the cache uptime.
    /// </summary>
    public TimeSpan? Uptime { get; set; }

    /// <summary>
    /// Gets or sets additional provider-specific statistics.
    /// </summary>
    public IDictionary<string, object> AdditionalStats { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets the timestamp when these statistics were collected.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Enumeration of cache eviction policies.
/// </summary>
public enum CacheEvictionPolicy
{
    /// <summary>
    /// Least Recently Used - evicts the least recently accessed items first.
    /// </summary>
    LRU,

    /// <summary>
    /// Least Frequently Used - evicts the least frequently accessed items first.
    /// </summary>
    LFU,

    /// <summary>
    /// First In, First Out - evicts the oldest items first.
    /// </summary>
    FIFO,

    /// <summary>
    /// Time To Live - evicts items based on their expiration time.
    /// </summary>
    TTL,

    /// <summary>
    /// Random - evicts items randomly.
    /// </summary>
    Random
}

/// <summary>
/// Configuration options for cache behavior.
/// </summary>
public class CacheOptions
{
    /// <summary>
    /// Gets or sets the default expiration time for cached items.
    /// </summary>
    public TimeSpan? DefaultExpiry { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of items the cache can hold.
    /// </summary>
    public long? MaxItems { get; set; }

    /// <summary>
    /// Gets or sets the maximum memory usage for the cache.
    /// </summary>
    public long? MaxMemoryBytes { get; set; }

    /// <summary>
    /// Gets or sets the eviction policy to use when the cache is full.
    /// </summary>
    public CacheEvictionPolicy EvictionPolicy { get; set; } = CacheEvictionPolicy.LRU;

    /// <summary>
    /// Gets or sets a value indicating whether to enable cache statistics collection.
    /// </summary>
    public bool EnableStatistics { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to compress cached values.
    /// </summary>
    public bool EnableCompression { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to encrypt cached values.
    /// </summary>
    public bool EnableEncryption { get; set; }

    /// <summary>
    /// Gets or sets the serialization format for cached objects.
    /// </summary>
    public string SerializationFormat { get; set; } = "Json";

    /// <summary>
    /// Gets or sets additional provider-specific options.
    /// </summary>
    public IDictionary<string, object> ProviderOptions { get; set; } = new Dictionary<string, object>();
}

/// <summary>
/// Extension methods for ICacheService to provide additional convenience methods.
/// </summary>
public static class CacheServiceExtensions
{
    /// <summary>
    /// Gets a cached value or sets it using the provided factory, with a default expiry of 5 minutes.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="cache">The cache service.</param>
    /// <param name="key">The cache key.</param>
    /// <param name="factory">The factory function to create the value if not cached.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The cached or newly created value.</returns>
    public static Task<T> GetOrCreateAsync<T>(this ICacheService cache, string key, Func<Task<T>> factory, CancellationToken cancellationToken = default)
    {
        return cache.GetOrCreateAsync(key, factory, TimeSpan.FromMinutes(5), cancellationToken);
    }

    /// <summary>
    /// Sets a value in the cache with a default expiry of 5 minutes.
    /// </summary>
    /// <typeparam name="T">The type of the value to cache.</typeparam>
    /// <param name="cache">The cache service.</param>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the caching operation.</returns>
    public static Task SetAsync<T>(this ICacheService cache, string key, T value, CancellationToken cancellationToken = default)
    {
        return cache.SetAsync(key, value, TimeSpan.FromMinutes(5), cancellationToken);
    }

    /// <summary>
    /// Creates a cache key from multiple parts.
    /// </summary>
    /// <param name="parts">The parts to combine into a cache key.</param>
    /// <returns>A combined cache key.</returns>
    public static string CreateKey(params string[] parts)
    {
        return string.Join(":", parts.Where(p => !string.IsNullOrEmpty(p)));
    }

    /// <summary>
    /// Wraps a function with caching, automatically caching the result.
    /// </summary>
    /// <typeparam name="TResult">The return type of the function.</typeparam>
    /// <param name="cache">The cache service.</param>
    /// <param name="key">The cache key.</param>
    /// <param name="function">The function to cache.</param>
    /// <param name="expiry">The cache expiry time.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The cached or computed result.</returns>
    public static async Task<TResult> CacheAsync<TResult>(this ICacheService cache, string key, Func<Task<TResult>> function, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        var cached = await cache.GetAsync<TResult>(key, cancellationToken);
        if (cached != null)
        {
            return cached;
        }

        var result = await function();
        await cache.SetAsync(key, result, expiry, cancellationToken);
        return result;
    }
}