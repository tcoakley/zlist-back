using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using zChecklist.Models;
using zChecklist.Repositories;

namespace zChecklist.Controllers
{
    [ApiController]
    [Route("api/users")]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly UserRepository _userRepository;

        public UserController(UserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        [HttpGet("{email}")]
        public async Task<Result<User>> GetUser(string email)
        {
            return await _userRepository.GetUserByEmailAsync(email);
        }

        [HttpGet("GetUserProfile")]
        public async Task<Result<User>> GetUserProfile()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Result<User>.Fail("User ID not found.");
            }

            return await _userRepository.GetUserAsync(userId);
        }


        [HttpPost("AddUser")]
        [AllowAnonymous]
        public async Task<Result<User>> AddUser([FromBody] User user)
        {
            return await _userRepository.AddUserAsync(user);

        }

        [HttpPut("UpdateUser")]
        public async Task<Result<User>> UpdateUser([FromBody] User updatedUser)
        {
            return await _userRepository.UpdateUserAsync(updatedUser);
        }

    }
}
