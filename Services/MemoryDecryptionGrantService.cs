using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;

namespace kd_802x_portal.Services;

public sealed class MemoryDecryptionGrantService(IMemoryCache cache) : IDecryptionGrantService
{
    private readonly IMemoryCache _cache = cache;

    public string Issue(long userId, TimeSpan lifetime)
    {
        Span<byte> buffer = stackalloc byte[32];
        RandomNumberGenerator.Fill(buffer);
        var token = Convert.ToHexString(buffer);
        _cache.Set(CacheKey(token), userId, lifetime);
        return token;
    }

    public bool TryConsume(string token, out long userId)
    {
        if (string.IsNullOrEmpty(token) || !_cache.TryGetValue<long>(CacheKey(token), out var stored))
        {
            userId = 0;
            return false;
        }
        userId = stored;
        _cache.Remove(CacheKey(token));
        return true;
    }

    private static string CacheKey(string token) => $"DecryptionGrant:{token}";
}
