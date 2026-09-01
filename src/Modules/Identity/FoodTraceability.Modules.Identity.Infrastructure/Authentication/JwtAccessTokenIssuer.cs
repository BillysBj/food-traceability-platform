using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FoodTraceability.Modules.Identity.Application.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FoodTraceability.Modules.Identity.Infrastructure.Authentication;

internal sealed class JwtAccessTokenIssuer(IOptions<JwtOptions> options) : IAccessTokenIssuer
{
    private readonly JwtOptions _options = options.Value;

    public IssuedAccessToken Issue(Guid userId, DateTimeOffset issuedAt)
    {
        var expiresAt = issuedAt.AddMinutes(_options.AccessTokenMinutes);
        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_options.SigningKey!));
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(
                JwtRegisteredClaimNames.Iat,
                issuedAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64)
        };
        var token = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            claims,
            issuedAt.UtcDateTime,
            expiresAt.UtcDateTime,
            new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));
        var serializedToken = new JwtSecurityTokenHandler().WriteToken(token);
        var expiresInSeconds = checked(_options.AccessTokenMinutes * 60);

        return new IssuedAccessToken(serializedToken, expiresInSeconds);
    }
}
