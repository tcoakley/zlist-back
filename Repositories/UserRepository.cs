using System.Data;
using Dapper;
using zListBack.Models;

namespace zListBack.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly IDbConnection _connection;

        public UserRepository(IDbConnection connection)
        {
            _connection = connection;
        }

        public async Task<Result<User>> GetUserByEmailAsync(string email)
        {
            try
            {
                const string sql = @"
                    SELECT Id, Email, FirstName, LastName, Password, ResetPassword,
                           Subscription, SubscriptionExpiresAt, SubscriptionSource, IsAdmin,
                           IsHelpEnabled, SortCompletedToBottom, CreatedAt, UpdatedAt
                    FROM Users
                    WHERE Email = @Email;";

                var user = await _connection.QuerySingleOrDefaultAsync<User>(sql, new { Email = email });
                if (user == null)
                    return Result<User>.Fail("User not found");

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
                const string sql = @"
                    SELECT Id, Email, FirstName, LastName, Password, ResetPassword,
                           Subscription, SubscriptionExpiresAt, SubscriptionSource, IsAdmin,
                           IsHelpEnabled, SortCompletedToBottom, CreatedAt, UpdatedAt
                    FROM Users
                    WHERE Id = @Id;";

                var user = await _connection.QuerySingleOrDefaultAsync<User>(sql, new { Id = id });
                if (user == null)
                    return Result<User>.Fail("User not found");

                return Result<User>.Ok(user);
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
                const string sql = @"
                    INSERT INTO Users (Email, FirstName, LastName, Password, CreatedAt)
                    OUTPUT INSERTED.Id, INSERTED.Email, INSERTED.FirstName, INSERTED.LastName,
                           INSERTED.Password, INSERTED.ResetPassword,
                           INSERTED.Subscription, INSERTED.SubscriptionExpiresAt, INSERTED.IsHelpEnabled,
                           INSERTED.CreatedAt, INSERTED.UpdatedAt
                    VALUES (@Email, @FirstName, @LastName, @Password, @CreatedAt);";

                var inserted = await _connection.QuerySingleAsync<User>(
                    sql,
                    new
                    {
                        user.Email,
                        user.FirstName,
                        user.LastName,
                        Password = BCrypt.Net.BCrypt.HashPassword(user.Password),
                        CreatedAt = DateTime.UtcNow
                    }
                );

                return Result<User>.Ok(inserted, "Account Successfully created");
            }
            catch (Exception ex)
            {
                var isDuplicate = ex.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
                                  (ex.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true);
                var message = isDuplicate
                    ? "A user with this email already exists. Please login or use a different email address."
                    : ex.Message;
                return Result<User>.Fail(message);
            }
        }

        public async Task<Result<User>> UpdateUserAsync(User model)
        {
            try
            {
                if (model.Password.Length > 0)
                {
                    const string sql = @"
                        UPDATE Users
                        SET Email = @Email, FirstName = @FirstName, LastName = @LastName,
                            Password = @Password, IsHelpEnabled = @IsHelpEnabled,
                            SortCompletedToBottom = @SortCompletedToBottom, UpdatedAt = GETUTCDATE()
                        OUTPUT INSERTED.Id, INSERTED.Email, INSERTED.FirstName, INSERTED.LastName,
                               INSERTED.Subscription, INSERTED.SubscriptionExpiresAt, INSERTED.IsHelpEnabled,
                               INSERTED.SortCompletedToBottom, INSERTED.CreatedAt, INSERTED.UpdatedAt
                        WHERE Id = @Id;";

                    var updated = await _connection.QuerySingleOrDefaultAsync<User>(
                        sql,
                        new
                        {
                            model.Id,
                            model.Email,
                            model.FirstName,
                            model.LastName,
                            Password = BCrypt.Net.BCrypt.HashPassword(model.Password),
                            model.IsHelpEnabled,
                            model.SortCompletedToBottom
                        }
                    );

                    if (updated == null)
                        return Result<User>.Fail("User not found");

                    return Result<User>.Ok(updated);
                }
                else
                {
                    const string sql = @"
                        UPDATE Users
                        SET Email = @Email, FirstName = @FirstName, LastName = @LastName,
                            IsHelpEnabled = @IsHelpEnabled, SortCompletedToBottom = @SortCompletedToBottom,
                            UpdatedAt = GETUTCDATE()
                        OUTPUT INSERTED.Id, INSERTED.Email, INSERTED.FirstName, INSERTED.LastName,
                               INSERTED.Subscription, INSERTED.SubscriptionExpiresAt, INSERTED.IsHelpEnabled,
                               INSERTED.SortCompletedToBottom, INSERTED.CreatedAt, INSERTED.UpdatedAt
                        WHERE Id = @Id;";

                    var updated = await _connection.QuerySingleOrDefaultAsync<User>(
                        sql,
                        new
                        {
                            model.Id,
                            model.Email,
                            model.FirstName,
                            model.LastName,
                            model.IsHelpEnabled,
                            model.SortCompletedToBottom
                        }
                    );

                    if (updated == null)
                        return Result<User>.Fail("User not found");

                    return Result<User>.Ok(updated);
                }
            }
            catch (Exception ex)
            {
                return Result<User>.Fail(ex.Message);
            }
        }

        public async Task<Result<User>> CheckLoginAsync(string email, string password)
        {
            try
            {
                const string sql = @"
                    SELECT Id, Email, FirstName, LastName, Password, ResetPassword,
                           Subscription, SubscriptionExpiresAt, SubscriptionSource, IsAdmin,
                           IsHelpEnabled, SortCompletedToBottom, CreatedAt, UpdatedAt
                    FROM Users
                    WHERE Email = @Email;";

                var user = await _connection.QuerySingleOrDefaultAsync<User>(sql, new { Email = email });
                if (user == null)
                    return Result<User>.Fail("Invalid email or password");

                if (!string.IsNullOrEmpty(user.Password) && BCrypt.Net.BCrypt.Verify(password, user.Password))
                    return Result<User>.Ok(user);

                if (!string.IsNullOrEmpty(user.ResetPassword) && user.ResetPassword == password)
                {
                    const string updateSql = @"
                        UPDATE Users
                        SET Password = @Password, ResetPassword = NULL, UpdatedAt = GETUTCDATE()
                        WHERE Id = @Id;";

                    user.Password = BCrypt.Net.BCrypt.HashPassword(password);
                    user.ResetPassword = null;
                    await _connection.ExecuteAsync(updateSql, new { Password = user.Password, user.Id });

                    return Result<User>.Ok(user);
                }

                return Result<User>.Fail("Invalid email or password");
            }
            catch (Exception ex)
            {
                return Result<User>.Fail(ex.Message);
            }
        }

        public async Task<Result<string>> GenerateResetPassword(string email)
        {
            try
            {
                var result = await GetUserByEmailAsync(email);
                if (!result.Success)
                    return Result<string>.Fail("user not found");

                var user = result.Model as User;
                var resetPassword = GeneratePassword();

                const string sql = @"
                    UPDATE Users
                    SET ResetPassword = @ResetPassword, UpdatedAt = GETUTCDATE()
                    WHERE Id = @Id;";

                await _connection.ExecuteAsync(sql, new { ResetPassword = resetPassword, user!.Id });

                return Result<string>.Ok(resetPassword);
            }
            catch (Exception ex)
            {
                return Result<string>.Fail(ex.Message);
            }
        }

        public async Task UpdateLastActiveAt(int userId)
        {
            const string sql = @"
                UPDATE Users
                SET LastActiveAt = GETUTCDATE(),
                    InactivityNoticeSentAt = NULL
                WHERE Id = @UserId;";
            await _connection.ExecuteAsync(sql, new { UserId = userId });
        }

        public async Task ClearInactivityNotice(int userId)
        {
            const string sql = @"
                UPDATE Users SET InactivityNoticeSentAt = NULL WHERE Id = @UserId;";
            await _connection.ExecuteAsync(sql, new { UserId = userId });
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
