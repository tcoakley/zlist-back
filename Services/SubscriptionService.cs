using zListBack.Models;
using zListBack.Repositories;

namespace zListBack.Services
{
    public class SubscriptionService
    {
        private const int FreeListLimit = 2;
        private const int GraceDays = 7;

        private readonly SubscriptionRepository _subscriptionRepo;
        private readonly UserRepository _userRepo;

        public SubscriptionService(SubscriptionRepository subscriptionRepo, UserRepository userRepo)
        {
            _subscriptionRepo = subscriptionRepo;
            _userRepo = userRepo;
        }

        public Task<bool> IsPremium(int userId) =>
            _subscriptionRepo.IsPremium(userId);

        public async Task<bool> CanCreateList(int userId)
        {
            if (await IsPremium(userId)) return true;
            var count = await _subscriptionRepo.GetOwnedListCount(userId);
            return count < FreeListLimit;
        }

        public Task<int> GetOwnedListCount(int userId) =>
            _subscriptionRepo.GetOwnedListCount(userId);

        // Returns true if this is the sponsor's first collaborator (included in base price).
        // Returns false if they already have one — adding another will require a Stripe seat charge.
        public async Task<bool> IsFirstCollaboratorSlot(int sponsorUserId)
        {
            var count = await _subscriptionRepo.GetActiveSponsoredCount(sponsorUserId);
            return count == 0;
        }

        public Task<bool> IsAlreadySponsored(int sponsorUserId, int sponsoredUserId) =>
            _subscriptionRepo.HasActiveSponsoredCollaborator(sponsorUserId, sponsoredUserId);

        // Called when a Premium user confirms they want to sponsor a collaborator.
        // If this is the second or later collaborator, also calls the Stripe stub to add a seat.
        public async Task<Result<bool>> SponsorCollaborator(int sponsorUserId, int sponsoredUserId)
        {
            try
            {
                var isFirst = await IsFirstCollaboratorSlot(sponsorUserId);
                await _subscriptionRepo.AddSponsoredCollaborator(sponsorUserId, sponsoredUserId);

                if (!isFirst)
                    await AddStripeCollaboratorSeat(sponsorUserId); // TODO: Stripe

                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail(ex.Message);
            }
        }

        // Called when a sponsor removes a collaborator or their subscription lapses.
        // Applies a 7-day grace, then evaluates whether the collaborator retains shared list access.
        public async Task RemoveSponsoredCollaborator(int sponsorUserId, int sponsoredUserId)
        {
            var graceUntil = DateTime.UtcNow.AddDays(GraceDays);
            await _subscriptionRepo.StartSponsorshipGrace(sponsorUserId, sponsoredUserId, graceUntil);
            await RemoveStripeCollaboratorSeat(sponsorUserId); // TODO: Stripe
        }

        // Called after grace period expires for a sponsored collaborator.
        // Revokes shared list access if the sponsor's free slot is already taken by someone else.
        public async Task FinalizeCollaboratorDowngrade(int sponsorUserId, int sponsoredUserId)
        {
            await _subscriptionRepo.DeactivateSponsoredCollaborator(sponsorUserId, sponsoredUserId);
            var freeCount = await _subscriptionRepo.GetFreeCollaboratorCount(sponsorUserId);
            if (freeCount > 0)
                await _subscriptionRepo.RevokeSharedListAccess(sponsorUserId, sponsoredUserId);
            // else: collaborator becomes the sponsor's free slot — access retained
        }

        // Called when a Premium user's subscription lapses (failed payment after grace, or cancellation at period end).
        public async Task HandleSponsorLapse(int sponsorUserId)
        {
            var graceUntil = DateTime.UtcNow.AddDays(GraceDays);
            await _subscriptionRepo.StartAllSponsorshipsGrace(sponsorUserId, graceUntil);
            await _subscriptionRepo.SetGracePeriod(sponsorUserId, graceUntil);
        }

        // Called when a Premium user cancels entirely (account deletion or full cancel).
        // All collaborators get 7-day grace, then lose shared list access.
        public async Task HandleSponsorCancellation(int sponsorUserId)
        {
            var graceUntil = DateTime.UtcNow.AddDays(GraceDays);
            await _subscriptionRepo.StartAllSponsorshipsGrace(sponsorUserId, graceUntil);
            await _subscriptionRepo.SetGracePeriod(sponsorUserId, graceUntil);
        }

        // Called after the grace period ends for a cancelled sponsor.
        public async Task FinalizeSponsorCancellation(int sponsorUserId)
        {
            await _subscriptionRepo.DeactivateAllSponsorships(sponsorUserId);
            await _subscriptionRepo.RevokeAllSharedListAccess(sponsorUserId);
            await _subscriptionRepo.SetUserSubscription(sponsorUserId, "free", "free", null);
        }

        // ─── Admin / Gift ────────────────────────────────────────────────────────────

        public async Task<Result<bool>> GrantPremium(string email, string source, DateTime? expiresAt)
        {
            try
            {
                var user = await _subscriptionRepo.GetUserByEmail(email);
                if (user == null) return Result<bool>.Fail("User not found.");
                await _subscriptionRepo.SetUserSubscription(user.Id, "premium", source, expiresAt);
                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail(ex.Message);
            }
        }

        public async Task<Result<bool>> RevokePremium(string email)
        {
            try
            {
                var user = await _subscriptionRepo.GetUserByEmail(email);
                if (user == null) return Result<bool>.Fail("User not found.");
                await _subscriptionRepo.SetUserSubscription(user.Id, "free", "free", null);
                await _subscriptionRepo.DeactivateAllSponsorships(user.Id);
                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail(ex.Message);
            }
        }

        // ─── Stripe stubs — wire up once Stripe account is ready ─────────────────────

        private Task CreateStripeCustomer(int userId, string email)
        {
            // TODO: Stripe — call Stripe.CustomerService.CreateAsync, persist customer ID via SetStripeIds
            return Task.CompletedTask;
        }

        private Task CreateStripeSubscription(int userId)
        {
            // TODO: Stripe — create subscription with base price ID ($1.99/month), persist subscription ID
            return Task.CompletedTask;
        }

        private Task AddStripeCollaboratorSeat(int sponsorUserId)
        {
            // TODO: Stripe — increment quantity on the collaborator seat price item on the sponsor's subscription
            return Task.CompletedTask;
        }

        private Task RemoveStripeCollaboratorSeat(int sponsorUserId)
        {
            // TODO: Stripe — decrement quantity on the collaborator seat price item
            return Task.CompletedTask;
        }

        private Task CancelStripeSubscription(int userId)
        {
            // TODO: Stripe — call Stripe.SubscriptionService.CancelAsync (at_period_end: true)
            return Task.CompletedTask;
        }
    }
}
