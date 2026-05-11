using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace kd_802x_portal.Data.Entities;

[Table("users")]
public sealed class User
{
    [Column("id")]
    public long Id { get; set; }

    [Column("username")]
    [MaxLength(64)]
    public required string Username { get; set; }

    [Column("email")]
    [MaxLength(255)]
    public required string Email { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    public EncryptedPassword? Password { get; set; }

    public static string NormalizeUsername(string raw)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(raw);
        return raw.Trim().ToLowerInvariant().Replace(".", string.Empty);
    }

    public static string NormalizeEmail(string raw)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(raw);
        var trimmed = raw.Trim().ToLowerInvariant();
        var atIndex = trimmed.IndexOf('@');
        if (atIndex <= 0)
        {
            throw new ArgumentException("メールアドレスの形式が不正です。", nameof(raw));
        }
        var local = trimmed[..atIndex].Replace(".", string.Empty);
        var domain = trimmed[atIndex..];
        return local + domain;
    }
}
