using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using PriceTracker.Domain.Models.Entities;

namespace PriceTracker.Api.Panel;

public sealed class PanelJwt
{
    public const string CookieName = "price_tracker_session";
    public const string RefreshCookieName = "price_tracker_refresh";
    public const string NameClaim = "username";
    public const string RoleClaim = "role";
    public const string SubjectClaim = JwtRegisteredClaimNames.Sub;
    public const string SessionClaim = "sid";

    private readonly string _issuer;
    private readonly string _audience;
    private readonly SymmetricSecurityKey _key;
    public int LifetimeMinutes { get; }
    public int RefreshLifetimeDays { get; }

    public PanelJwt(IConfiguration configuration, bool development)
    {
        _issuer = configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException("Set Jwt:Issuer.");
        _audience = configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException("Set Jwt:Audience.");
        LifetimeMinutes = configuration.GetValue<int>("Jwt:LifetimeMinutes");
        RefreshLifetimeDays = configuration.GetValue<int>("Jwt:RefreshLifetimeDays");
        if (string.IsNullOrWhiteSpace(_issuer) || string.IsNullOrWhiteSpace(_audience) || LifetimeMinutes is < 1 or > 720)
            throw new InvalidOperationException("Configure Jwt issuer, audience and a lifetime between 1 and 720 minutes.");
        if (RefreshLifetimeDays is < 1 or > 90)
            throw new InvalidOperationException("Configure Jwt:RefreshLifetimeDays between 1 and 90.");

        var secret = configuration["Jwt:SigningKey"];
        var bytes = new byte[64];
        if (string.IsNullOrWhiteSpace(secret) && development)
            bytes = RandomNumberGenerator.GetBytes(32);
        else
        {
            if (string.IsNullOrWhiteSpace(secret) || !Convert.TryFromBase64String(secret, bytes, out var length)
                || length is < 32 or > 64)
                throw new InvalidOperationException("Set Jwt__SigningKey to 32–64 random bytes encoded as Base64.");
            bytes = bytes[..length];
        }
        _key = new SymmetricSecurityKey(bytes);
    }

    public TokenValidationParameters ValidationParameters => new()
    {
        ValidIssuer = _issuer,
        ValidAudience = _audience,
        IssuerSigningKey = _key,
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        RequireExpirationTime = true,
        RequireSignedTokens = true,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ValidTypes = ["JWT"],
        ClockSkew = TimeSpan.Zero,
        NameClaimType = NameClaim,
        RoleClaimType = RoleClaim
    };

    public string Issue(User user, UserSession session, DateTime now, DateTime expiresAt)
    {
        var claims = new Claim[]
        {
            new(SubjectClaim, user.Id.ToString()),
            new(SessionClaim, session.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(NameClaim, user.UserName),
            new(RoleClaim, PanelSecurity.RoleName(user.Role)),
            new(JwtRegisteredClaimNames.Iat, EpochTime.GetIntDate(now).ToString(CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64)
        };
        var token = new JwtSecurityToken(_issuer, _audience, claims, now, expiresAt,
            new SigningCredentials(_key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static string NewRefreshToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

    public static string? RefreshHash(string? token)
    {
        if (token is not { Length: 43 } ||
            token.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_'))
            return null;
        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(token)));
    }
}
