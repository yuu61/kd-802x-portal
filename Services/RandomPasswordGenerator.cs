using System.Security.Cryptography;

namespace kd_802x_portal.Services;

public sealed class RandomPasswordGenerator : IPasswordGenerator
{
    public string Generate()
    {
        Span<byte> buffer = stackalloc byte[16];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToBase64String(buffer)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
