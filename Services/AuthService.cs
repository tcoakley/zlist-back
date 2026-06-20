using zListBack.Models;
using zListBack.Repositories;
using zListBack.Utils;

namespace zListBack.Services
{
    public class AuthService
    {
        private readonly RefreshTokenRepository _refreshTokenRepository;

        public AuthService(RefreshTokenRepository refreshTokenRepository)
        {
            _refreshTokenRepository = refreshTokenRepository;
        }

        public async Task<(string TokenString, DateTime ExpiresAt)> CreateRefreshToken(int userId)
        {
            var tokenString = TokenHelper.GenerateRefreshToken();
            var token = new RefreshToken
            {
                UserId = userId,
                Token = tokenString,
                ExpiresAt = DateTime.UtcNow.AddYears(5),
                CreatedAt = DateTime.UtcNow,
                Revoked = false
            };
            await _refreshTokenRepository.AddAsync(token);
            return (tokenString, token.ExpiresAt);
        }

        // Validates the current token, rotates it (invalidates old, stores new), and returns the
        // associated user plus the new token string. Returns null if the token is invalid/expired.
        public async Task<(User User, string NewTokenString, DateTime ExpiresAt)?> RotateRefreshToken(string currentToken)
        {
            var refreshToken = await _refreshTokenRepository.GetByTokenAsync(currentToken);
            if (refreshToken == null || refreshToken.ExpiresAt < DateTime.UtcNow || refreshToken.Revoked)
                return null;

            var user = refreshToken.User;
            if (user == null) return null;

            var newTokenString = TokenHelper.GenerateRefreshToken();
            var newToken = new RefreshToken
            {
                UserId = user.Id,
                Token = newTokenString,
                ExpiresAt = DateTime.UtcNow.AddYears(5),
                CreatedAt = DateTime.UtcNow,
                Revoked = false
            };

            await _refreshTokenRepository.InvalidateAsync(currentToken);
            await _refreshTokenRepository.AddAsync(newToken);

            return (user, newTokenString, newToken.ExpiresAt);
        }
    }
}
