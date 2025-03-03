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

        public async Task<Result<User>> GetUserByEmailAsync(string email)
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (user == null)
                {
                    return Result<User>.Fail("User not found");
                }
                return Result<User>.Ok(user);
            }
            catch (Exception ex)
            {
                return Result<User>.Fail(ex.Message);
            }
            
        }

        public async Task<User?> GetUserAsync(int id)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<Result<User>> AddUserAsync(User user)
        {
            try
            {
                user.CreatedAt = DateTime.UtcNow;
                user.Password = BCrypt.Net.BCrypt.HashPassword(user.Password);

                _context.Users.Add(user);
                await _context.SaveChangesAsync();
                return Result<User>.Ok(user, "Account Successfully created");
            }
            catch (Exception ex)
            {
                var message = ex.InnerException != null && ex.InnerException.Message.Contains("Duplicate", StringComparison.OrdinalIgnoreCase)
                    ? "A user with this email already exists. Please login or use a different email address."
                    : ex.Message;
                return Result<User>.Fail(message);

            }
        }

        public async Task<Result> UpdateUserAsync(User model)
        {
            var user = await GetUserAsync(model.Id);
            if (user == null)
            {
                return Result.Fail("User not found");
            }
            user.UpdatedAt = DateTime.UtcNow;
            user.Email = model.Email;
            user.FirstName = model.FirstName;
            user.Lastname = model.Lastname;
            if (model.Password.Length > 0)
            {
                user.Password = BCrypt.Net.BCrypt.HashPassword(model.Password);
            }
            _context.Users.Update(user);
            try
            {
                await _context.SaveChangesAsync();
                return Result.Ok();
            }
            catch (Exception ex) { 
                return Result.Fail(ex.Message);
            }
            

        }

        public async Task<Result<User>> CheckLoginAsync(string email, string password)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.Password))
            {
                return Result<User>.Fail("Invalid email or password");
            }
            return Result<User>.Ok(user);

        }
    }
}
