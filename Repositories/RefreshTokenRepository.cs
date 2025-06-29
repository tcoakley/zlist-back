using System;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using zListBack.Data;
using zListBack.Models;

namespace zListBack.Repositories
{
    public class RefreshTokenRepository
    {
        private readonly AppDbContext _context;

        public RefreshTokenRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(RefreshToken refreshToken)
        {
            _context.RefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync();
        }

        public async Task<RefreshToken?> GetByTokenAsync(string token)
        {
            return await _context.RefreshTokens
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt => rt.Token == token && !rt.Revoked);
        }

        public async Task InvalidateAsync(string token)
        {
            var refreshToken = await _context.RefreshTokens.FirstOrDefaultAsync(rt => rt.Token == token);
            if (refreshToken != null)
            {
                refreshToken.Revoked = true;
                await _context.SaveChangesAsync();
            }
        }

        public async Task RemoveExpiredTokensAsync()
        {
            var expiredTokens = _context.RefreshTokens.Where(rt => rt.ExpiresAt < DateTime.UtcNow || rt.Revoked);
            _context.RefreshTokens.RemoveRange(expiredTokens);
            await _context.SaveChangesAsync();
        }
    }
}
