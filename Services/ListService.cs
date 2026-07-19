using Microsoft.Extensions.Logging;
using zListBack.Models;
using zListBack.Dtos;
using zListBack.Repositories;
using zListBack.Mappers;

namespace zListBack.Services
{
    public class ListService
    {
        private readonly ListRepository _listRepository;
        private readonly SubscriptionService _subscriptionService;
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly ILogger<ListService> _logger;
        private readonly EmailService _emailService;
        private readonly IUserRepository _userRepo;

        public ListService(ListRepository listRepository, SubscriptionService subscriptionService, ISubscriptionRepository subscriptionRepository, ILogger<ListService> logger, EmailService emailService, IUserRepository userRepo)
        {
            _listRepository = listRepository;
            _subscriptionService = subscriptionService;
            _subscriptionRepository = subscriptionRepository;
            _logger = logger;
            _emailService = emailService;
            _userRepo = userRepo;
        }

        public async Task<Result<ListModel>> AddList(ListModel listModel, int userId)
        {
            try
            {
                if (!await _subscriptionService.CanCreateList(userId))
                    return Result<ListModel>.Fail("Free accounts are limited to 2 checklists. Upgrade to Premium for unlimited lists.");

                var listEntity = ListMapper.ToEntity(listModel);
                listEntity.CreatedAt = DateTime.UtcNow;
                listEntity.UpdatedAt = DateTime.UtcNow;

                var result = await _listRepository.AddList(listEntity, userId);
                if (!result.Success || result.Model == null)
                    return Result<ListModel>.Fail(result.Message ?? "Failed to add list.");

                var resultDto = ListMapper.ToModel(result.Model);
                return Result<ListModel>.Ok(resultDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AddList failed. UserId={UserId}, ListName={ListName}", userId, listModel.ListName);
                return Result<ListModel>.Fail(ex.Message);
            }
        }

        public async Task<Result<ListItemModel>> AddListItem(ListItemModel model)
        {
            var entity = ListItemMapper.ToEntity(model);
            var result = await _listRepository.AddListItem(entity);

            return result.Success && result.Model != null
                ? Result<ListItemModel>.Ok(ListItemMapper.ToModel(result.Model))
                : Result<ListItemModel>.Fail(result.Message ?? "Failed to add list item.");
        }

        public async Task<Result<ListModel>> GetList(int id, int userId)
        {
            var result = await _listRepository.GetList(id, userId);
            return result.Success && result.Model != null
                ? Result<ListModel>.Ok(ListMapper.ToModel(result.Model))
                : Result<ListModel>.Fail(result.Message ?? "List not found.");
        }

        public async Task<Result<List<ListModel>>> GetLists(int userId)
        {
            var result = await _listRepository.GetLists(userId);
            return result.Success && result.Model != null
                ? Result<List<ListModel>>.Ok(result.Model.Select(ListMapper.ToModel).ToList())
                : Result<List<ListModel>>.Fail(result.Message ?? "Failed to retrieve lists.");
        }

        public async Task<Result<ListRunModel>> CreateListRun(int listId, int userId)
        {
            await _userRepo.UpdateLastActiveAt(userId);
            var result = await _listRepository.CreateListRun(listId);
            return result.Success && result.Model != null
                ? Result<ListRunModel>.Ok(ListRunMapper.ToModel(result.Model))
                : Result<ListRunModel>.Fail(result.Message ?? "Failed to create list run.");
        }

        public async Task<Result<bool>> CompleteListRun(int runId, int userId)
        {
            return await _listRepository.CompleteListRun(runId, userId);
        }

        public async Task<Result<bool>> SetListRunItemCompletion(int runItemId, bool isComplete, int userId)
        {
            await _userRepo.UpdateLastActiveAt(userId);
            return await _listRepository.SetListRunItemCompletion(runItemId, isComplete, userId);
        }

        public async Task<Result<ListRunModel>> GetListRun(int runId)
        {
            var result = await _listRepository.GetListRun(runId);
            return result.Success && result.Model != null
                ? Result<ListRunModel>.Ok(ListRunMapper.ToModel(result.Model))
                : Result<ListRunModel>.Fail(result.Message ?? "List run not found.");
        }

        public async Task<Result<List<ListRunModel>>> GetListRuns(int listId)
        {
            var result = await _listRepository.GetListRuns(listId);
            return result.Success && result.Model != null
                ? Result<List<ListRunModel>>.Ok(result.Model.Select(ListRunMapper.ToModel).ToList())
                : Result<List<ListRunModel>>.Fail(result.Message ?? "Failed to get list runs.");
        }

        public async Task<Result<ListRunItemModel>> AddListRunItem(int listRunId, ListItemModel model, bool oneTime)
        {
            var itemEntity = ListItemMapper.ToEntity(model);

            var result = await _listRepository.AddListRunItem(listRunId, itemEntity, oneTime);
            return result.Success && result.Model != null
                ? Result<ListRunItemModel>.Ok(ListRunItemMapper.ToModel(result.Model))
                : Result<ListRunItemModel>.Fail(result.Message ?? "Failed to add list run item.");
        }

        public async Task<Result<bool>> EditList(ListModel model)
        {
            var entity = ListMapper.ToEntity(model);
            return await _listRepository.EditList(entity);
        }

        public async Task<Result<bool>> EditListItem(ListItemModel model)
        {
            var entity = ListItemMapper.ToEntity(model);
            return await _listRepository.EditListItem(entity);
        }

        public async Task<Result<bool>> DeleteListItem(int itemId)
        {
            return await _listRepository.DeleteListItem(itemId);
        }

        public async Task<Result<bool>> DeleteList(int listId, int userId)
        {
            return await _listRepository.DeleteList(listId, userId);
        }

        public async Task<Result<bool>> DeleteListRun(int runId, int userId)
        {
            return await _listRepository.DeleteListRun(runId, userId);
        }

        public async Task<Result<List<ListRunHistoryModel>>> GetListRunHistory(int listId)
        {
            return await _listRepository.GetListRunHistory(listId);
        }

        // === Shared list methods ====================================================

        public async Task<Result<List<ListMemberModel>>> GetListMembers(int listId, int userId)
        {
            var listResult = await _listRepository.GetList(listId, userId);
            if (!listResult.Success)
                return Result<List<ListMemberModel>>.Fail("List not found.");

            return await _listRepository.GetListMembers(listId);
        }

        public async Task<Result<List<ListMemberModel>>> GetKnownCollaborators(int userId)
        {
            return await _listRepository.GetKnownCollaborators(userId);
        }

        public async Task<Result<List<ListPendingInviteModel>>> GetPendingInvitations(int listId, int userId)
        {
            var listResult = await _listRepository.GetList(listId, userId);
            if (!listResult.Success)
                return Result<List<ListPendingInviteModel>>.Fail("List not found.");

            return await _listRepository.GetPendingInvitations(listId);
        }

        /// <summary>
        /// Creates an invitation. Returns RequiresSponsor=true when the inviting user needs to
        /// confirm sponsorship before the invite can be sent as a normal (non-premium-required) invite.
        /// SponsorConfirmed=null: evaluate and return status.
        /// SponsorConfirmed=true: create sponsorship then normal invite.
        /// SponsorConfirmed=false: send RequiresPremium invite without sponsorship.
        /// </summary>
        public async Task<Result<InviteResultModel>> InviteToList(int listId, int invitingUserId, string email, bool? sponsorConfirmed)
        {
            var listResult = await _listRepository.GetList(listId, invitingUserId);
            if (!listResult.Success || listResult.Model == null)
                return Result<InviteResultModel>.Fail("List not found.");

            if (!listResult.Model.IsOwner)
                return Result<InviteResultModel>.Fail("Only the list owner can invite members.");

            if (!await _subscriptionService.IsPremium(invitingUserId))
                return Result<InviteResultModel>.Fail("Sharing lists requires a Premium account.");

            var normalizedEmail = email.Trim().ToLower();

            // Check if invitee has their own Premium
            var invitee = await _subscriptionRepository.GetUserByEmail(normalizedEmail);
            var inviteeIsPremium = invitee != null && await _subscriptionService.IsPremium(invitee.Id);

            // Determine if sponsorship is needed: invitee is not premium AND inviting user's free slot is taken
            bool requiresSponsor = false;
            if (!inviteeIsPremium && invitee != null)
            {
                var alreadySponsored = await _subscriptionService.IsAlreadySponsored(invitingUserId, invitee.Id);
                if (!alreadySponsored)
                {
                    var isFirstSlot = await _subscriptionService.IsFirstCollaboratorSlot(invitingUserId);
                    requiresSponsor = !isFirstSlot;
                }
            }

            // If sponsorship is needed and caller hasn't confirmed yet, return the prompt signal
            if (requiresSponsor && sponsorConfirmed == null)
                return Result<InviteResultModel>.Ok(new InviteResultModel { RequiresSponsor = true, ListName = listResult.Model.ListName });

            // Create sponsorship if confirmed
            bool isPremiumRequired = false;
            if (requiresSponsor)
            {
                if (sponsorConfirmed == true && invitee != null)
                    await _subscriptionService.SponsorCollaborator(invitingUserId, invitee.Id);
                else
                    isPremiumRequired = true;
            }
            else if (!inviteeIsPremium && invitee != null)
            {
                // First free collaborator slot — create sponsorship record automatically (no Stripe charge)
                var alreadySponsored = await _subscriptionService.IsAlreadySponsored(invitingUserId, invitee.Id);
                if (!alreadySponsored)
                    await _subscriptionService.SponsorCollaborator(invitingUserId, invitee.Id);
            }

            var token = Guid.NewGuid().ToString("N");
            var invitation = new ListInvitation
            {
                ListId = listId,
                InvitedByUserId = invitingUserId,
                InvitedEmail = normalizedEmail,
                Token = token,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                RequiresPremium = isPremiumRequired
            };

            var result = await _listRepository.CreateListInvitation(invitation);
            if (!result.Success)
                return Result<InviteResultModel>.Fail(result.Message ?? "Failed to create invitation.");

            return Result<InviteResultModel>.Ok(new InviteResultModel
            {
                RequiresSponsor = false,
                RequiresPremiumEmail = isPremiumRequired,
                Token = token,
                ListName = listResult.Model.ListName,
                Message = isPremiumRequired
                    ? "Invitation sent. The recipient will need a Premium account to accept."
                    : null
            });
        }

        public async Task<Result<InviteResultModel>> InviteToListAndNotify(int listId, int userId, string email, bool? sponsorConfirmed, string appBaseUrl, string inviterName)
        {
            var result = await InviteToList(listId, userId, email, sponsorConfirmed);
            if (!result.Success || result.Model == null || result.Model.RequiresSponsor)
                return result;

            if (result.Model.RequiresPremiumEmail)
                await _emailService.SendPremiumRequiredInvitationEmail(email, result.Model.ListName ?? string.Empty, inviterName);
            else
                await _emailService.SendInvitationEmail(email, result.Model.ListName ?? string.Empty, appBaseUrl, result.Model.Token!);

            return result;
        }

        public async Task<Result<ListInvitationInfoModel>> GetListInvitation(string token)
        {
            var result = await _listRepository.GetListInvitation(token);
            if (!result.Success || result.Model == null)
                return Result<ListInvitationInfoModel>.Fail(result.Message ?? "Invitation not found.");

            var inv = result.Model;
            var model = new ListInvitationInfoModel
            {
                ListId = inv.ListId,
                ListName = inv.ListName ?? string.Empty,
                InvitedByName = $"{inv.InvitedByFirstName} {inv.InvitedByLastName}".Trim(),
                InvitedEmail = inv.InvitedEmail,
                Status = inv.Status,
                IsExpired = inv.ExpiresAt < DateTime.UtcNow,
                HasAccount = inv.HasAccount
            };

            return Result<ListInvitationInfoModel>.Ok(model);
        }

        public async Task<Result<bool>> AcceptListInvitation(string token, int userId)
        {
            return await _listRepository.AcceptListInvitation(token, userId);
        }

        public async Task<Result<IEnumerable<Dtos.UserPendingInvitationModel>>> GetPendingInvitationsForUser(string email)
        {
            return await _listRepository.GetPendingInvitationsForUser(email);
        }

        public async Task<Result<bool>> DeclineListInvitation(string token)
        {
            return await _listRepository.DeclineListInvitation(token);
        }

        public async Task<Result<bool>> RemoveListMember(int listId, int requestingUserId, int memberUserId)
        {
            return await _listRepository.RemoveListMember(listId, requestingUserId, memberUserId);
        }

        public async Task<Result<bool>> LeaveList(int listId, int userId)
        {
            return await _listRepository.LeaveList(listId, userId);
        }
    }
}
