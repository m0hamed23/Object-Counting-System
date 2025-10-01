using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt; // For JwtSecurityTokenHandler, SecurityTokenDescriptor, JwtRegisteredClaimNames
using System.Security.Claims;          // For ClaimsIdentity, Claim
using System.Text;
using CountingWebAPI.Models.Database;
using CountingWebAPI.Models;

namespace CountingWebAPI.Helpers
{
    public static class JwtHelper
    {
        public static string GenerateJwtToken(User user, JwtSettings jwtSettings)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var keyBytes = Encoding.ASCII.GetBytes(jwtSettings.Key);
            if (keyBytes.Length < 32)
            {
                throw new ArgumentException("JWT secret key must be at least 32 bytes (256 bits) long for HmacSha256.");
            }
            var securityKey = new SymmetricSecurityKey(keyBytes);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(JwtRegisteredClaimNames.Sub, user.Username),
                    new Claim(JwtRegisteredClaimNames.NameId, user.Id.ToString()), // Standard claim for User ID
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) // Unique token identifier
                }),
                Expires = DateTime.UtcNow.AddHours(24),
                Issuer = jwtSettings.Issuer,
                Audience = jwtSettings.Audience,
                SigningCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}