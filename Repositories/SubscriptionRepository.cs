using System.Data;
using Dapper;
using zListBack.Dtos;
using zListBack.Models;

namespace zListBack.Repositories
{
    public class SubscriptionRepository : ISubscriptionRepository
    {
        private readonly IDbConnection _connection;

        public SubscriptionRepository(IDbConnection connection)
        {
            _connection = connection;
        }

        public async Task<bool> IsPremium(int userId)
        {
            const string sql = @"
                SELECT CASE WHEN
                    (u.Subscription = 'premium' AND (u.GracePeriodUntil IS NULL OR u.GracePeriodUntil > GETUTCDATE()))
                    OR EXISTS (
                        SELECT 1 FROM SponsoredCollaborators sc
                        WHERE sc.SponsoredUserId = @UserId
                          AND sc.IsActive = 1
                          AND (sc.GraceUntil IS NULL OR sc.GraceUntil > GETUTCDATE())
                    )
                THEN 1 ELSE 0 END
                FROM Users u WHERE u.Id = @UserId;";

            return await _connection.ExecuteScalarAsync<bool>(sql, new { UserId = userId });
        }

        public async Task<int> GetOwnedListCount(int userId)
        {
            const string sql = @"
                SELECT COUNT(*) FROM UserLists WHERE UserId = @UserId AND IsOwner = 1;";
            return await _connection.ExecuteScalarAsync<int>(sql, new { UserId = userId });
        }

        // Returns the number of unique non-premium collaborators currently on this sponsor's lists.
        // The first slot is included in Premium; each beyond that costs $1/month via Stripe.
        public async Task<int> GetActiveSponsoredCount(int sponsorUserId)
        {
            const string sql = @"
                SELECT COUNT(*) FROM SponsoredCollaborators
                WHERE SponsorUserId = @SponsorUserId AND IsActive = 1
                  AND (GraceUntil IS NULL OR GraceUntil > GETUTCDATE());";
            return await _connection.ExecuteScalarAsync<int>(sql, new { SponsorUserId = sponsorUserId });
        }

        public async Task<bool> HasActiveSponsoredCollaborator(int sponsorUserId, int sponsoredUserId)
        {
            const string sql = @"
                SELECT COUNT(1) FROM SponsoredCollaborators
                WHERE SponsorUserId = @SponsorUserId AND SponsoredUserId = @SponsoredUserId
                  AND IsActive = 1 AND (GraceUntil IS NULL OR GraceUntil > GETUTCDATE());";
            return await _connection.ExecuteScalarAsync<bool>(sql, new { SponsorUserId = sponsorUserId, SponsoredUserId = sponsoredUserId });
        }

        public async Task AddSponsoredCollaborator(int sponsorUserId, int sponsoredUserId)
        {
            const string sql = @"
                IF NOT EXISTS (SELECT 1 FROM SponsoredCollaborators WHERE SponsorUserId = @SponsorUserId AND SponsoredUserId = @SponsoredUserId)
                    INSERT INTO SponsoredCollaborators (SponsorUserId, SponsoredUserId, CreatedAt, IsActive)
                    VALUES (@SponsorUserId, @SponsoredUserId, GETUTCDATE(), 1)
                ELSE
                    UPDATE SponsoredCollaborators
                    SET IsActive = 1, GraceUntil = NULL
                    WHERE SponsorUserId = @SponsorUserId AND SponsoredUserId = @SponsoredUserId;";
            await _connection.ExecuteAsync(sql, new { SponsorUserId = sponsorUserId, SponsoredUserId = sponsoredUserId });
        }

        public async Task StartSponsorshipGrace(int sponsorUserId, int sponsoredUserId, DateTime graceUntil)
        {
            const string sql = @"
                UPDATE SponsoredCollaborators
                SET GraceUntil = @GraceUntil
                WHERE SponsorUserId = @SponsorUserId AND SponsoredUserId = @SponsoredUserId;";
            await _connection.ExecuteAsync(sql, new { SponsorUserId = sponsorUserId, SponsoredUserId = sponsoredUserId, GraceUntil = graceUntil });
        }

        public async Task DeactivateSponsoredCollaborator(int sponsorUserId, int sponsoredUserId)
        {
            const string sql = @"
                UPDATE SponsoredCollaborators SET IsActive = 0
                WHERE SponsorUserId = @SponsorUserId AND SponsoredUserId = @SponsoredUserId;";
            await _connection.ExecuteAsync(sql, new { SponsorUserId = sponsorUserId, SponsoredUserId = sponsoredUserId });
        }

        // When a sponsor's account lapses, start grace on all their sponsored collaborators.
        public async Task StartAllSponsorshipsGrace(int sponsorUserId, DateTime graceUntil)
        {
            const string sql = @"
                UPDATE SponsoredCollaborators
                SET GraceUntil = @GraceUntil
                WHERE SponsorUserId = @SponsorUserId AND IsActive = 1 AND GraceUntil IS NULL;";
            await _connection.ExecuteAsync(sql, new { SponsorUserId = sponsorUserId, GraceUntil = graceUntil });
        }

        public async Task DeactivateAllSponsorships(int sponsorUserId)
        {
            const string sql = @"
                UPDATE SponsoredCollaborators SET IsActive = 0
                WHERE SponsorUserId = @SponsorUserId;";
            await _connection.ExecuteAsync(sql, new { SponsorUserId = sponsorUserId });
        }

        public async Task SetUserSubscription(int userId, string subscription, string source, DateTime? expiresAt)
        {
            const string sql = @"
                UPDATE Users
                SET Subscription = @Subscription, SubscriptionSource = @Source,
                    SubscriptionExpiresAt = @ExpiresAt, GracePeriodUntil = NULL, UpdatedAt = GETUTCDATE()
                WHERE Id = @UserId;";
            await _connection.ExecuteAsync(sql, new { UserId = userId, Subscription = subscription, Source = source, ExpiresAt = expiresAt });
        }

        public async Task SetStripeIds(int userId, string stripeCustomerId, string stripeSubscriptionId)
        {
            const string sql = @"
                UPDATE Users SET StripeCustomerId = @CustomerId, StripeSubscriptionId = @SubscriptionId, UpdatedAt = GETUTCDATE()
                WHERE Id = @UserId;";
            await _connection.ExecuteAsync(sql, new { UserId = userId, CustomerId = stripeCustomerId, SubscriptionId = stripeSubscriptionId });
        }

        public async Task SetGracePeriod(int userId, DateTime? graceUntil)
        {
            const string sql = @"
                UPDATE Users SET GracePeriodUntil = @GraceUntil, UpdatedAt = GETUTCDATE()
                WHERE Id = @UserId;";
            await _connection.ExecuteAsync(sql, new { UserId = userId, GraceUntil = graceUntil });
        }

        // Returns the count of non-premium (free) collaborators currently on this sponsor's shared lists.
        // Used to determine if the sponsor's included free slot is occupied when a sponsorship ends.
        public async Task<int> GetFreeCollaboratorCount(int sponsorUserId)
        {
            const string sql = @"
                SELECT COUNT(DISTINCT ul.UserId)
                FROM UserLists ul
                INNER JOIN UserLists ownerUl ON ownerUl.ListId = ul.ListId AND ownerUl.UserId = @SponsorUserId AND ownerUl.IsOwner = 1
                INNER JOIN Users u ON u.Id = ul.UserId
                WHERE ul.UserId != @SponsorUserId
                  AND u.Subscription != 'premium';";
            return await _connection.ExecuteScalarAsync<int>(sql, new { SponsorUserId = sponsorUserId });
        }

        // Remove a specific user's access to all lists owned by the sponsor.
        public async Task RevokeSharedListAccess(int sponsorUserId, int collaboratorUserId)
        {
            const string sql = @"
                DELETE ul FROM UserLists ul
                INNER JOIN UserLists ownerUl ON ownerUl.ListId = ul.ListId AND ownerUl.UserId = @SponsorUserId AND ownerUl.IsOwner = 1
                WHERE ul.UserId = @CollaboratorUserId AND ul.IsOwner = 0;";
            await _connection.ExecuteAsync(sql, new { SponsorUserId = sponsorUserId, CollaboratorUserId = collaboratorUserId });
        }

        // Remove ALL non-owner members from all lists owned by the sponsor.
        public async Task RevokeAllSharedListAccess(int sponsorUserId)
        {
            const string sql = @"
                DELETE ul FROM UserLists ul
                INNER JOIN UserLists ownerUl ON ownerUl.ListId = ul.ListId AND ownerUl.UserId = @SponsorUserId AND ownerUl.IsOwner = 1
                WHERE ul.UserId != @SponsorUserId AND ul.IsOwner = 0;";
            await _connection.ExecuteAsync(sql, new { SponsorUserId = sponsorUserId });
        }

        public async Task<User?> GetSponsor(int sponsoredUserId)
        {
            const string sql = @"
                SELECT TOP 1 u.FirstName, u.LastName
                FROM SponsoredCollaborators sc
                INNER JOIN Users u ON u.Id = sc.SponsorUserId
                WHERE sc.SponsoredUserId = @SponsoredUserId AND sc.IsActive = 1
                ORDER BY sc.CreatedAt;";
            return await _connection.QuerySingleOrDefaultAsync<User>(sql, new { SponsoredUserId = sponsoredUserId });
        }

        public async Task<IEnumerable<SponsoredCollaboratorModel>> GetSponsoredCollaborators(int sponsorUserId)
        {
            const string sql = @"
                SELECT sc.SponsoredUserId AS UserId, u.Email, u.FirstName, u.LastName,
                       sc.CreatedAt, sc.IsActive, sc.GraceUntil
                FROM SponsoredCollaborators sc
                INNER JOIN Users u ON u.Id = sc.SponsoredUserId
                WHERE sc.SponsorUserId = @SponsorUserId
                ORDER BY sc.CreatedAt;";
            return await _connection.QueryAsync<SponsoredCollaboratorModel>(sql, new { SponsorUserId = sponsorUserId });
        }

        public async Task<bool> NeedsDowngradeSelection(int userId)
        {
            var isPremium = await IsPremium(userId);
            if (isPremium) return false;

            const string sql = @"
                SELECT COUNT(*)
                FROM Lists l
                INNER JOIN UserLists ul ON ul.ListId = l.Id AND ul.UserId = @UserId AND ul.IsOwner = 1
                WHERE l.IsArchived = 0;";
            var count = await _connection.ExecuteScalarAsync<int>(sql, new { UserId = userId });
            return count > 2;
        }

        // ─── Daily cleanup queries ────────────────────────────────────────────────

        public async Task<IEnumerable<User>> GetInactivePremiumUsers(int inactiveDays)
        {
            const string sql = @"
                SELECT Id, Email, FirstName, LastName, Subscription, SubscriptionExpiresAt,
                       StripeCustomerId, StripeSubscriptionId, InactivityNoticeSentAt, LastActiveAt
                FROM Users
                WHERE Subscription = 'premium'
                  AND SubscriptionSource = 'stripe'
                  AND InactivityNoticeSentAt IS NULL
                  AND (LastActiveAt IS NULL OR LastActiveAt < DATEADD(DAY, -@InactiveDays, GETUTCDATE()));";
            return await _connection.QueryAsync<User>(sql, new { InactiveDays = inactiveDays });
        }

        public async Task SetInactivityNoticeSent(int userId)
        {
            const string sql = @"
                UPDATE Users SET InactivityNoticeSentAt = GETUTCDATE() WHERE Id = @UserId;";
            await _connection.ExecuteAsync(sql, new { UserId = userId });
        }

        public async Task<IEnumerable<(User Sponsor, IEnumerable<User> Collaborators)>> GetSponsorsWithInactiveCollaborators(int inactiveDays)
        {
            const string sql = @"
                SELECT
                    s.Id, s.Email, s.FirstName, s.LastName,
                    s.SubscriptionExpiresAt, s.InactivityNoticeSentAt,
                    c.Id AS CollabId, c.Email AS CollabEmail,
                    c.FirstName AS CollabFirstName, c.LastName AS CollabLastName,
                    c.LastActiveAt AS CollabLastActiveAt
                FROM SponsoredCollaborators sc
                INNER JOIN Users s ON s.Id = sc.SponsorUserId
                INNER JOIN Users c ON c.Id = sc.SponsoredUserId
                WHERE sc.IsActive = 1
                  AND s.Subscription = 'premium'
                  AND s.SubscriptionSource = 'stripe'
                  AND (c.LastActiveAt IS NULL OR c.LastActiveAt < DATEADD(DAY, -@InactiveDays, GETUTCDATE()))
                ORDER BY s.Id;";

            var rows = await _connection.QueryAsync<dynamic>(sql, new { InactiveDays = inactiveDays });

            return rows
                .GroupBy(r => (int)r.Id)
                .Select(g =>
                {
                    var first = g.First();
                    var sponsor = new User
                    {
                        Id = (int)first.Id,
                        Email = (string)first.Email,
                        FirstName = (string?)first.FirstName,
                        LastName = (string?)first.LastName,
                        SubscriptionExpiresAt = (DateTime?)first.SubscriptionExpiresAt
                    };
                    var collaborators = g.Select(r => new User
                    {
                        Id = (int)r.CollabId,
                        Email = (string)r.CollabEmail,
                        FirstName = (string?)r.CollabFirstName,
                        LastName = (string?)r.CollabLastName,
                        LastActiveAt = (DateTime?)r.CollabLastActiveAt
                    });
                    return (Sponsor: sponsor, Collaborators: collaborators);
                });
        }

        public async Task<IEnumerable<User>> GetUsersWithBillingInDays(int days)
        {
            const string sql = @"
                SELECT Id, Email, FirstName, LastName, SubscriptionExpiresAt, BillingReminderSentAt
                FROM Users
                WHERE Subscription = 'premium'
                  AND SubscriptionSource = 'stripe'
                  AND SubscriptionExpiresAt IS NOT NULL
                  AND CAST(SubscriptionExpiresAt AS DATE) = CAST(DATEADD(DAY, @Days, GETUTCDATE()) AS DATE)
                  AND (BillingReminderSentAt IS NULL
                       OR CAST(BillingReminderSentAt AS DATE) < CAST(DATEADD(DAY, -25, GETUTCDATE()) AS DATE));";
            return await _connection.QueryAsync<User>(sql, new { Days = days });
        }

        public async Task SetBillingReminderSent(int userId, DateTime periodEnd)
        {
            const string sql = @"
                UPDATE Users SET BillingReminderSentAt = GETUTCDATE() WHERE Id = @UserId;";
            await _connection.ExecuteAsync(sql, new { UserId = userId });
        }

        public async Task<IEnumerable<(User Sponsor, User Collaborator)>> GetCollaboratorsWhoUpgradedThemselves()
        {
            const string sql = @"
                SELECT
                    s.Id, s.Email, s.FirstName, s.LastName,
                    c.Id AS CollabId, c.Email AS CollabEmail,
                    c.FirstName AS CollabFirstName, c.LastName AS CollabLastName
                FROM SponsoredCollaborators sc
                INNER JOIN Users s ON s.Id = sc.SponsorUserId
                INNER JOIN Users c ON c.Id = sc.SponsoredUserId
                WHERE sc.IsActive = 1
                  AND c.Subscription = 'premium'
                  AND c.SubscriptionSource = 'stripe';";

            var rows = await _connection.QueryAsync<dynamic>(sql);
            return rows.Select(r => (
                Sponsor: new User
                {
                    Id = (int)r.Id,
                    Email = (string)r.Email,
                    FirstName = (string?)r.FirstName,
                    LastName = (string?)r.LastName
                },
                Collaborator: new User
                {
                    Id = (int)r.CollabId,
                    Email = (string)r.CollabEmail,
                    FirstName = (string?)r.CollabFirstName,
                    LastName = (string?)r.CollabLastName
                }
            ));
        }

        public async Task<User?> GetUserByStripeCustomerId(string stripeCustomerId)
        {
            const string sql = @"
                SELECT Id, Email, FirstName, LastName, Subscription, SubscriptionExpiresAt,
                       SubscriptionSource, StripeCustomerId, StripeSubscriptionId, GracePeriodUntil, IsAdmin
                FROM Users WHERE StripeCustomerId = @StripeCustomerId;";
            return await _connection.QuerySingleOrDefaultAsync<User>(sql, new { StripeCustomerId = stripeCustomerId });
        }

        public async Task<User?> GetUserByEmail(string email)
        {
            const string sql = @"
                SELECT Id, Email, FirstName, LastName, Subscription, SubscriptionExpiresAt,
                       SubscriptionSource, StripeCustomerId, StripeSubscriptionId, GracePeriodUntil, IsAdmin
                FROM Users WHERE LOWER(Email) = LOWER(@Email);";
            return await _connection.QuerySingleOrDefaultAsync<User>(sql, new { Email = email });
        }
    }
}
