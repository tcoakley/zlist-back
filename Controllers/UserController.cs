using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using zListBack.Models;
using zListBack.Repositories;
using zListBack.Dtos;
using zListBack.Mappers;

namespace zListBack.Controllers
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
            {
                return Result<UserModel>.Fail("User ID not found.");
            }

            var result = await _userRepository.GetUserAsync(userId);
            return result.Success
                ? Result<UserModel>.Ok(UserMapper.ToDto(result.Model!))
                : Result<UserModel>.Fail(result.Message ?? "Failed to retrieve user profile.");
        }

        [HttpPost("AddUser")]
        [AllowAnonymous]
        public async Task<Result<UserModel>> AddUser([FromBody] User user)
        {
            var result = await _userRepository.AddUserAsync(user);
            return result.Success
                ? Result<UserModel>.Ok(UserMapper.ToDto(result.Model!))
                : Result<UserModel>.Fail(result.Message ?? "Failed to add user.");
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
