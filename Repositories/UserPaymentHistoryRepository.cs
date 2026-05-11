using System.Data;
using Dapper;
using zListBack.Models;

namespace zListBack.Repositories
{
    public class UserPaymentHistoryRepository
    {
        private readonly IDbConnection _connection;

        public UserPaymentHistoryRepository(IDbConnection connection)
        {
            _connection = connection;
        }

        public async Task<Result<UserPaymentHistory>> AddAsync(UserPaymentHistory payment)
        {
            try
            {
                const string sql = @"
                    INSERT INTO UserPaymentHistory
                        (UserId, StripeEventId, AmountPaid, Currency, PlanType, PaidAt, CreatedAt)
                    OUTPUT INSERTED.Id, INSERTED.UserId, INSERTED.StripeEventId, INSERTED.AmountPaid,
                           INSERTED.Currency, INSERTED.PlanType, INSERTED.PaidAt, INSERTED.CreatedAt
                    VALUES
                        (@UserId, @StripeEventId, @AmountPaid, @Currency, @PlanType, @PaidAt, @CreatedAt);";

                var inserted = await _connection.QuerySingleAsync<UserPaymentHistory>(sql, new
                {
                    payment.UserId,
                    payment.StripeEventId,
                    payment.AmountPaid,
                    payment.Currency,
                    payment.PlanType,
                    payment.PaidAt,
                    CreatedAt = DateTime.UtcNow
                });

                return Result<UserPaymentHistory>.Ok(inserted);
            }
            catch (Exception ex)
            {
                // Duplicate StripeEventId — idempotent, not a real error
                if (ex.Message.Contains("UQ_UserPaymentHistory_StripeEventId", StringComparison.OrdinalIgnoreCase))
                    return Result<UserPaymentHistory>.Fail("Duplicate Stripe event — already recorded.");

                return Result<UserPaymentHistory>.Fail(ex.Message);
            }
        }

        public async Task<IEnumerable<UserPaymentHistory>> GetByUserIdAsync(int userId)
        {
            const string sql = @"
                SELECT Id, UserId, StripeEventId, AmountPaid, Currency, PlanType, PaidAt, CreatedAt
                FROM UserPaymentHistory
                WHERE UserId = @UserId
                ORDER BY PaidAt DESC;";

            return await _connection.QueryAsync<UserPaymentHistory>(sql, new { UserId = userId });
        }
    }
}
