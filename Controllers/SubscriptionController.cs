using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using zListBack.Dtos;
using zListBack.Models;
using zListBack.Repositories;
using zListBack.Services;

namespace zListBack.Controllers
{
    [ApiController]
    [Route("api/subscription")]
    [Authorize]
    public class SubscriptionController : ControllerBase
    {
        private readonly SubscriptionService _subscriptionService;
        private readonly ISubscriptionRepository _subscriptionRepo;
        private readonly IUserRepository _userRepo;
        private readonly ListRepository _listRepo;
        private readonly EmailService _emailService;

        private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        public SubscriptionController(SubscriptionService subscriptionService, ISubscriptionRepository subscriptionRepo, IUserRepository userRepo, ListRepository listRepo, EmailService emailService)
        {
            _subscriptionService = subscriptionService;
            _subscriptionRepo = subscriptionRepo;
            _userRepo = userRepo;
            _listRepo = listRepo;
            _emailService = emailService;
        }

        // ─── Account status ──────────────────────────────────────────────────────────

        [HttpGet("status")]
        public async Task<Result<SubscriptionStatusModel>> GetStatus()
        {
            var userId = UserId;
            var userResult = await _userRepo.GetUserAsync(userId);
            if (!userResult.Success || userResult.Model == null)
                return Result<SubscriptionStatusModel>.Fail("User not found.");

            var user = userResult.Model;
            var isPremium = await _subscriptionService.IsPremium(userId);
            var isSponsored = isPremium && user.Subscription != "premium";
            var ownedCount = await _subscriptionRepo.GetOwnedListCount(userId);

            string? sponsorName = null;
            if (isSponsored)
            {
                var sponsor = await _subscriptionRepo.GetSponsor(userId);
                if (sponsor != null)
                {
                    var last = string.IsNullOrWhiteSpace(sponsor.LastName) ? "" : $" {sponsor.LastName[0]}.";
                    sponsorName = $"{sponsor.FirstName}{last}".Trim();
                }
            }

            return Result<SubscriptionStatusModel>.Ok(new SubscriptionStatusModel
            {
                Subscription = user.Subscription,
                SubscriptionSource = user.SubscriptionSource,
                ExpiresAt = user.SubscriptionExpiresAt,
                GracePeriodUntil = user.GracePeriodUntil,
                IsPremium = isPremium,
                IsSponsored = isSponsored,
                SponsorName = sponsorName,
                OwnedListCount = ownedCount,
                OwnedListLimit = isPremium ? -1 : 2
            });
        }

        // ─── Upgrade / Cancel ────────────────────────────────────────────────────────

        /// <summary>
        /// Upgrades the current user from free to premium.
        /// TODO: Stripe — this stub simulates a successful subscription.
        /// Replace with a real Stripe Checkout session redirect or PaymentIntent flow
        /// once the Stripe account and price IDs are configured.
        /// </summary>
        [HttpPost("upgrade")]
        public async Task<Result<UpgradeResponse>> Upgrade()
        {
            var userId = UserId;
            var userResult = await _userRepo.GetUserAsync(userId);
            if (!userResult.Success || userResult.Model == null)
                return Result<UpgradeResponse>.Fail("User not found.");

            var isPremium = await _subscriptionService.IsPremium(userId);
            if (isPremium)
                return Result<UpgradeResponse>.Fail("Account is already premium.");

            return await _subscriptionService.Upgrade(userId, userResult.Model.Email);
        }

        /// <summary>
        /// Cancels the current user's premium subscription.
        /// TODO: Stripe — this stub simulates an immediate cancellation.
        /// With real Stripe, call CancelAsync(at_period_end: true) so the user keeps
        /// access until the period ends; the webhook fires customer.subscription.deleted
        /// at that point and HandleSponsorLapse is called from there instead.
        /// </summary>
        [HttpPost("cancel")]
        public async Task<Result<bool>> Cancel()
        {
            var userId = UserId;
            var isPremium = await _subscriptionService.IsPremium(userId);
            if (!isPremium)
                return Result<bool>.Fail("No active premium subscription to cancel.");

            return await _subscriptionService.Cancel(userId);
        }

        // ─── Downgrade list selection ─────────────────────────────────────────────────

        [HttpGet("needs-selection")]
        public async Task<Result<SelectionStatusModel>> NeedsSelection()
        {
            var userId = UserId;
            var needsSelection = await _subscriptionRepo.NeedsDowngradeSelection(userId);
            if (!needsSelection)
                return Result<SelectionStatusModel>.Ok(new SelectionStatusModel { NeedsSelection = false });

            var lists = await _listRepo.GetOwnedListsForSelection(userId);
            return Result<SelectionStatusModel>.Ok(new SelectionStatusModel
            {
                NeedsSelection = true,
                AllowedCount = 2,
                Lists = lists.Select(l => new SelectionListItem
                {
                    Id = l.Id,
                    ListName = l.ListName,
                    TotalItems = l.TotalItems,
                    IsArchived = l.IsArchived
                }).ToList()
            });
        }

        [HttpPost("select-lists")]
        public async Task<Result<bool>> SelectLists([FromBody] SelectListsRequest request)
        {
            var userId = UserId;
            if (request.KeepListIds.Count > 2)
                return Result<bool>.Fail("You can only keep 2 lists on the free plan.");

            await _listRepo.ArchiveUnselectedLists(userId, request.KeepListIds);
            return Result<bool>.Ok(true);
        }

        // ─── Sponsored collaborators ─────────────────────────────────────────────────

        /// <summary>
        /// Returns all users the current user is sponsoring (free slot + paid seats).
        /// </summary>
        [HttpGet("collaborators")]
        public async Task<Result<IEnumerable<SponsoredCollaboratorModel>>> GetCollaborators()
        {
            var userId = UserId;
            var isPremium = await _subscriptionService.IsPremium(userId);
            if (!isPremium)
                return Result<IEnumerable<SponsoredCollaboratorModel>>.Fail("Premium subscription required.");

            var collaborators = await _subscriptionService.GetSponsoredCollaborators(userId);
            return Result<IEnumerable<SponsoredCollaboratorModel>>.Ok(collaborators);
        }

        /// <summary>
        /// Adds a collaborator by email. The first collaborator is included in the base
        /// premium price; each additional one requires a Stripe seat charge.
        /// TODO: Stripe — when IsFirstCollaboratorSlot is false, AddStripeCollaboratorSeat
        /// must succeed before the collaborator record is written. The stub in
        /// SubscriptionService.SponsorCollaborator currently skips the Stripe call.
        /// </summary>
        [HttpPost("collaborators")]
        public async Task<Result<bool>> AddCollaborator([FromBody] AddCollaboratorRequest request)
        {
            var sponsorId = UserId;

            var isPremium = await _subscriptionService.IsPremium(sponsorId);
            if (!isPremium)
                return Result<bool>.Fail("Premium subscription required to sponsor collaborators.");

            var targetUser = await _subscriptionRepo.GetUserByEmail(request.Email);
            if (targetUser == null)
                return Result<bool>.Fail("No account found with that email address.");

            if (targetUser.Id == sponsorId)
                return Result<bool>.Fail("You cannot sponsor yourself.");

            var alreadySponsored = await _subscriptionService.IsAlreadySponsored(sponsorId, targetUser.Id);
            if (alreadySponsored)
                return Result<bool>.Fail("That user is already one of your sponsored collaborators.");

            var isFreeSlot = await _subscriptionService.IsFirstCollaboratorSlot(sponsorId);

            var result = await _subscriptionService.SponsorCollaborator(sponsorId, targetUser.Id);
            if (!result.Success) return result;

            var sponsorResult = await _userRepo.GetUserAsync(sponsorId);
            if (sponsorResult.Success && sponsorResult.Model != null)
            {
                var sponsor = sponsorResult.Model;
                var sponsorName = $"{sponsor.FirstName} {sponsor.LastName}".Trim();
                var collaboratorFirstName = targetUser.FirstName ?? targetUser.Email;
                _ = _emailService.SendCollaboratorAddedEmail(targetUser.Email, collaboratorFirstName, sponsorName, isFreeSlot);
            }

            return result;
        }

        /// <summary>
        /// Removes a sponsored collaborator. Starts a 7-day grace period before access is revoked.
        /// TODO: Stripe — RemoveStripeCollaboratorSeat must be called (and confirmed) before
        /// or alongside the grace period start. The stub currently skips the Stripe call.
        /// </summary>
        [HttpDelete("collaborators/{collaboratorUserId:int}")]
        public async Task<Result<bool>> RemoveCollaborator(int collaboratorUserId)
        {
            var sponsorId = UserId;

            var isPremium = await _subscriptionService.IsPremium(sponsorId);
            if (!isPremium)
                return Result<bool>.Fail("Premium subscription required.");

            var isSponsored = await _subscriptionService.IsAlreadySponsored(sponsorId, collaboratorUserId);
            if (!isSponsored)
                return Result<bool>.Fail("That user is not one of your sponsored collaborators.");

            var graceUntil = DateTime.UtcNow.AddDays(7);
            await _subscriptionService.RemoveSponsoredCollaborator(sponsorId, collaboratorUserId);

            var collaboratorResult = await _userRepo.GetUserAsync(collaboratorUserId);
            var sponsorResult = await _userRepo.GetUserAsync(sponsorId);
            if (collaboratorResult.Success && collaboratorResult.Model != null &&
                sponsorResult.Success && sponsorResult.Model != null)
            {
                var collaborator = collaboratorResult.Model;
                var sponsor = sponsorResult.Model;
                var sponsorName = $"{sponsor.FirstName} {sponsor.LastName}".Trim();
                var collaboratorFirstName = collaborator.FirstName ?? collaborator.Email;
                _ = _emailService.SendCollaboratorRemovedEmail(collaborator.Email, collaboratorFirstName, sponsorName, graceUntil);
            }

            return Result<bool>.Ok(true);
        }

        // ─── Payment history ─────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the current user's payment history, recorded from Stripe webhook events.
        /// TODO: Stripe — this table is populated by the invoice.payment_succeeded webhook
        /// (POST /api/stripe/webhook). Until Stripe is wired, this will return an empty list.
        /// </summary>
        [HttpGet("payment-history")]
        public async Task<Result<IEnumerable<UserPaymentHistory>>> GetPaymentHistory()
        {
            var history = await _subscriptionService.GetPaymentHistory(UserId);
            return Result<IEnumerable<UserPaymentHistory>>.Ok(history);
        }

        // ─── Admin endpoints ─────────────────────────────────────────────────────────

        [HttpGet("admin/status")]
        public async Task<Result<SubscriptionStatusModel>> AdminGetStatus([FromQuery] string email)
        {
            if (!await IsAdmin()) return Result<SubscriptionStatusModel>.Fail("Unauthorized.");

            var user = await _subscriptionRepo.GetUserByEmail(email);
            if (user == null) return Result<SubscriptionStatusModel>.Fail("No account found with that email.");

            var isPremium = await _subscriptionService.IsPremium(user.Id);
            var isSponsored = isPremium && user.Subscription != "premium";
            var ownedCount = await _subscriptionRepo.GetOwnedListCount(user.Id);

            string? sponsorName = null;
            if (isSponsored)
            {
                var sponsor = await _subscriptionRepo.GetSponsor(user.Id);
                if (sponsor != null)
                {
                    var last = string.IsNullOrWhiteSpace(sponsor.LastName) ? "" : $" {sponsor.LastName[0]}.";
                    sponsorName = $"{sponsor.FirstName}{last}".Trim();
                }
            }

            return Result<SubscriptionStatusModel>.Ok(new SubscriptionStatusModel
            {
                Subscription = user.Subscription,
                SubscriptionSource = user.SubscriptionSource,
                ExpiresAt = user.SubscriptionExpiresAt,
                GracePeriodUntil = user.GracePeriodUntil,
                IsPremium = isPremium,
                IsSponsored = isSponsored,
                SponsorName = sponsorName,
                OwnedListCount = ownedCount,
                OwnedListLimit = isPremium ? -1 : 2
            });
        }

        [HttpPost("admin/grant")]
        public async Task<Result<bool>> AdminGrantPremium([FromBody] AdminGrantPremiumRequest request)
        {
            if (!await IsAdmin()) return Result<bool>.Fail("Unauthorized.");
            var validSources = new[] { "gift", "admin" };
            if (!validSources.Contains(request.Source))
                return Result<bool>.Fail("Invalid source. Use 'gift' or 'admin'.");

            var user = await _subscriptionRepo.GetUserByEmail(request.Email);
            if (user == null) return Result<bool>.Fail("No account found with that email.");

            var result = await _subscriptionService.GrantPremium(request.Email, request.Source, request.ExpiresAt);
            if (result.Success)
            {
                var firstName = user.FirstName ?? user.Email;
                _ = _emailService.SendAdminGrantedEmail(user.Email, firstName, request.Source, request.ExpiresAt);
            }
            return result;
        }

        [HttpPost("admin/revoke")]
        public async Task<Result<bool>> AdminRevokePremium([FromBody] AdminRevokeRequest request)
        {
            if (!await IsAdmin()) return Result<bool>.Fail("Unauthorized.");

            var user = await _subscriptionRepo.GetUserByEmail(request.Email);
            if (user == null) return Result<bool>.Fail("No account found with that email.");

            var result = await _subscriptionService.RevokePremium(request.Email);
            if (result.Success)
            {
                var firstName = user.FirstName ?? user.Email;
                _ = _emailService.SendAdminRevokedEmail(user.Email, firstName);
            }
            return result;
        }

        private async Task<bool> IsAdmin()
        {
            var result = await _userRepo.GetUserAsync(UserId);
            return result.Success && result.Model?.IsAdmin == true;
        }
    }
}
