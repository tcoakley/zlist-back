using Microsoft.EntityFrameworkCore;
using zListBack.Data;
using zListBack.Models;

namespace zListBack.Repositories
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

        public async Task<Result<User>> GetUserAsync(int id)
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
                if (user != null)
                {
                    return Result<User>.Ok(user);
                }
                return Result<User>.Fail($"User not found");
            }
            catch (Exception ex)
            {
                return Result<User>.Fail(ex.Message);
            }
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

        public async Task<Result<User>> UpdateUserAsync(User model)
        {
            var result = await GetUserAsync(model.Id);
            if (!result.Success)
            {
                return Result<User>.Fail("User not found");
            }
            var user = result.Model as User;
            user!.UpdatedAt = DateTime.UtcNow;
            user.Email = model.Email;
            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            if (model.Password.Length > 0)
            {
                user.Password = BCrypt.Net.BCrypt.HashPassword(model.Password);
            }
            _context.Users.Update(user);
            try
            {
                await _context.SaveChangesAsync();
                return Result<User>.Ok(user);
            }
            catch (Exception ex) { 
                return Result<User>.Fail(ex.Message);
            }
            

        }

        public async Task<Result<User>> CheckLoginAsync(string email, string password)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                return Result<User>.Fail("Invalid email or password");
            }

            if (!string.IsNullOrEmpty(user.Password) && BCrypt.Net.BCrypt.Verify(password, user.Password))
            {
                return Result<User>.Ok(user);
            }

            if (!string.IsNullOrEmpty(user.ResetPassword) && user.ResetPassword == password)
            {
                user.Password = BCrypt.Net.BCrypt.HashPassword(password);
                user.ResetPassword = null; 

                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                return Result<User>.Ok(user);
            }

            return Result<User>.Fail("Invalid email or password");
        }


        public async Task<Result<string>> GenerateResetPassword(string email)
        {
            var result = await GetUserByEmailAsync(email);
            if (!result.Success)
            {
                return Result<string>.Fail("user not found");
            }
            var user = result.Model as User;
            user!.ResetPassword = GeneratePassword();
            _context.Users.Update(user);
            try
            {
                await _context.SaveChangesAsync();
                return Result<string>.Ok(user.ResetPassword);
            }
            catch (Exception ex)
            {
                return Result<string>.Fail(ex.Message);
            }

        }


        private string GeneratePassword()
        {
            const string upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string lower = "abcdefghijklmnopqrstuvwxyz";
            const string digits = "0123456789";
            const string special = "!@#$%^&*";
            const string allChars = upper + lower + digits + special;

            Random random = new Random();
            int length = random.Next(5, 11);

            string password =
                upper[random.Next(upper.Length)].ToString() +
                lower[random.Next(lower.Length)].ToString() +
                digits[random.Next(digits.Length)].ToString() +
                special[random.Next(special.Length)].ToString();

            for (int i = password.Length; i < length; i++)
            {
                password += allChars[random.Next(allChars.Length)];
            }

            return new string(password.OrderBy(_ => random.Next()).ToArray());
        }

    }
}
