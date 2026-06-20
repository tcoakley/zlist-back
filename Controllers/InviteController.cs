using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using zListBack.Dtos;
using zListBack.Models;
using zListBack.Services;

namespace zListBack.Controllers
{
    [ApiController]
    [Route("api/invite")]
    public class InviteController : ControllerBase
    {
        private readonly ListService _listService;

        public InviteController(ListService listService)
        {
            _listService = listService;
        }

        [HttpGet("{token}")]
        [AllowAnonymous]
        public async Task<Result<ListInvitationInfoModel>> GetInvitation(string token)
        {
            return await _listService.GetListInvitation(token);
        }

        [HttpGet("pending")]
        [Authorize]
        public async Task<Result<IEnumerable<UserPendingInvitationModel>>> GetPendingInvitations()
        {
            var email = HttpContext.User?.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(email))
                return Result<IEnumerable<UserPendingInvitationModel>>.Fail("User email not found in token.");

            return await _listService.GetPendingInvitationsForUser(email);
        }

        [HttpPost("{token}/accept")]
        [Authorize]
        public async Task<Result<bool>> AcceptInvitation(string token)
        {
            var userIdClaim = HttpContext.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                return Result<bool>.Fail("User not authenticated.");

            return await _listService.AcceptListInvitation(token, userId);
        }

        [HttpDelete("{token}")]
        [Authorize]
        public async Task<Result<bool>> DeclineInvitation(string token)
        {
            return await _listService.DeclineListInvitation(token);
        }
    }
}
