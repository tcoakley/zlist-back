using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using zListBack.Dtos;
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
        private readonly int _userId;

        public ListController(ListService listService, IHttpContextAccessor httpContextAccessor)
        {
            _listService = listService;

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
            return await _listService.DeleteList(listId);
        }

        [HttpPut("CompleteListRun/{runId}")]
        public async Task<Result<bool>> CompleteListRun(int runId)
        {
            return await _listService.CompleteListRun(runId, _userId);
        }

        [HttpPut("SetListRunItemCompletion/{runItemId}")]
        public async Task<Result<bool>> SetListRunItemCompletion(int runItemId, [FromBody] bool isComplete)
        {
            return await _listService.SetListRunItemCompletion(runItemId, isComplete, _userId);
        }

        [HttpGet("GetListRun/{runId}")]
        public async Task<Result<ListRunModel>> GetListRun(int runId)
        {
            return await _listService.GetListRun(runId);
        }

        [HttpPost("CreateListRun/{listId}")]
        public async Task<Result<ListRunModel>> CreateListRun(int listId)
        {
            return await _listService.CreateListRun(listId);
        }

    }
}
