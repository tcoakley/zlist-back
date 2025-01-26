using Microsoft.EntityFrameworkCore;
using zChecklist.Data;
using zChecklist.Models;

namespace zChecklist.Repositories
{
    public class UserRepository
    {
        private readonly AppDbContext _context;

        public UserRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task AddUserAsync(User user)
        {
            user.CreatedAt = DateTime.UtcNow;
            user.Password = BCrypt.Net.BCrypt.HashPassword(user.Password);
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateUserAsync(User user)
        {
            user.UpdatedAt = DateTime.UtcNow;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }

        public async Task<Result<User>> CheckLoginAsync(string email, string password)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.Password))
            {
                return new Result<User>
                {
                    Success = false,
                    Model = null,
                    Message = "Invalid email or password"
                };
            }

            return new Result<User>
            {
                Success = true,
                Model = user,
                Message = "Login successful"
            };
        }
    }
}
