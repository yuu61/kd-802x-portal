using kd_802x_portal.Data.Entities;

namespace kd_802x_portal.Services;

public interface IUserAccountService
{
    Task<User> EnsureUserAsync(string email, CancellationToken ct = default);
    Task<string> RegeneratePasswordAsync(long userId, CancellationToken ct = default);
    Task<string?> RevealPasswordAsync(long userId, CancellationToken ct = default);
}
