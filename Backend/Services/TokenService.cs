using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Backend.Models;
using Microsoft.IdentityModel.Tokens;

namespace Backend.Services;

public interface ITokenService
{
    (string Token, DateTime ExpiresAtUtc) CreateToken(ApplicationUser user, IList<string> roles);
}

/// <summary>
/// Issues signed JWT bearer tokens. Claims carry only pseudonymous identity
/// (user id, username, DisplayName, roles) — never FirstName/LastName/Email,
/// so a decoded student token can't leak PII either.
/// </summary>
public sealed class TokenService(IConfiguration config) : ITokenService
{
    public (string Token, DateTime ExpiresAtUtc) CreateToken(ApplicationUser user, IList<string> roles)
    {
        var jwt = config.GetRequiredSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]
            ?? throw new InvalidOperationException("Jwt:Key is not configured.")));

        var expiresAtUtc = DateTime.UtcNow.AddMinutes(jwt.GetValue("ExpiryMinutes", 480));

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName!),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("displayName", user.DisplayName)
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var token = new JwtSecurityToken(
            issuer: jwt["Issuer"],
            audience: jwt["Audience"],
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }
}
