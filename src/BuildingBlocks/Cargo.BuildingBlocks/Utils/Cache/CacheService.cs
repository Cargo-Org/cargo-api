using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Cargo.BuildingBlocks.Utils.Cache;

/// <summary>
/// Strongly-typed Redis wrapper over <see cref="IDistributedCache"/>.
///
/// All values are JSON-serialised so any POCO can be stored.
/// Callers never deal with raw byte arrays or DistributedCacheEntryOptions.
/// </summary>
public class CacheService(
    IDistributedCache cache,
    ILogger<CacheService> logger) : ICacheService
{
    // Re-use a single JsonSerializerOptions instance for performance
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    // ──────────────────────────────────────────────────────────────
    //  SetAsync
    // ──────────────────────────────────────────────────────────────

    public async Task SetAsync<T>(
        string key, T value, TimeSpan ttl, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, _json);

            var entryOptions = new DistributedCacheEntryOptions
            {
                // Absolute expiry: OTPs must expire at a fixed wall-clock time
                AbsoluteExpirationRelativeToNow = ttl
            };

            await cache.SetAsync(key, bytes, entryOptions, ct);

            logger.LogDebug("Cache SET [{Key}] TTL={Ttl}", key, ttl);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Cache SET failed for key [{Key}]", key);
            throw;
        }
    }

    // ──────────────────────────────────────────────────────────────
    //  GetAsync
    // ──────────────────────────────────────────────────────────────

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            byte[]? bytes = await cache.GetAsync(key, ct);

            if (bytes is null or { Length: 0 })
            {
                logger.LogDebug("Cache MISS [{Key}]", key);
                return default;
            }

            logger.LogDebug("Cache HIT [{Key}]", key);
            return JsonSerializer.Deserialize<T>(bytes, _json);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Cache GET failed for key [{Key}]", key);
            throw;
        }
    }

    // ──────────────────────────────────────────────────────────────
    //  RemoveAsync
    // ──────────────────────────────────────────────────────────────

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            await cache.RemoveAsync(key, ct);
            logger.LogDebug("Cache REMOVE [{Key}]", key);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Cache REMOVE failed for key [{Key}]", key);
            throw;
        }
    }

    // ──────────────────────────────────────────────────────────────
    //  ExistsAsync
    // ──────────────────────────────────────────────────────────────

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        // IDistributedCache has no native "exists" — we do a lightweight
        // GET and check for null.  Redis GET on a missing key is O(1).
        byte[]? bytes = await cache.GetAsync(key, ct);
        return bytes is not null;
    }
}