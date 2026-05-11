using System.Text;
using VaultSharp;
using VaultSharp.V1.SecretsEngines.Transit;

namespace kd_802x_portal.Services;

public sealed class VaultTransitPasswordCryptoService(
    IVaultClient vaultClient,
    IConfiguration configuration) : IPasswordCryptoService
{
    private readonly IVaultClient _vault = vaultClient;
    private readonly string _keyName = configuration.GetSection("Vault")["TransitKeyName"]
        ?? throw new InvalidOperationException("Vault:TransitKeyName が未設定です。");

    public async Task<string> EncryptAsync(string plaintext, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(plaintext);
        var request = new EncryptRequestOptions
        {
            Base64EncodedPlainText = Convert.ToBase64String(Encoding.UTF8.GetBytes(plaintext))
        };
        var response = await _vault.V1.Secrets.Transit.EncryptAsync(_keyName, request);
        return response.Data.CipherText;
    }

    public async Task<string> DecryptAsync(string ciphertext, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(ciphertext);
        var request = new DecryptRequestOptions
        {
            CipherText = ciphertext
        };
        var response = await _vault.V1.Secrets.Transit.DecryptAsync(_keyName, request);
        var plaintextBytes = Convert.FromBase64String(response.Data.Base64EncodedPlainText);
        return Encoding.UTF8.GetString(plaintextBytes);
    }
}
