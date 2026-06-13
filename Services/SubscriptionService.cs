using Microsoft.Extensions.Options;
using Stripe;
using zListBack.Configurations;
using zListBack.Dtos;
using zListBack.Models;
using zListBack.Repositories;
using StripeCustomerSvc = Stripe.CustomerService;
using StripeSubscriptionSvc = Stripe.SubscriptionService;
using StripeSubscriptionItemSvc = Stripe.SubscriptionItemService;

namespace zListBack.Services
{
    public class SubscriptionService
    {
        private const int FreeListLimit = 2;
        private const int GraceDays = 7;

        private readonly ISubscriptionRepository _subscriptionRepo;
        private readonly IUserRepository _userRepo;
        private readonly IUserPaymentHistoryRepository _paymentHistoryRepo;
        private readonly ListRepository _listRepo;
        private readonly StripeSettings _stripe;

        public SubscriptionService(
            ISubscriptionRepository subscriptionRepo,
            IUserRepository userRepo,
            IUserPaymentHistoryRepository paymentHistoryRepo,
            ListRepository listRepo,
            IOptions<StripeSettings> stripeOptions)
        {
            _subscriptionRepo = subscriptionRepo;
            _userRepo = userRepo;
            _paymentHistoryRepo = paymentHistoryRepo;
            _listRepo = listRepo;
            _stripe = stripeOptions.Value;
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
        public async Task<bool> IsFirstCollaboratorSlot(int sponsorUserId)
        {
            var count = await _subscriptionRepo.GetActiveSponsoredCount(sponsorUserId);
            return count == 0;
        }

        public Task<bool> IsAlreadySponsored(int sponsorUserId, int sponsoredUserId) =>
            _subscriptionRepo.HasActiveSponsoredCollaborator(sponsorUserId, sponsoredUserId);

        // Adds a sponsored collaborator. First slot is free; second+ adds a Stripe seat.
        public async Task<Result<bool>> SponsorCollaborator(int sponsorUserId, int sponsoredUserId)
        {
            try
            {
                var isFirst = await IsFirstCollaboratorSlot(sponsorUserId);
                await _subscriptionRepo.AddSponsoredCollaborator(sponsorUserId, sponsoredUserId);

                if (!isFirst)
                    await AddStripeCollaboratorSeat(sponsorUserId);

                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail(ex.Message);
            }
        }

        // Starts a 7-day grace for the removed collaborator, then removes the Stripe seat.
        public async Task RemoveSponsoredCollaborator(int sponsorUserId, int sponsoredUserId)
        {
            var graceUntil = DateTime.UtcNow.AddDays(GraceDays);
            await _subscriptionRepo.StartSponsorshipGrace(sponsorUserId, sponsoredUserId, graceUntil);
            await RemoveStripeCollaboratorSeat(sponsorUserId);
        }

        // Called after grace expires — deactivates the record and revokes list access if the free slot is taken.
        public async Task FinalizeCollaboratorDowngrade(int sponsorUserId, int sponsoredUserId)
        {
            await _subscriptionRepo.DeactivateSponsoredCollaborator(sponsorUserId, sponsoredUserId);
            var freeCount = await _subscriptionRepo.GetFreeCollaboratorCount(sponsorUserId);
            if (freeCount > 0)
                await _subscriptionRepo.RevokeSharedListAccess(sponsorUserId, sponsoredUserId);
            // else: collaborator slides into the free slot — access retained
        }

        // Called when a premium subscription lapses (failed payment after grace).
        public async Task HandleSponsorLapse(int sponsorUserId)
        {
            var graceUntil = DateTime.UtcNow.AddDays(GraceDays);
            await _subscriptionRepo.StartAllSponsorshipsGrace(sponsorUserId, graceUntil);
            await _subscriptionRepo.SetGracePeriod(sponsorUserId, graceUntil);
        }

        // Called when a subscription is cancelled (fires from webhook customer.subscription.deleted).
        public async Task HandleSponsorCancellation(int sponsorUserId)
        {
            var graceUntil = DateTime.UtcNow.AddDays(GraceDays);
            await _subscriptionRepo.StartAllSponsorshipsGrace(sponsorUserId, graceUntil);
            await _subscriptionRepo.SetGracePeriod(sponsorUserId, graceUntil);
        }

        // Called after the grace period ends — removes access and sets user to free.
        public async Task FinalizeSponsorCancellation(int sponsorUserId)
        {
            await _subscriptionRepo.DeactivateAllSponsorships(sponsorUserId);
            await _subscriptionRepo.RevokeAllSharedListAccess(sponsorUserId);
            await _subscriptionRepo.SetUserSubscription(sponsorUserId, "free", "free", null);
        }

        public Task<IEnumerable<SponsoredCollaboratorModel>> GetSponsoredCollaborators(int sponsorUserId) =>
            _subscriptionRepo.GetSponsoredCollaborators(sponsorUserId);

        public Task<IEnumerable<UserPaymentHistory>> GetPaymentHistory(int userId) =>
            _paymentHistoryRepo.GetByUserIdAsync(userId);

        // === Upgrade =================================================================
        // Creates a Stripe customer (if needed) and an incomplete subscription.
        // Returns the PaymentIntent clientSecret so the frontend can confirm payment
        // via the Stripe Payment Element.

        public async Task<Result<UpgradeResponse>> Upgrade(int userId, string email)
        {
            try
            {
                var userResult = await _userRepo.GetUserAsync(userId);
                if (!userResult.Success || userResult.Model == null)
                    return Result<UpgradeResponse>.Fail("User not found.");

                var user = userResult.Model;

                var customerId = string.IsNullOrEmpty(user.StripeCustomerId)
                    ? await CreateStripeCustomer(userId, email)
                    : user.StripeCustomerId;

                var (subscriptionId, clientSecret) = await CreateStripeSubscription(userId, customerId);

                return Result<UpgradeResponse>.Ok(new UpgradeResponse
                {
                    ClientSecret = clientSecret,
                    SubscriptionId = subscriptionId
                });
            }
            catch (StripeException ex)
            {
                return Result<UpgradeResponse>.Fail(ex.StripeError?.Message ?? ex.Message);
            }
            catch (Exception ex)
            {
                return Result<UpgradeResponse>.Fail(ex.Message);
            }
        }

        // === Cancel ==================================================================
        // Sets cancel_at_period_end = true in Stripe. The user keeps access until the
        // current period ends. The webhook customer.subscription.deleted fires at that
        // point and calls HandleSponsorCancellation / FinalizeSponsorCancellation.

        public async Task<Result<bool>> Cancel(int userId)
        {
            try
            {
                var userResult = await _userRepo.GetUserAsync(userId);
                if (!userResult.Success || userResult.Model == null)
                    return Result<bool>.Fail("User not found.");

                var subscriptionId = userResult.Model.StripeSubscriptionId;
                if (string.IsNullOrEmpty(subscriptionId))
                    return Result<bool>.Fail("No active Stripe subscription found.");

                await CancelStripeSubscription(subscriptionId);
                await _subscriptionRepo.SetCancellationScheduled(userId, userResult.Model.SubscriptionExpiresAt);

                return Result<bool>.Ok(true);
            }
            catch (StripeException ex)
            {
                return Result<bool>.Fail(ex.StripeError?.Message ?? ex.Message);
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail(ex.Message);
            }
        }

        // === Admin / Gift =============================================================

        public async Task<Result<bool>> GrantPremium(string email, string source, DateTime? expiresAt)
        {
            try
            {
                var user = await _subscriptionRepo.GetUserByEmail(email);
                if (user == null) return Result<bool>.Fail("User not found.");
                await _subscriptionRepo.SetUserSubscription(user.Id, "premium", source, expiresAt);
                await _listRepo.RestoreArchivedLists(user.Id);
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

        // === Stripe private methods ===================================================

        private async Task<string> CreateStripeCustomer(int userId, string email)
        {
            var svc = new StripeCustomerSvc();
            var customer = await svc.CreateAsync(new CustomerCreateOptions
            {
                Email = email,
                Metadata = new Dictionary<string, string> { ["userId"] = userId.ToString() }
            });
            await _subscriptionRepo.SetStripeIds(userId, customer.Id, string.Empty);
            return customer.Id;
        }

        private async Task<(string SubscriptionId, string ClientSecret)> CreateStripeSubscription(int userId, string customerId)
        {
            var svc = new StripeSubscriptionSvc();
            var subscription = await svc.CreateAsync(new SubscriptionCreateOptions
            {
                Customer = customerId,
                Items = new List<SubscriptionItemOptions>
                {
                    new() { Price = _stripe.PremiumPriceId }
                },
                PaymentBehavior = "default_incomplete",
                PaymentSettings = new SubscriptionPaymentSettingsOptions
                {
                    SaveDefaultPaymentMethod = "on_subscription"
                },
                Expand = new List<string> { "latest_invoice", "latest_invoice.confirmation_secret" }
            });

            await _subscriptionRepo.SetStripeIds(userId, customerId, subscription.Id);

            // Stripe.net v52+ uses Invoice.ConfirmationSecret (replaces PaymentIntent.ClientSecret)
            var clientSecret = subscription.LatestInvoice?.ConfirmationSecret?.ClientSecret
                ?? throw new InvalidOperationException("Stripe did not return a confirmation secret.");

            return (subscription.Id, clientSecret);
        }

        private async Task AddStripeCollaboratorSeat(int sponsorUserId)
        {
            var userResult = await _userRepo.GetUserAsync(sponsorUserId);
            var subscriptionId = userResult.Model?.StripeSubscriptionId;
            if (string.IsNullOrEmpty(subscriptionId)) return;

            var subSvc = new StripeSubscriptionSvc();
            var subscription = await subSvc.GetAsync(subscriptionId, new SubscriptionGetOptions
            {
                Expand = new List<string> { "items" }
            });

            var seatItem = subscription.Items.Data
                .FirstOrDefault(i => i.Price.Id == _stripe.CollaboratorPriceId);

            var itemSvc = new StripeSubscriptionItemSvc();
            if (seatItem != null)
            {
                await itemSvc.UpdateAsync(seatItem.Id, new SubscriptionItemUpdateOptions
                {
                    Quantity = seatItem.Quantity + 1
                });
            }
            else
            {
                await itemSvc.CreateAsync(new SubscriptionItemCreateOptions
                {
                    Subscription = subscriptionId,
                    Price = _stripe.CollaboratorPriceId,
                    Quantity = 1
                });
            }
        }

        private async Task RemoveStripeCollaboratorSeat(int sponsorUserId)
        {
            var userResult = await _userRepo.GetUserAsync(sponsorUserId);
            var subscriptionId = userResult.Model?.StripeSubscriptionId;
            if (string.IsNullOrEmpty(subscriptionId)) return;

            var subSvc = new StripeSubscriptionSvc();
            var subscription = await subSvc.GetAsync(subscriptionId, new SubscriptionGetOptions
            {
                Expand = new List<string> { "items" }
            });

            var seatItem = subscription.Items.Data
                .FirstOrDefault(i => i.Price.Id == _stripe.CollaboratorPriceId);
            if (seatItem == null) return;

            var itemSvc = new StripeSubscriptionItemSvc();
            if (seatItem.Quantity <= 1)
                await itemSvc.DeleteAsync(seatItem.Id);
            else
                await itemSvc.UpdateAsync(seatItem.Id, new SubscriptionItemUpdateOptions
                {
                    Quantity = seatItem.Quantity - 1
                });
        }

        private async Task CancelStripeSubscription(string subscriptionId)
        {
            var svc = new StripeSubscriptionSvc();
            await svc.UpdateAsync(subscriptionId, new SubscriptionUpdateOptions
            {
                CancelAtPeriodEnd = true
            });
        }
    }
}
