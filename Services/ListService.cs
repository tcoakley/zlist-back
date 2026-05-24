using zListBack.Models;
using zListBack.Dtos;
using zListBack.Repositories;
using zListBack.Mappers;

namespace zListBack.Services
{
    public class ListService
    {
        private readonly ListRepository _listRepository;

        public ListService(ListRepository listRepository)
        {
            _listRepository = listRepository;
        }

        public async Task<Result<ListModel>> AddList(ListModel listModel, int userId)
        {
            try
            {
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

        public async Task<Result<ListRunModel>> CreateListRun(int listId)
        {
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

        public async Task<Result<List<ListRunHistoryModel>>> GetListRunHistory(int listId)
        {
            return await _listRepository.GetListRunHistory(listId);
        }

        // ─── Shared list methods ────────────────────────────────────────────────────

        public async Task<Result<List<ListMemberModel>>> GetListMembers(int listId, int userId)
        {
            // Verify the requesting user is a member of the list
            var listResult = await _listRepository.GetList(listId, userId);
            if (!listResult.Success)
                return Result<List<ListMemberModel>>.Fail("List not found.");

            return await _listRepository.GetListMembers(listId);
        }

        /// <summary>Creates an invitation and returns the invite token on success.</summary>
        public async Task<Result<string>> InviteToList(int listId, int invitingUserId, string email)
        {
            // Verify requester is the owner
            var listResult = await _listRepository.GetList(listId, invitingUserId);
            if (!listResult.Success || listResult.Model == null)
                return Result<string>.Fail("List not found.");

            if (!listResult.Model.IsOwner)
                return Result<string>.Fail("Only the list owner can invite members.");

            var token = Guid.NewGuid().ToString("N");
            var invitation = new ListInvitation
            {
                ListId = listId,
                InvitedByUserId = invitingUserId,
                InvitedEmail = email.Trim().ToLower(),
                Token = token,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            };

            var result = await _listRepository.CreateListInvitation(invitation);
            if (!result.Success)
                return Result<string>.Fail(result.Message ?? "Failed to create invitation.");

            return Result<string>.Ok(token);
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
                IsExpired = inv.ExpiresAt < DateTime.UtcNow
            };

            return Result<ListInvitationInfoModel>.Ok(model);
        }

        public async Task<Result<bool>> AcceptListInvitation(string token, int userId)
        {
            return await _listRepository.AcceptListInvitation(token, userId);
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
