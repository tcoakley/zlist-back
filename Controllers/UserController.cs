using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using zListBack.Dtos;
using zListBack.Mappers;
using zListBack.Models;
using zListBack.Repositories;
using zListBack.Services;

namespace zListBack.Controllers
{
    [ApiController]
    [Route("api/users")]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly AuthService _authService;

        public UserController(IUserRepository userRepository, AuthService authService)
        {
            _userRepository = userRepository;
            _authService = authService;
        }

        [HttpGet("{email}")]
        public async Task<Result<UserModel>> GetUser(string email)
        {
            var result = await _userRepository.GetUserByEmailAsync(email);
            return result.Success
                ? Result<UserModel>.Ok(UserMapper.ToDto(result.Model!))
                : Result<UserModel>.Fail(result.Message ?? "Failed to retrieve user.");
        }

        [HttpGet("GetUserProfile")]
        public async Task<Result<UserModel>> GetUserProfile()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return Result<UserModel>.Fail("User ID not found.");

            return await _authService.GetUserProfileWithPremium(userId);
        }

        [HttpPost("AddUser")]
        [AllowAnonymous]
        public async Task<Result<UserModel>> AddUser([FromBody] SignupRequest request)
        {
            return await _authService.SignupUser(request);
        }

        [HttpPut("UpdateUser")]
        public async Task<Result<UserModel>> UpdateUser([FromBody] User updatedUser)
        {
            var result = await _userRepository.UpdateUserAsync(updatedUser);
            return result.Success
                ? Result<UserModel>.Ok(UserMapper.ToDto(result.Model!))
                : Result<UserModel>.Fail(result.Message ?? "Failed to update user.");
        }
    }
}
