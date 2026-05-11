using kd_802x_portal.Data;
using kd_802x_portal.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace kd_802x_portal.Services;

public sealed class UserAccountService(
    AppDbContext db,
    IPasswordGenerator passwordGenerator,
    IPasswordCryptoService passwordCrypto,
    TimeProvider timeProvider) : IUserAccountService
{
    public async Task<User> EnsureUserAsync(string email, CancellationToken ct = default)
    {
        var normalizedEmail = User.NormalizeEmail(email);
        var atIndex = normalizedEmail.IndexOf('@');
        var username = normalizedEmail[..atIndex];

        var existing = await db.Users
            .Include(u => u.Password)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);
        if (existing is not null)
        {
            return existing;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var user = new User
        {
            Username = username,
            Email = normalizedEmail,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        var plaintext = passwordGenerator.Generate();
        var ciphertext = await passwordCrypto.EncryptAsync(plaintext, ct);
        var encrypted = new EncryptedPassword
        {
            UserId = user.Id,
            Ciphertext = ciphertext,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.EncryptedPasswords.Add(encrypted);
        await db.SaveChangesAsync(ct);
        user.Password = encrypted;
        return user;
    }

    public async Task<string> RegeneratePasswordAsync(long userId, CancellationToken ct = default)
    {
        var encrypted = await db.EncryptedPasswords.FirstOrDefaultAsync(p => p.UserId == userId, ct)
            ?? throw new InvalidOperationException("ユーザーのパスワードが存在しません。");
        var plaintext = passwordGenerator.Generate();
        var ciphertext = await passwordCrypto.EncryptAsync(plaintext, ct);
        encrypted.Ciphertext = ciphertext;
        encrypted.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        return plaintext;
    }

    public async Task<string?> RevealPasswordAsync(long userId, CancellationToken ct = default)
    {
        var encrypted = await db.EncryptedPasswords.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        return encrypted is null ? null : await passwordCrypto.DecryptAsync(encrypted.Ciphertext, ct);
    }
}
