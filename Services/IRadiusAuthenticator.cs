namespace kd_802x_portal.Services;

public interface IRadiusAuthenticator
{
    Task<bool> AuthenticateAsync(string username, string password, CancellationToken ct = default);
}
