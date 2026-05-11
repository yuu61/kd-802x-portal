namespace kd_802x_portal.Services;

public interface IPasswordCryptoService
{
    Task<string> EncryptAsync(string plaintext, CancellationToken ct = default);
    Task<string> DecryptAsync(string ciphertext, CancellationToken ct = default);
}
