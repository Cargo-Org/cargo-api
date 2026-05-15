namespace Cargo.BuildingBlocks.Utils.Cache;

/// <summary>
/// Generic distributed-cache abstraction backed by Redis.
/// Hides <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/>
/// complexity and provides strongly-typed JSON serialisation.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Stores <paramref name="value"/> as JSON under <paramref name="key"/>
    /// with an absolute expiry of <paramref name="ttl"/>.
    /// Overwrites any existing value.
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan ttl,
                     CancellationToken ct = default);

    /// <summary>
    /// Returns the deserialised value, or <c>default</c> if the key is
    /// missing or expired.
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);

    /// <summary>
    /// Deletes the key unconditionally. Safe to call even if it doesn't exist.
    /// </summary>
    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Returns <c>true</c> if the key exists and has not expired.
    /// </summary>
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
}