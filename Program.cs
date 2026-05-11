using System.Security.Claims;
using kd_802x_portal.Components;
using kd_802x_portal.Data;
using kd_802x_portal.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using VaultSharp;
using VaultSharp.V1.AuthMethods.AppRole;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default が未設定です。");
// 起動時に DB に接続しないよう固定バージョン指定 (PoC は MariaDB 11)。
// バージョンを切り替えたい場合は appsettings の "Database:ServerVersion" を見るように拡張可能。
var serverVersion = new MariaDbServerVersion(new Version(11, 0));
// Blazor Server の長命 SignalR 回線対策で Factory を併用する。Controller / Service はそのまま Scoped DbContext を利用
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseMySql(connectionString, serverVersion));
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, serverVersion));

builder.Services.AddSingleton<IVaultClient>(sp =>
{
    var section = sp.GetRequiredService<IConfiguration>().GetSection("Vault");
    var address = section["Address"] ?? throw new InvalidOperationException("Vault:Address が未設定です。");
    var roleId = section["RoleId"] ?? throw new InvalidOperationException("Vault:RoleId が未設定です。");
    var secretId = section["SecretId"] ?? throw new InvalidOperationException("Vault:SecretId が未設定です。");
    var authMethod = new AppRoleAuthMethodInfo(roleId, secretId);
    var settings = new VaultClientSettings(address, authMethod);
    return new VaultClient(settings);
});

builder.Services.AddMemoryCache();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IDecryptionGrantService, MemoryDecryptionGrantService>();
builder.Services.AddScoped<IPasswordGenerator, RandomPasswordGenerator>();
builder.Services.AddScoped<IPasswordCryptoService, VaultTransitPasswordCryptoService>();
builder.Services.AddScoped<IUserAccountService, UserAccountService>();
builder.Services.AddScoped<IRadiusAuthenticator, RadiusAuthenticator>();
builder.Services.AddHttpContextAccessor();

var googleConfig = builder.Configuration.GetSection("Authentication:Google");
var allowedHd = googleConfig["AllowedHostedDomain"] ?? string.Empty;
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.SlidingExpiration = false;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
})
.AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    // 既定の InboundClaimTypeMap が `auth_time` を WS-* 名前空間 URI に書き換えてしまうため、
    // 短形 (`auth_time` / `email` / `hd` など) のまま参照できるよう無効化
    options.MapInboundClaims = false;
    options.Authority = googleConfig["Authority"];
    options.ClientId = googleConfig["ClientId"];
    options.ClientSecret = googleConfig["ClientSecret"];
    options.ResponseType = OpenIdConnectResponseType.Code;
    options.UsePkce = true;
    options.SaveTokens = true;
    options.Scope.Clear();
    options.Scope.Add("openid");
    options.Scope.Add("email");
    options.Scope.Add("profile");
    options.GetClaimsFromUserInfoEndpoint = true;
    // hd / email_verified は id_token に含まれており、MapInboundClaims=false 設定により短形名で参照可能
    options.Events = new OpenIdConnectEvents
    {
        OnRedirectToIdentityProvider = ctx =>
        {
            if (!string.IsNullOrEmpty(allowedHd))
            {
                ctx.ProtocolMessage.SetParameter("hd", allowedHd);
            }
            return Task.CompletedTask;
        },
        OnTokenValidated = async ctx =>
        {
            var hd = ctx.Principal?.FindFirst("hd")?.Value;
            var emailVerified = ctx.Principal?.FindFirst("email_verified")?.Value;
            var email = ctx.Principal?.FindFirst(ClaimTypes.Email)?.Value;
            if (!string.Equals(hd, allowedHd, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(emailVerified, "true", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(email))
            {
                ctx.Fail($"認証拒否: hd={hd} email_verified={emailVerified}");
                return;
            }
            var userService = ctx.HttpContext.RequestServices.GetRequiredService<IUserAccountService>();
            await userService.EnsureUserAsync(email);
        }
    };
});
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddControllers();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // PoC ではマイグレーションを未生成のため EnsureCreated でスキーマを作成する。
    // 本番化時に `dotnet ef migrations add InitialCreate` で IaC 化し Migrate() に切り替える。
    db.Database.EnsureCreated();
}

app.Run();
