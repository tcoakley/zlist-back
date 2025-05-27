using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
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

        [HttpPost("AddList")]
        public async Task<Result<List>> AddList([FromBody] List list)
        {
            return await _listService.AddList(list, _userId);
        }

        [HttpPost("AddListItem")]
        public async Task<Result<ListItem>> AddListItem([FromBody] ListItem item)
        {
            return await _listService.AddListItem(item);
        }

        [HttpPut("EditList")]
        public async Task<Result<List>> EditList([FromBody] List list)
        {
            return await _listService.EditList(list);
        }

        [HttpPut("EditListItem")]
        public async Task<Result<ListItem>> EditListItem([FromBody] ListItem item)
        {
            return await _listService.EditListItem(item);
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
    }
}
