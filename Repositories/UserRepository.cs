using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using zListBack.Models;

namespace zListBack.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly IDbConnection _connection;
        private readonly ILogger<UserRepository> _logger;

        public UserRepository(IDbConnection connection, ILogger<UserRepository> logger)
        {
            _connection = connection;
            _logger = logger;
        }

        public async Task<Result<User>> GetUserByEmailAsync(string email)
        {
            try
            {
                const string sql = @"
                    SELECT Id, Email, FirstName, LastName, Password, ResetPassword,
                           Subscription, SubscriptionExpiresAt, SubscriptionSource,
                           StripeCustomerId, StripeSubscriptionId, GracePeriodUntil,
                           IsAdmin, IsHelpEnabled, SortCompletedToBottom,
                           LastActiveAt, InactivityNoticeSentAt, BillingReminderSentAt,
                           CancellationScheduledAt, CreatedAt, UpdatedAt
                    FROM Users
                    WHERE Email = @Email;";

                var user = await _connection.QuerySingleOrDefaultAsync<User>(sql, new { Email = email });
                if (user == null)
                    return Result<User>.Fail("User not found");

                return Result<User>.Ok(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetUserByEmailAsync failed. Email={Email}", email);
                return Result<User>.Fail(ex.Message);
            }
        }

        public async Task<Result<User>> GetUserAsync(int id)
        {
            try
            {
                const string sql = @"
                    SELECT Id, Email, FirstName, LastName, Password, ResetPassword,
                           Subscription, SubscriptionExpiresAt, SubscriptionSource,
                           StripeCustomerId, StripeSubscriptionId, GracePeriodUntil,
                           IsAdmin, IsHelpEnabled, SortCompletedToBottom,
                           LastActiveAt, InactivityNoticeSentAt, BillingReminderSentAt,
                           CancellationScheduledAt, CreatedAt, UpdatedAt
                    FROM Users
                    WHERE Id = @Id;";

                var user = await _connection.QuerySingleOrDefaultAsync<User>(sql, new { Id = id });
                if (user == null)
                    return Result<User>.Fail("User not found");

                return Result<User>.Ok(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetUserAsync failed. UserId={UserId}", id);
                return Result<User>.Fail(ex.Message);
            }
        }

        public async Task<Result<User>> AddUserAsync(User user)
        {
            try
            {
                const string sql = @"
                    INSERT INTO Users (Email, FirstName, LastName, Password, CreatedAt, LastActiveAt)
                    OUTPUT INSERTED.Id, INSERTED.Email, INSERTED.FirstName, INSERTED.LastName,
                           INSERTED.Password, INSERTED.ResetPassword,
                           INSERTED.Subscription, INSERTED.SubscriptionExpiresAt, INSERTED.IsHelpEnabled,
                           INSERTED.CreatedAt, INSERTED.UpdatedAt
                    VALUES (@Email, @FirstName, @LastName, @Password, @CreatedAt, @CreatedAt);";

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
                if (!isDuplicate)
                    _logger.LogError(ex, "AddUserAsync failed. Email={Email}", user.Email);
                var message = isDuplicate
                    ? "A user with this email already exists. Please login or use a different email address."
                    : ex.Message;
                return Result<User>.Fail(message);
            }
        }

        // Google-only accounts have no password to hash — separate from AddUserAsync so the
        // normal password-signup path can't accidentally be reached with a null/empty password.
        public async Task<Result<User>> AddGoogleUserAsync(User user)
        {
            try
            {
                const string sql = @"
                    INSERT INTO Users (Email, FirstName, LastName, Password, CreatedAt, LastActiveAt)
                    OUTPUT INSERTED.Id, INSERTED.Email, INSERTED.FirstName, INSERTED.LastName,
                           INSERTED.Password, INSERTED.ResetPassword,
                           INSERTED.Subscription, INSERTED.SubscriptionExpiresAt, INSERTED.IsHelpEnabled,
                           INSERTED.CreatedAt, INSERTED.UpdatedAt
                    VALUES (@Email, @FirstName, @LastName, NULL, @CreatedAt, @CreatedAt);";

                var inserted = await _connection.QuerySingleAsync<User>(
                    sql,
                    new
                    {
                        user.Email,
                        user.FirstName,
                        user.LastName,
                        CreatedAt = DateTime.UtcNow
                    }
                );

                return Result<User>.Ok(inserted, "Account Successfully created");
            }
            catch (Exception ex)
            {
                var isDuplicate = ex.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
                                  (ex.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true);
                if (!isDuplicate)
                    _logger.LogError(ex, "AddGoogleUserAsync failed. Email={Email}", user.Email);
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
                if (!string.IsNullOrEmpty(model.Password))
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
                _logger.LogError(ex, "UpdateUserAsync failed. UserId={UserId}", model.Id);
                return Result<User>.Fail(ex.Message);
            }
        }

        private const int MaxFailedLoginAttempts = 5;
        private const int LockoutMinutes = 15;

        public async Task<Result<User>> CheckLoginAsync(string email, string password)
        {
            try
            {
                const string sql = @"
                    SELECT Id, Email, FirstName, LastName, Password, ResetPassword,
                           Subscription, SubscriptionExpiresAt, SubscriptionSource,
                           StripeCustomerId, StripeSubscriptionId, GracePeriodUntil,
                           IsAdmin, IsHelpEnabled, SortCompletedToBottom,
                           LastActiveAt, InactivityNoticeSentAt, BillingReminderSentAt,
                           CancellationScheduledAt, FailedLoginAttempts, LockoutUntil,
                           CreatedAt, UpdatedAt
                    FROM Users
                    WHERE Email = @Email;";

                var user = await _connection.QuerySingleOrDefaultAsync<User>(sql, new { Email = email });
                if (user == null)
                    return Result<User>.Fail("Invalid email or password");

                // A correct reset/temp password proves email ownership, at least as strongly as the
                // original password would — so it's allowed through even while locked out; it's the
                // designated recovery path out of a lockout, not just another guessable credential.
                if (!string.IsNullOrEmpty(user.ResetPassword) && user.ResetPassword == password)
                {
                    const string updateSql = @"
                        UPDATE Users
                        SET Password = @Password, ResetPassword = NULL, UpdatedAt = GETUTCDATE()
                        WHERE Id = @Id;";

                    user.Password = BCrypt.Net.BCrypt.HashPassword(password);
                    user.ResetPassword = null;
                    await _connection.ExecuteAsync(updateSql, new { Password = user.Password, user.Id });
                    await ResetFailedLoginAttemptsAsync(user);

                    return Result<User>.Ok(user);
                }

                if (user.LockoutUntil.HasValue && user.LockoutUntil.Value > DateTime.UtcNow)
                {
                    var minutesRemaining = (int)Math.Ceiling((user.LockoutUntil.Value - DateTime.UtcNow).TotalMinutes);
                    return Result<User>.Fail($"Account locked due to too many failed login attempts. Try again in {minutesRemaining} minute(s).");
                }

                if (!string.IsNullOrEmpty(user.Password) && BCrypt.Net.BCrypt.Verify(password, user.Password))
                {
                    await ResetFailedLoginAttemptsAsync(user);
                    return Result<User>.Ok(user);
                }

                await RegisterFailedLoginAttemptAsync(user);
                return Result<User>.Fail("Invalid email or password");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CheckLoginAsync failed. Email={Email}", email);
                return Result<User>.Fail(ex.Message);
            }
        }

        private async Task ResetFailedLoginAttemptsAsync(User user)
        {
            if (user.FailedLoginAttempts == 0 && user.LockoutUntil == null)
                return;

            const string sql = @"
                UPDATE Users
                SET FailedLoginAttempts = 0, LockoutUntil = NULL
                WHERE Id = @Id;";
            await _connection.ExecuteAsync(sql, new { user.Id });
        }

        private async Task RegisterFailedLoginAttemptAsync(User user)
        {
            // A past-expired lockout starts a fresh attempt count rather than accumulating forever.
            var baseAttempts = user.LockoutUntil.HasValue && user.LockoutUntil.Value <= DateTime.UtcNow
                ? 0
                : user.FailedLoginAttempts;
            var attempts = baseAttempts + 1;
            DateTime? lockoutUntil = attempts >= MaxFailedLoginAttempts
                ? DateTime.UtcNow.AddMinutes(LockoutMinutes)
                : null;

            const string sql = @"
                UPDATE Users
                SET FailedLoginAttempts = @Attempts, LockoutUntil = @LockoutUntil
                WHERE Id = @Id;";
            await _connection.ExecuteAsync(sql, new { Attempts = attempts, LockoutUntil = lockoutUntil, user.Id });
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
                _logger.LogError(ex, "GenerateResetPassword failed. Email={Email}", email);
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

        public async Task<Result<bool>> DeleteAccountAsync(int userId, string email)
        {
            try
            {
                using var transaction = _connection.BeginTransaction();

                // Capture owned list IDs before touching UserLists
                var ownedListIds = (await _connection.QueryAsync<int>(
                    "SELECT ListId FROM UserLists WHERE UserId = @UserId AND IsOwner = 1",
                    new { UserId = userId }, transaction)).AsList();

                if (ownedListIds.Count > 0)
                {
                    var p = new { ListIds = ownedListIds };

                    await _connection.ExecuteAsync(@"
                        DELETE lri FROM ListRunItems lri
                        INNER JOIN ListRuns lr ON lr.Id = lri.ListRunId
                        WHERE lr.ListId IN @ListIds", p, transaction);

                    await _connection.ExecuteAsync(
                        "DELETE FROM ListRuns WHERE ListId IN @ListIds", p, transaction);

                    await _connection.ExecuteAsync(
                        "DELETE FROM ListItems WHERE ListId IN @ListIds", p, transaction);

                    await _connection.ExecuteAsync(
                        "DELETE FROM ListInvitations WHERE ListId IN @ListIds", p, transaction);

                    await _connection.ExecuteAsync(
                        "DELETE FROM UserLists WHERE ListId IN @ListIds", p, transaction);

                    await _connection.ExecuteAsync(
                        "DELETE FROM Lists WHERE Id IN @ListIds", p, transaction);
                }

                // Remove user from lists they were a non-owner member of
                await _connection.ExecuteAsync(
                    "DELETE FROM UserLists WHERE UserId = @UserId",
                    new { UserId = userId }, transaction);

                await _connection.ExecuteAsync(
                    "DELETE FROM SponsoredCollaborators WHERE SponsorUserId = @UserId OR SponsoredUserId = @UserId",
                    new { UserId = userId }, transaction);

                await _connection.ExecuteAsync(
                    "DELETE FROM ListInvitations WHERE LOWER(InvitedEmail) = LOWER(@Email)",
                    new { Email = email }, transaction);

                await _connection.ExecuteAsync(
                    "DELETE FROM RefreshTokens WHERE UserId = @UserId",
                    new { UserId = userId }, transaction);

                await _connection.ExecuteAsync(
                    "DELETE FROM UserPaymentHistory WHERE UserId = @UserId",
                    new { UserId = userId }, transaction);

                await _connection.ExecuteAsync(
                    "DELETE FROM Users WHERE Id = @UserId",
                    new { UserId = userId }, transaction);

                transaction.Commit();
                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeleteAccountAsync failed. UserId={UserId}", userId);
                return Result<bool>.Fail(ex.Message);
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
