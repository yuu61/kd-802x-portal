using System.Security.Cryptography;
using System.Text;
using kd_802x_portal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace kd_802x_portal.Controllers;

[ApiController]
[Route("api/internal/radius")]
[AllowAnonymous]
public sealed class InternalRadiusController(
    IRadiusAuthenticator authenticator,
    IConfiguration configuration,
    ILogger<InternalRadiusController> logger) : ControllerBase
{
    private readonly string _expectedSecret = configuration.GetSection("Radius")["SharedSecret"]
        ?? throw new InvalidOperationException("Radius:SharedSecret が未設定です。");

    [HttpPost("authenticate")]
    public async Task<IActionResult> Authenticate([FromBody] AuthenticateRequest request, CancellationToken ct)
    {
        if (!ValidateBearer())
        {
            logger.LogWarning("RADIUS 認証 API: Bearer トークン不一致");
            return Unauthorized();
        }
        if (request is null)
        {
            return BadRequest();
        }
        var result = await authenticator.AuthenticateAsync(request.Username, request.Password, ct);
        // FreeRADIUS の rlm_rest は HTTP ステータスコードで accept/reject を判別する
        return result
            ? Ok(new AuthenticateResponse { Result = "accept" })
            : StatusCode(StatusCodes.Status401Unauthorized, new AuthenticateResponse { Result = "reject" });
    }

    private bool ValidateBearer()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var header) || header.Count == 0)
        {
            return false;
        }
        var raw = header[0];
        if (string.IsNullOrEmpty(raw) || !raw.StartsWith("Bearer ", StringComparison.Ordinal))
        {
            return false;
        }
        var token = raw["Bearer ".Length..];
        var providedBytes = Encoding.UTF8.GetBytes(token);
        var expectedBytes = Encoding.UTF8.GetBytes(_expectedSecret);
        return CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }

    public sealed class AuthenticateRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public sealed class AuthenticateResponse
    {
        public string Result { get; set; } = "reject";
    }
}
