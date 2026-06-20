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

        private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        public SubscriptionController(SubscriptionService subscriptionService, ISubscriptionRepository subscriptionRepo, IUserRepository userRepo, ListRepository listRepo)
        {
            _subscriptionService = subscriptionService;
            _subscriptionRepo = subscriptionRepo;
            _userRepo = userRepo;
            _listRepo = listRepo;
        }

        // === Account status ==========================================================

        [HttpGet("status")]
        public async Task<Result<SubscriptionStatusModel>> GetStatus()
        {
            var status = await _subscriptionService.GetSubscriptionStatus(UserId);
            return status != null
                ? Result<SubscriptionStatusModel>.Ok(status)
                : Result<SubscriptionStatusModel>.Fail("User not found.");
        }

        // === Upgrade / Cancel ========================================================

        [HttpPost("upgrade")]
        public async Task<Result<UpgradeResponse>> Upgrade()
        {
            var userId = UserId;
            var userResult = await _userRepo.GetUserAsync(userId);
            if (!userResult.Success || userResult.Model == null)
                return Result<UpgradeResponse>.Fail("User not found.");

            if (userResult.Model.SubscriptionSource == "stripe")
                return Result<UpgradeResponse>.Fail("Account is already on a paid subscription.");

            return await _subscriptionService.Upgrade(userId, userResult.Model.Email);
        }

        [HttpPost("cancel")]
        public async Task<Result<bool>> Cancel()
        {
            var userId = UserId;
            var isPremium = await _subscriptionService.IsPremium(userId);
            if (!isPremium)
                return Result<bool>.Fail("No active premium subscription to cancel.");

            return await _subscriptionService.Cancel(userId);
        }

        // === Downgrade list selection =================================================

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
            if (request.KeepListIds.Count > 2)
                return Result<bool>.Fail("You can only keep 2 lists on the free plan.");

            await _listRepo.ArchiveUnselectedLists(UserId, request.KeepListIds);
            return Result<bool>.Ok(true);
        }

        // === Sponsored collaborators =================================================

        [HttpGet("collaborators")]
        public async Task<Result<IEnumerable<SponsoredCollaboratorModel>>> GetCollaborators()
        {
            var userId = UserId;
            if (!await _subscriptionService.IsPremium(userId))
                return Result<IEnumerable<SponsoredCollaboratorModel>>.Fail("Premium subscription required.");

            var collaborators = await _subscriptionService.GetSponsoredCollaborators(userId);
            return Result<IEnumerable<SponsoredCollaboratorModel>>.Ok(collaborators);
        }

        [HttpPost("collaborators")]
        public async Task<Result<bool>> AddCollaborator([FromBody] AddCollaboratorRequest request)
        {
            var sponsorId = UserId;
            if (!await _subscriptionService.IsPremium(sponsorId))
                return Result<bool>.Fail("Premium subscription required to sponsor collaborators.");

            return await _subscriptionService.AddFreeCollaboratorByEmail(sponsorId, request.Email);
        }

        [HttpGet("collaborators/pending")]
        public async Task<Result<IEnumerable<PendingSponsorInvitationModel>>> GetPendingInvitations()
        {
            var userId = UserId;
            if (!await _subscriptionService.IsPremium(userId))
                return Result<IEnumerable<PendingSponsorInvitationModel>>.Fail("Premium subscription required.");

            var pending = await _subscriptionService.GetPendingSponsorInvitations(userId);
            return Result<IEnumerable<PendingSponsorInvitationModel>>.Ok(pending);
        }

        [HttpDelete("collaborators/pending/{email}")]
        public async Task<Result<bool>> CancelPendingInvitation(string email)
        {
            return await _subscriptionService.CancelPendingSponsorInvitation(UserId, Uri.UnescapeDataString(email));
        }

        [HttpGet("collaborators/check")]
        public async Task<Result<CollaboratorCheckModel>> CheckCollaborator([FromQuery] string email)
        {
            var userId = UserId;
            if (!await _subscriptionService.IsPremium(userId))
                return Result<CollaboratorCheckModel>.Fail("Premium subscription required.");

            var check = await _subscriptionService.CheckCollaboratorPremiumStatus(userId, email);
            return Result<CollaboratorCheckModel>.Ok(check);
        }

        [HttpPost("collaborators/paid")]
        public async Task<Result<bool>> AddPaidCollaborator([FromBody] AddCollaboratorRequest request)
        {
            var sponsorId = UserId;
            if (!await _subscriptionService.IsPremium(sponsorId))
                return Result<bool>.Fail("Premium subscription required.");

            return await _subscriptionService.AddPaidCollaboratorByEmail(sponsorId, request.Email);
        }

        [HttpDelete("collaborators/{collaboratorUserId:int}")]
        public async Task<Result<bool>> RemoveCollaborator(int collaboratorUserId)
        {
            var sponsorId = UserId;

            if (!await _subscriptionService.IsPremium(sponsorId))
                return Result<bool>.Fail("Premium subscription required.");

            if (!await _subscriptionService.IsAlreadySponsored(sponsorId, collaboratorUserId))
                return Result<bool>.Fail("That user is not one of your sponsored collaborators.");

            await _subscriptionService.RemoveSponsoredCollaborator(sponsorId, collaboratorUserId);
            return Result<bool>.Ok(true);
        }

        // === Payment history =========================================================

        [HttpGet("payment-history")]
        public async Task<Result<IEnumerable<UserPaymentHistory>>> GetPaymentHistory()
        {
            var history = await _subscriptionService.GetPaymentHistory(UserId);
            return Result<IEnumerable<UserPaymentHistory>>.Ok(history);
        }

        // === Admin endpoints =========================================================

        [HttpGet("admin/status")]
        public async Task<Result<SubscriptionStatusModel>> AdminGetStatus([FromQuery] string email)
        {
            if (!await IsAdmin()) return Result<SubscriptionStatusModel>.Fail("Unauthorized.");

            var user = await _subscriptionRepo.GetUserByEmail(email);
            if (user == null) return Result<SubscriptionStatusModel>.Fail("No account found with that email.");

            var status = await _subscriptionService.GetSubscriptionStatus(user.Id);
            return status != null
                ? Result<SubscriptionStatusModel>.Ok(status)
                : Result<SubscriptionStatusModel>.Fail("User not found.");
        }

        [HttpPost("admin/grant")]
        public async Task<Result<bool>> AdminGrantPremium([FromBody] AdminGrantPremiumRequest request)
        {
            if (!await IsAdmin()) return Result<bool>.Fail("Unauthorized.");

            var validSources = new[] { "gift", "admin" };
            if (!validSources.Contains(request.Source))
                return Result<bool>.Fail("Invalid source. Use 'gift' or 'admin'.");

            return await _subscriptionService.AdminGrantPremium(request.Email, request.Source, request.ExpiresAt);
        }

        [HttpPost("admin/revoke")]
        public async Task<Result<bool>> AdminRevokePremium([FromBody] AdminRevokeRequest request)
        {
            if (!await IsAdmin()) return Result<bool>.Fail("Unauthorized.");

            return await _subscriptionService.AdminRevokePremium(request.Email);
        }

        private async Task<bool> IsAdmin()
        {
            var result = await _userRepo.GetUserAsync(UserId);
            return result.Success && result.Model?.IsAdmin == true;
        }
    }
}
