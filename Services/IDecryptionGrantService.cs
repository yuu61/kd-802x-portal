namespace kd_802x_portal.Services;

public interface IDecryptionGrantService
{
    string Issue(long userId, TimeSpan lifetime);
    bool TryConsume(string token, out long userId);
}
