using System.Data;
using Dapper;
using zListBack.Models;

namespace zListBack.Repositories
{
    public class RefreshTokenRepository
    {
        private readonly IDbConnection _connection;

        public RefreshTokenRepository(IDbConnection connection)
        {
            _connection = connection;
        }

        public async Task AddAsync(RefreshToken refreshToken)
        {
            const string sql = @"
                INSERT INTO RefreshTokens (UserId, Token, ExpiresAt, CreatedAt, Revoked)
                OUTPUT INSERTED.Id
                VALUES (@UserId, @Token, @ExpiresAt, @CreatedAt, @Revoked);";

            refreshToken.Id = await _connection.ExecuteScalarAsync<int>(
                sql,
                new
                {
                    refreshToken.UserId,
                    refreshToken.Token,
                    refreshToken.ExpiresAt,
                    refreshToken.CreatedAt,
                    refreshToken.Revoked
                }
            );
        }

        public async Task<RefreshToken?> GetByTokenAsync(string token)
        {
            const string sql = @"
                SELECT rt.Id, rt.UserId, rt.Token, rt.ExpiresAt, rt.CreatedAt, rt.Revoked,
                       u.Id, u.Email, u.FirstName, u.LastName, u.CreatedAt, u.UpdatedAt
                FROM RefreshTokens rt
                INNER JOIN Users u ON u.Id = rt.UserId
                WHERE rt.Token = @Token AND rt.Revoked = 0;";

            var result = await _connection.QueryAsync<RefreshToken, User, RefreshToken>(
                sql,
                (refreshToken, user) =>
                {
                    refreshToken.User = user;
                    return refreshToken;
                },
                new { Token = token },
                splitOn: "Id"
            );

            return result.FirstOrDefault();
        }

        public async Task InvalidateAsync(string token)
        {
            const string sql = @"
                UPDATE RefreshTokens
                SET Revoked = 1
                WHERE Token = @Token;";

            await _connection.ExecuteAsync(sql, new { Token = token });
        }

        public async Task RemoveExpiredTokensAsync()
        {
            const string sql = @"
                DELETE FROM RefreshTokens
                WHERE ExpiresAt < GETUTCDATE() OR Revoked = 1;";

            await _connection.ExecuteAsync(sql);
        }
    }
}
