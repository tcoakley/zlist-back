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
        private readonly SubscriptionRepository _subscriptionRepo;
        private readonly UserRepository _userRepo;
        private readonly ListRepository _listRepo;

        private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        public SubscriptionController(SubscriptionService subscriptionService, SubscriptionRepository subscriptionRepo, UserRepository userRepo, ListRepository listRepo)
        {
            _subscriptionService = subscriptionService;
            _subscriptionRepo = subscriptionRepo;
            _userRepo = userRepo;
            _listRepo = listRepo;
        }

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

            return Result<SubscriptionStatusModel>.Ok(new SubscriptionStatusModel
            {
                Subscription = user.Subscription,
                SubscriptionSource = user.SubscriptionSource,
                ExpiresAt = user.SubscriptionExpiresAt,
                GracePeriodUntil = user.GracePeriodUntil,
                IsPremium = isPremium,
                IsSponsored = isSponsored,
                OwnedListCount = ownedCount,
                OwnedListLimit = isPremium ? -1 : 2
            });
        }

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

        // ─── Admin endpoints ─────────────────────────────────────────────────────────

        [HttpPost("admin/grant")]
        public async Task<Result<bool>> AdminGrantPremium([FromBody] AdminGrantPremiumRequest request)
        {
            if (!await IsAdmin()) return Result<bool>.Fail("Unauthorized.");
            var validSources = new[] { "gift", "admin" };
            if (!validSources.Contains(request.Source))
                return Result<bool>.Fail("Invalid source. Use 'gift' or 'admin'.");

            return await _subscriptionService.GrantPremium(request.Email, request.Source, request.ExpiresAt);
        }

        [HttpPost("admin/revoke")]
        public async Task<Result<bool>> AdminRevokePremium([FromBody] AdminRevokeRequest request)
        {
            if (!await IsAdmin()) return Result<bool>.Fail("Unauthorized.");
            return await _subscriptionService.RevokePremium(request.Email);
        }

        private async Task<bool> IsAdmin()
        {
            var result = await _userRepo.GetUserAsync(UserId);
            return result.Success && result.Model?.IsAdmin == true;
        }
    }
}
