using System.Security.Claims;
using kd_802x_portal.Data;
using kd_802x_portal.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace kd_802x_portal.Controllers;

[Route("oidc")]
public sealed class OidcChallengeController(
    AppDbContext db,
    IDecryptionGrantService grantService,
    IConfiguration configuration,
    TimeProvider timeProvider) : Controller
{
    [Authorize]
    [HttpGet("challenge")]
    public IActionResult InitiateChallenge([FromQuery] string action = "reveal", [FromQuery] string? returnUrl = null)
    {
        var redirectUri = Url.Action(nameof(Callback), "OidcChallenge", new { action, returnUrl })!;
        var properties = new AuthenticationProperties { RedirectUri = redirectUri };
        properties.SetParameter("prompt", "login");
        properties.SetParameter("max_age", "0");
        return Challenge(properties, OpenIdConnectDefaults.AuthenticationScheme);
    }

    [Authorize]
    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string action, [FromQuery] string? returnUrl, CancellationToken ct)
    {
        var defaultReturn = returnUrl ?? "/dashboard";

        var maxAgeText = configuration.GetSection("Authentication:Google")["ReauthMaxAgeSeconds"] ?? "120";
        if (!int.TryParse(maxAgeText, out var maxAgeSeconds))
        {
            maxAgeSeconds = 120;
        }
        var authTimeClaim = User.FindFirst("auth_time")?.Value;
        if (!long.TryParse(authTimeClaim, out var authTimeUnix))
        {
            return Redirect(defaultReturn + "?reauth=failed");
        }
        var authTime = DateTimeOffset.FromUnixTimeSeconds(authTimeUnix);
        var elapsed = timeProvider.GetUtcNow() - authTime;
        if (elapsed > TimeSpan.FromSeconds(maxAgeSeconds))
        {
            return Redirect(defaultReturn + "?reauth=stale");
        }

        if (!string.Equals(action, "reveal", StringComparison.Ordinal) &&
            !string.Equals(action, "regenerate", StringComparison.Ordinal))
        {
            return Redirect(defaultReturn);
        }

        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(email))
        {
            return Redirect(defaultReturn + "?reauth=failed");
        }
        var normalizedEmail = kd_802x_portal.Data.Entities.User.NormalizeEmail(email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);
        if (user is null)
        {
            return Redirect(defaultReturn + "?reauth=failed");
        }

        var lifetimeText = configuration.GetSection("PasswordReveal")["GrantLifetimeSeconds"] ?? "30";
        if (!int.TryParse(lifetimeText, out var lifetimeSeconds))
        {
            lifetimeSeconds = 30;
        }
        var token = grantService.Issue(user.Id, TimeSpan.FromSeconds(lifetimeSeconds));
        return string.Equals(action, "regenerate", StringComparison.Ordinal)
            ? Redirect($"/dashboard?regenerate={token}")
            : Redirect($"/dashboard?reveal={token}");
    }

    [HttpGet("signin")]
    [AllowAnonymous]
    public IActionResult SignIn([FromQuery] string? returnUrl = null)
    {
        var redirectUri = string.IsNullOrEmpty(returnUrl) ? "/dashboard" : returnUrl;
        var properties = new AuthenticationProperties { RedirectUri = redirectUri };
        return Challenge(properties, OpenIdConnectDefaults.AuthenticationScheme);
    }

    [Authorize]
    [HttpGet("signout")]
    public IActionResult LogOut()
    {
        return SignOut(
            new AuthenticationProperties { RedirectUri = "/" },
            "Cookies",
            OpenIdConnectDefaults.AuthenticationScheme);
    }
}
