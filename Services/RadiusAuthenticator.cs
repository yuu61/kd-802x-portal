using System.Security.Cryptography;
using System.Text;
using kd_802x_portal.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace kd_802x_portal.Services;

public sealed class RadiusAuthenticator(
    AppDbContext db,
    IPasswordCryptoService passwordCrypto,
    ILogger<RadiusAuthenticator> logger) : IRadiusAuthenticator
{
    public async Task<bool> AuthenticateAsync(string username, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            return false;
        }

        var normalized = NormalizeUsername(username);
        var user = await db.Users
            .Include(u => u.Password)
            .FirstOrDefaultAsync(u => u.Username == normalized, ct);
        if (user?.Password is null)
        {
            return false;
        }

        string plaintext;
        try
        {
            plaintext = await passwordCrypto.DecryptAsync(user.Password.Ciphertext, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Vault 接続失敗や復号失敗時は認証失敗扱い。OperationCanceledException は呼び出し側へ伝播させる。
            logger.LogWarning(ex, "パスワード復号に失敗 (username={Username})", normalized);
            return false;
        }

        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        return CryptographicOperations.FixedTimeEquals(passwordBytes, plaintextBytes);
    }

    private static string NormalizeUsername(string raw)
    {
        var trimmed = raw.Trim().ToLowerInvariant();
        var atIndex = trimmed.IndexOf('@');
        var local = atIndex > 0 ? trimmed[..atIndex] : trimmed;
        return local.Replace(".", string.Empty);
    }
}
