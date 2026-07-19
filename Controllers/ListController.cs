using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using zListBack.Dtos;
using zListBack.Hubs;
using zListBack.Models;
using zListBack.Services;

namespace zListBack.Controllers
{
    [ApiController]
    [Route("api/lists")]
    [Authorize]
    public class ListController : ControllerBase
    {
        private readonly ListService _listService;
        private readonly IHubContext<RunHub> _hub;
        private readonly string _appBaseUrl;
        private readonly int _userId;

        public ListController(ListService listService, IHubContext<RunHub> hub, IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        {
            _listService = listService;
            _hub = hub;
            _appBaseUrl = configuration["AppSettings:BaseUrl"] ?? "https://localhost:4200";

            var userIdClaim = httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out _userId))
            {
                throw new UnauthorizedAccessException("User ID not found in token.");
            }
        }

        [HttpGet("GetLists")]
        public async Task<Result<List<ListModel>>> GetLists()
        {
            return await _listService.GetLists(_userId);
        }

        [HttpGet("GetList/{listId}")]
        public async Task<Result<ListModel>> GetList(int listId)
        {
            return await _listService.GetList(listId, _userId);
        }

        [HttpPost("AddList")]
        public async Task<Result<ListModel>> AddList([FromBody] ListModel listModel)
        {
            return await _listService.AddList(listModel, _userId);
        }

        [HttpPost("AddListItem")]
        public async Task<Result<ListItemModel>> AddListItem([FromBody] ListItemModel itemModel)
        {
            return await _listService.AddListItem(itemModel);
        }

        [HttpPost("AddRunItem")]
        public async Task<Result<ListRunItemModel>> AddRunItem([FromBody] AddRunItemRequest request)
        {
            var model = new ListItemModel
            {
                ListId = request.ListId,
                ItemName = request.ItemName,
                SortOrder = 9999
            };
            var result = await _listService.AddListRunItem(request.ListRunId, model, request.OneTime);
            if (result.Success && result.Model != null)
                await _hub.Clients.Group($"run-{request.ListRunId}").SendAsync("ItemAdded", result.Model);
            return result;
        }

        [HttpPut("EditList")]
        public async Task<Result<bool>> EditList([FromBody] ListModel listModel)
        {
            return await _listService.EditList(listModel);
        }

        [HttpPut("EditListItem")]
        public async Task<Result<bool>> EditListItem([FromBody] ListItemModel itemModel)
        {
            return await _listService.EditListItem(itemModel);
        }

        [HttpDelete("DeleteListItem/{itemId}")]
        public async Task<Result<bool>> DeleteListItem(int itemId)
        {
            return await _listService.DeleteListItem(itemId);
        }

        [HttpDelete("DeleteList/{listId}")]
        public async Task<Result<bool>> DeleteList(int listId)
        {
            return await _listService.DeleteList(listId, _userId);
        }

        [HttpDelete("DeleteListRun/{runId}")]
        public async Task<Result<bool>> DeleteListRun(int runId)
        {
            var result = await _listService.DeleteListRun(runId, _userId);
            if (result.Success)
                await _hub.Clients.Group($"run-{runId}").SendAsync("RunDeleted");
            return result;
        }

        [HttpPut("CompleteListRun/{runId}")]
        public async Task<Result<bool>> CompleteListRun(int runId)
        {
            var result = await _listService.CompleteListRun(runId, _userId);
            if (result.Success)
                await _hub.Clients.Group($"run-{runId}").SendAsync("RunCompleted");
            return result;
        }

        [HttpPut("SetListRunItemCompletion/{runItemId}")]
        public async Task<Result<bool>> SetListRunItemCompletion(int runItemId, [FromBody] ToggleRunItemRequest request)
        {
            var result = await _listService.SetListRunItemCompletion(runItemId, request.IsComplete, _userId);
            if (result.Success)
            {
                var initials = RunHub.GetUserInitials(_userId);
                var displayName = RunHub.GetUserDisplayName(_userId);
                await _hub.Clients.Group($"run-{request.RunId}")
                    .SendAsync("ItemToggled", runItemId, request.IsComplete, initials, displayName);
            }
            return result;
        }

        [HttpGet("GetListRun/{runId}")]
        public async Task<Result<ListRunModel>> GetListRun(int runId)
        {
            return await _listService.GetListRun(runId);
        }

        [HttpPost("CreateListRun/{listId}")]
        public async Task<Result<ListRunModel>> CreateListRun(int listId)
        {
            return await _listService.CreateListRun(listId, _userId);
        }

        [HttpGet("GetListRunHistory/{listId}")]
        public async Task<Result<List<ListRunHistoryModel>>> GetListRunHistory(int listId)
        {
            return await _listService.GetListRunHistory(listId);
        }

        // === Shared list endpoints ===================================================

        [HttpGet("collaborators")]
        public async Task<Result<List<ListMemberModel>>> GetKnownCollaborators()
        {
            return await _listService.GetKnownCollaborators(_userId);
        }

        [HttpGet("{listId}/members")]
        public async Task<Result<List<ListMemberModel>>> GetListMembers(int listId)
        {
            return await _listService.GetListMembers(listId, _userId);
        }

        [HttpGet("{listId}/invitations")]
        public async Task<Result<List<ListPendingInviteModel>>> GetPendingInvitations(int listId)
        {
            return await _listService.GetPendingInvitations(listId, _userId);
        }

        [HttpPost("{listId}/invite")]
        public async Task<Result<InviteResultModel>> InviteToList(int listId, [FromBody] InviteRequestModel request)
        {
            var inviterName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "Someone";
            return await _listService.InviteToListAndNotify(listId, _userId, request.Email, request.SponsorConfirmed, _appBaseUrl, inviterName);
        }

        [HttpDelete("{listId}/members/{memberId}")]
        public async Task<Result<bool>> RemoveListMember(int listId, int memberId)
        {
            return await _listService.RemoveListMember(listId, _userId, memberId);
        }

        [HttpDelete("{listId}/leave")]
        public async Task<Result<bool>> LeaveList(int listId)
        {
            return await _listService.LeaveList(listId, _userId);
        }
    }
}
