using Microsoft.Extensions.Logging;
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
        private readonly EmailService _emailService;
        private readonly StripeSettings _stripe;
        private readonly ILogger<SubscriptionService> _logger;

        public SubscriptionService(
            ISubscriptionRepository subscriptionRepo,
            IUserRepository userRepo,
            IUserPaymentHistoryRepository paymentHistoryRepo,
            ListRepository listRepo,
            EmailService emailService,
            IOptions<StripeSettings> stripeOptions,
            ILogger<SubscriptionService> logger)
        {
            _subscriptionRepo = subscriptionRepo;
            _userRepo = userRepo;
            _paymentHistoryRepo = paymentHistoryRepo;
            _listRepo = listRepo;
            _emailService = emailService;
            _stripe = stripeOptions.Value;
            _logger = logger;
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

        // Returns true if the sponsor's free slot (included in base price) is not yet occupied.
        public async Task<bool> IsFirstCollaboratorSlot(int sponsorUserId)
        {
            return !await _subscriptionRepo.HasActiveFreeSeatCollaborator(sponsorUserId);
        }

        public Task<bool> IsAlreadySponsored(int sponsorUserId, int sponsoredUserId) =>
            _subscriptionRepo.HasActiveSponsoredCollaborator(sponsorUserId, sponsoredUserId);

        // Adds a sponsored collaborator. First slot is free; second+ adds a Stripe seat.
        public async Task<Result<bool>> SponsorCollaborator(int sponsorUserId, int sponsoredUserId)
        {
            try
            {
                var isFirst = await IsFirstCollaboratorSlot(sponsorUserId);
                await _subscriptionRepo.AddSponsoredCollaborator(sponsorUserId, sponsoredUserId, isFreeSeat: true);

                if (!isFirst)
                    await AddStripeCollaboratorSeat(sponsorUserId);

                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SponsorCollaborator failed. SponsorUserId={SponsorUserId}, SponsoredUserId={SponsoredUserId}", sponsorUserId, sponsoredUserId);
                return Result<bool>.Fail(ex.Message);
            }
        }

        // Starts a 7-day grace for the removed collaborator, removes them from all sponsor-owned lists immediately,
        // removes the Stripe seat, and notifies the collaborator.
        public async Task RemoveSponsoredCollaborator(int sponsorUserId, int sponsoredUserId)
        {
            var graceUntil = DateTime.UtcNow.AddDays(GraceDays);
            await _subscriptionRepo.StartSponsorshipGrace(sponsorUserId, sponsoredUserId, graceUntil);
            await _subscriptionRepo.RevokeSharedListAccess(sponsorUserId, sponsoredUserId);
            await RemoveStripeCollaboratorSeat(sponsorUserId);

            var collaboratorResult = await _userRepo.GetUserAsync(sponsoredUserId);
            var sponsorResult = await _userRepo.GetUserAsync(sponsorUserId);
            if (collaboratorResult.Model != null && sponsorResult.Model != null)
            {
                var sponsorName = $"{sponsorResult.Model.FirstName} {sponsorResult.Model.LastName}".Trim();
                var collaboratorFirstName = collaboratorResult.Model.FirstName ?? collaboratorResult.Model.Email;
                _ = _emailService.SendCollaboratorRemovedEmail(collaboratorResult.Model.Email, collaboratorFirstName, sponsorName, graceUntil);
            }
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

        // === Free collaborator by email ==============================================

        public async Task<Result<bool>> AddFreeCollaboratorByEmail(int sponsorId, string email)
        {
            try
            {
                var hasFreeSlot = await IsFirstCollaboratorSlot(sponsorId);
                if (!hasFreeSlot)
                    return Result<bool>.Fail("Your free collaborator slot is already taken. Use the paid seat option to add more collaborators.");

                var sponsorResult = await _userRepo.GetUserAsync(sponsorId);
                var sponsorName = sponsorResult.Model != null
                    ? $"{sponsorResult.Model.FirstName} {sponsorResult.Model.LastName}".Trim()
                    : "Someone";

                var targetUser = await _subscriptionRepo.GetUserByEmail(email);
                if (targetUser == null)
                {
                    var existing = await _subscriptionRepo.GetPendingSponsorInvitationByEmail(email);
                    if (existing != null && existing.Value.SponsorUserId != sponsorId)
                        return Result<bool>.Fail("That email has already been invited by another sponsor and is awaiting signup.");

                    // The free slot never grants premium — it's a labeling/autocomplete convenience only.
                    return await InviteNewCollaborator(sponsorId, email, sponsorName, includesPremium: false);
                }

                if (targetUser.Id == sponsorId)
                    return Result<bool>.Fail("You cannot sponsor yourself.");

                var alreadySponsored = await IsAlreadySponsored(sponsorId, targetUser.Id);
                if (alreadySponsored)
                    return Result<bool>.Fail("That user is already one of your sponsored collaborators.");

                var result = await SponsorCollaborator(sponsorId, targetUser.Id);
                if (!result.Success) return result;

                var collaboratorFirstName = targetUser.FirstName ?? targetUser.Email;
                _ = _emailService.SendCollaboratorAddedEmail(targetUser.Email, collaboratorFirstName, sponsorName, isFreeSlot: true);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AddFreeCollaboratorByEmail failed. SponsorUserId={SponsorUserId}, Email={Email}", sponsorId, email);
                return Result<bool>.Fail(ex.Message);
            }
        }

        // === Paid collaborator by email ==============================================

        public async Task<Result<bool>> AddPaidCollaboratorByEmail(int sponsorId, string email)
        {
            try
            {
                var sponsorResult = await _userRepo.GetUserAsync(sponsorId);
                if (sponsorResult.Model?.SubscriptionSource != "stripe")
                    return Result<bool>.Fail("Paid collaborator seats require an active paid subscription. Sponsored and admin-granted accounts can add one free collaborator but cannot add paid seats.");

                var sponsorName = sponsorResult.Model != null
                    ? $"{sponsorResult.Model.FirstName} {sponsorResult.Model.LastName}".Trim()
                    : "Someone";

                var targetUser = await _subscriptionRepo.GetUserByEmail(email);

                if (targetUser == null)
                {
                    var existing = await _subscriptionRepo.GetPendingSponsorInvitationByEmail(email);
                    if (existing != null)
                        return Result<bool>.Fail("That email has a pending collaborator invitation from another user. They need to sign up first.");

                    var tempPassword = Convert.ToBase64String(Guid.NewGuid().ToByteArray())[..16];
                    var newUser = new Models.User { Email = email, Password = tempPassword };
                    var createResult = await _userRepo.AddUserAsync(newUser);
                    if (!createResult.Success || createResult.Model == null)
                        return Result<bool>.Fail("Could not create an account for that email address.");

                    targetUser = createResult.Model;
                    var result2 = await AddPaidCollaborator(sponsorId, targetUser.Id);
                    if (!result2.Success) return result2;

                    _ = _emailService.SendPaidSeatAccountCreatedEmail(email, sponsorName, tempPassword);
                    return result2;
                }

                if (targetUser.Id == sponsorId)
                    return Result<bool>.Fail("You cannot sponsor yourself.");

                var alreadySponsored = await IsAlreadySponsored(sponsorId, targetUser.Id);
                if (alreadySponsored)
                    return Result<bool>.Fail("That user is already one of your sponsored collaborators.");

                var result = await AddPaidCollaborator(sponsorId, targetUser.Id);
                if (!result.Success) return result;

                var name = targetUser.FirstName ?? targetUser.Email;
                _ = _emailService.SendCollaboratorAddedEmail(targetUser.Email, name, sponsorName, isFreeSlot: false);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AddPaidCollaboratorByEmail failed. SponsorUserId={SponsorUserId}, Email={Email}", sponsorId, email);
                return Result<bool>.Fail(ex.Message);
            }
        }

        // === Pending sponsor invitations ==============================================

        public async Task<Result<bool>> InviteNewCollaborator(int sponsorUserId, string email, string sponsorName, bool includesPremium = true)
        {
            try
            {
                var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
                    .Replace("+", "-").Replace("/", "_").Replace("=", "")[..22];
                var expiresAt = DateTime.UtcNow.AddDays(7);

                await _subscriptionRepo.CreatePendingSponsorInvitation(sponsorUserId, email, token, expiresAt);
                _ = _emailService.SendSponsorInvitationEmail(email, sponsorName, includesPremium);

                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "InviteNewCollaborator failed. SponsorUserId={SponsorUserId}, Email={Email}", sponsorUserId, email);
                return Result<bool>.Fail(ex.Message);
            }
        }

        public Task<IEnumerable<PendingSponsorInvitationModel>> GetPendingSponsorInvitations(int sponsorUserId) =>
            _subscriptionRepo.GetPendingSponsorInvitations(sponsorUserId);

        public async Task<Result<bool>> CancelPendingSponsorInvitation(int sponsorUserId, string email)
        {
            await _subscriptionRepo.DeletePendingSponsorInvitation(sponsorUserId, email);
            return Result<bool>.Ok(true);
        }

        public async Task ApplyPendingSponsorshipOnSignup(int newUserId, string email)
        {
            try
            {
                var pending = await _subscriptionRepo.GetPendingSponsorInvitationByEmail(email);
                if (pending == null) return;

                await _subscriptionRepo.AddSponsoredCollaborator(pending.Value.SponsorUserId, newUserId, isFreeSeat: true);
                await _subscriptionRepo.DeletePendingSponsorInvitationByEmail(email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ApplyPendingSponsorshipOnSignup failed. UserId={UserId}, Email={Email}", newUserId, email);
            }
        }

        // === Collaborator premium check ===============================================

        public async Task<CollaboratorCheckModel> CheckCollaboratorPremiumStatus(int sponsorUserId, string email)
        {
            var user = await _subscriptionRepo.GetUserByEmail(email);
            if (user == null)
                return new CollaboratorCheckModel { Exists = false };

            var isAlreadyYours = await IsAlreadySponsored(sponsorUserId, user.Id);
            var isPremium = await IsPremium(user.Id);

            bool isSponsoredByOther = false;
            string? premiumSource = null;

            if (isPremium)
            {
                premiumSource = user.SubscriptionSource;
                if (user.Subscription != "premium") // user is on sponsored/free but IsPremium returned true → sponsored
                {
                    premiumSource = "sponsored";
                    var sponsor = await _subscriptionRepo.GetSponsor(user.Id);
                    if (sponsor != null && sponsor.Id != sponsorUserId)
                        isSponsoredByOther = true;
                }
            }

            return new CollaboratorCheckModel
            {
                Exists = true,
                IsPremium = isPremium,
                PremiumSource = premiumSource,
                IsAlreadyYourCollaborator = isAlreadyYours,
                IsAlreadySponsoredByOther = isSponsoredByOther
            };
        }

        // === Paid collaborator seat (Stripe before DB) ================================

        public async Task<Result<bool>> AddPaidCollaborator(int sponsorUserId, int sponsoredUserId)
        {
            try
            {
                await AddStripeCollaboratorSeat(sponsorUserId);
                await _subscriptionRepo.AddSponsoredCollaborator(sponsorUserId, sponsoredUserId, isFreeSeat: false);
                await ReleaseFreeSeatIfSponsored(sponsoredUserId);
                return Result<bool>.Ok(true);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "AddPaidCollaborator Stripe failed. SponsorUserId={SponsorUserId}", sponsorUserId);
                return Result<bool>.Fail(ex.StripeError?.Message ?? ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AddPaidCollaborator failed. SponsorUserId={SponsorUserId}", sponsorUserId);
                return Result<bool>.Fail(ex.Message);
            }
        }

        // Called when a user is added as a paid seat — releases any free-slot sponsorship
        // they hold with another sponsor, since they are now covered by a paid seat.
        private async Task ReleaseFreeSeatIfSponsored(int sponsoredUserId)
        {
            try
            {
                var freeSeatSponsorId = await _subscriptionRepo.GetActiveFreeSeatSponsorId(sponsoredUserId);
                if (freeSeatSponsorId == null) return;

                var sponsorResult = await _userRepo.GetUserAsync(freeSeatSponsorId.Value);
                var collaboratorResult = await _userRepo.GetUserAsync(sponsoredUserId);

                await _subscriptionRepo.DeactivateSponsoredCollaborator(freeSeatSponsorId.Value, sponsoredUserId);

                if (sponsorResult.Model != null && collaboratorResult.Model != null)
                {
                    var collaboratorName = $"{collaboratorResult.Model.FirstName} {collaboratorResult.Model.LastName}".Trim();
                    if (string.IsNullOrEmpty(collaboratorName)) collaboratorName = collaboratorResult.Model.Email;
                    var sponsorFirstName = sponsorResult.Model.FirstName ?? sponsorResult.Model.Email;
                    _ = _emailService.SendFreeSeatReleasedEmail(sponsorResult.Model.Email, sponsorFirstName, collaboratorName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ReleaseFreeSeatIfSponsored failed. SponsoredUserId={SponsoredUserId}", sponsoredUserId);
            }
        }

        // === Collaborator self-upgrade ===============================================
        // Called from the webhook when a sponsored collaborator subscribes via Stripe.
        // Removes the Stripe seat from their sponsor (if paid) and deactivates the record.

        public async Task HandleCollaboratorSelfUpgrade(int userId)
        {
            try
            {
                var record = await _subscriptionRepo.GetActiveSponsorRecord(userId);
                if (record == null) return;

                var (sponsorId, isFreeSeat) = record.Value;

                var sponsorResult = await _userRepo.GetUserAsync(sponsorId);
                var collaboratorResult = await _userRepo.GetUserAsync(userId);

                if (!isFreeSeat)
                    await RemoveStripeCollaboratorSeat(sponsorId);

                await _subscriptionRepo.DeactivateSponsoredCollaborator(sponsorId, userId);

                if (sponsorResult.Model != null && collaboratorResult.Model != null)
                {
                    var collaboratorName = $"{collaboratorResult.Model.FirstName} {collaboratorResult.Model.LastName}".Trim();
                    if (string.IsNullOrEmpty(collaboratorName)) collaboratorName = collaboratorResult.Model.Email;
                    var sponsorFirstName = sponsorResult.Model.FirstName ?? sponsorResult.Model.Email;
                    _ = _emailService.SendCollaboratorUpgradedEmail(sponsorResult.Model.Email, sponsorFirstName, collaboratorName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HandleCollaboratorSelfUpgrade failed. UserId={UserId}", userId);
            }
        }

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
                _logger.LogError(ex, "Upgrade (Stripe) failed. UserId={UserId}, Email={Email}", userId, email);
                return Result<UpgradeResponse>.Fail(ex.StripeError?.Message ?? ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Upgrade failed. UserId={UserId}, Email={Email}", userId, email);
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
                _logger.LogError(ex, "Cancel (Stripe) failed. UserId={UserId}", userId);
                return Result<bool>.Fail(ex.StripeError?.Message ?? ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cancel failed. UserId={UserId}", userId);
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
                _logger.LogError(ex, "GrantPremium failed. Email={Email}, Source={Source}", email, source);
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
                _logger.LogError(ex, "RevokePremium failed. Email={Email}", email);
                return Result<bool>.Fail(ex.Message);
            }
        }

        // === Subscription status ======================================================

        public async Task<SubscriptionStatusModel?> GetSubscriptionStatus(int userId)
        {
            var userResult = await _userRepo.GetUserAsync(userId);
            if (!userResult.Success || userResult.Model == null) return null;

            var user = userResult.Model;
            var isPremium = await IsPremium(userId);
            var isSponsored = isPremium && user.Subscription != "premium";
            var ownedCount = await _subscriptionRepo.GetOwnedListCount(userId);

            string? sponsorName = null;
            if (isSponsored)
            {
                var sponsor = await _subscriptionRepo.GetSponsor(userId);
                if (sponsor != null)
                {
                    var last = string.IsNullOrWhiteSpace(sponsor.LastName) ? "" : $" {sponsor.LastName}";
                    sponsorName = $"{sponsor.FirstName}{last}".Trim();
                }
            }

            return new SubscriptionStatusModel
            {
                Subscription = user.Subscription,
                SubscriptionSource = user.SubscriptionSource,
                ExpiresAt = user.SubscriptionExpiresAt,
                GracePeriodUntil = user.GracePeriodUntil,
                CancellationScheduledAt = user.CancellationScheduledAt,
                IsPremium = isPremium,
                IsSponsored = isSponsored,
                SponsorName = sponsorName,
                OwnedListCount = ownedCount,
                OwnedListLimit = isPremium ? -1 : 2
            };
        }

        // === Admin / Gift (with email notification) ===================================

        public async Task<Result<bool>> AdminGrantPremium(string email, string source, DateTime? expiresAt)
        {
            try
            {
                var user = await _subscriptionRepo.GetUserByEmail(email);
                if (user == null) return Result<bool>.Fail("No account found with that email.");

                await _subscriptionRepo.SetUserSubscription(user.Id, "premium", source, expiresAt);
                await _listRepo.RestoreArchivedLists(user.Id);

                var firstName = user.FirstName ?? user.Email;
                _ = _emailService.SendAdminGrantedEmail(user.Email, firstName, source, expiresAt);

                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AdminGrantPremium failed. Email={Email}, Source={Source}", email, source);
                return Result<bool>.Fail(ex.Message);
            }
        }

        public async Task<Result<bool>> AdminRevokePremium(string email)
        {
            try
            {
                var user = await _subscriptionRepo.GetUserByEmail(email);
                if (user == null) return Result<bool>.Fail("No account found with that email.");

                await _subscriptionRepo.SetUserSubscription(user.Id, "free", "free", null);
                await _subscriptionRepo.DeactivateAllSponsorships(user.Id);

                var firstName = user.FirstName ?? user.Email;
                _ = _emailService.SendAdminRevokedEmail(user.Email, firstName);

                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AdminRevokePremium failed. Email={Email}", email);
                return Result<bool>.Fail(ex.Message);
            }
        }

        // === Stripe webhook handlers ==================================================

        public async Task HandleStripePaymentSucceeded(Invoice invoice, string stripeEventId)
        {
            if (string.IsNullOrEmpty(invoice.CustomerId)) return;

            var user = await _subscriptionRepo.GetUserByStripeCustomerId(invoice.CustomerId);
            if (user == null) return;

            var periodEnd = invoice.Lines?.Data?.FirstOrDefault()?.Period?.End;
            if (periodEnd.HasValue)
            {
                await _subscriptionRepo.SetUserSubscription(user.Id, "premium", "stripe", periodEnd.Value);
                await _listRepo.RestoreArchivedLists(user.Id);
                await HandleCollaboratorSelfUpgrade(user.Id);
            }

            await _subscriptionRepo.SetGracePeriod(user.Id, null!);
            await _subscriptionRepo.SetCancellationScheduled(user.Id, null);

            if (!string.IsNullOrEmpty(stripeEventId))
            {
                // Check before inserting — each renewal has a new event ID so AddAsync.Success
                // is true every month and cannot distinguish first payment from renewals.
                var existingHistory = await _paymentHistoryRepo.GetByUserIdAsync(user.Id);
                var isFirstPayment = !existingHistory.Any();

                await _paymentHistoryRepo.AddAsync(new UserPaymentHistory
                {
                    UserId = user.Id,
                    StripeEventId = stripeEventId,
                    AmountPaid = invoice.AmountPaid / 100m,
                    Currency = invoice.Currency ?? "usd",
                    PlanType = "premium",
                    PaidAt = invoice.StatusTransitions?.PaidAt ?? DateTime.UtcNow
                });

                if (isFirstPayment)
                {
                    var firstName = user.FirstName ?? user.Email;
                    _ = _emailService.SendSubscriptionActivatedEmail(user.Email, firstName);
                }
            }
        }

        public async Task HandleStripePaymentFailed(Invoice invoice)
        {
            if (string.IsNullOrEmpty(invoice.CustomerId)) return;

            var user = await _subscriptionRepo.GetUserByStripeCustomerId(invoice.CustomerId);
            if (user == null) return;

            await HandleSponsorLapse(user.Id);

            var gracePeriodUntil = DateTime.UtcNow.AddDays(GraceDays);
            var firstName = user.FirstName ?? user.Email;
            _ = _emailService.SendPaymentFailedEmail(user.Email, firstName, gracePeriodUntil);
        }

        public async Task HandleStripeSubscriptionDeleted(Subscription subscription)
        {
            if (string.IsNullOrEmpty(subscription.CustomerId)) return;

            var user = await _subscriptionRepo.GetUserByStripeCustomerId(subscription.CustomerId);
            if (user == null) return;

            var lastAccessDate = subscription.EndedAt ?? DateTime.UtcNow;
            await _subscriptionRepo.SetCancellationScheduled(user.Id, null);
            await HandleSponsorCancellation(user.Id);
            await FinalizeSponsorCancellation(user.Id);

            var firstName = user.FirstName ?? user.Email;
            _ = _emailService.SendSubscriptionCancelledEmail(user.Email, firstName, lastAccessDate);
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
            if (string.IsNullOrEmpty(subscriptionId))
                throw new InvalidOperationException("Paid collaborator seats require an active Stripe subscription. Admin-granted or gift premium accounts cannot add paid seats.");

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

        public async Task CancelSubscriptionImmediately(string subscriptionId)
        {
            var svc = new StripeSubscriptionSvc();
            await svc.CancelAsync(subscriptionId);
        }
    }
}
