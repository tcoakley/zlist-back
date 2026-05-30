using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using zListBack.Models;
using zListBack.Repositories;
using zListBack.Dtos;
using zListBack.Mappers;
using zListBack.Services;

namespace zListBack.Controllers
{
    [ApiController]
    [Route("api/users")]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly UserRepository _userRepository;
        private readonly RecaptchaService _recaptchaService;
        private readonly EmailService _emailService;

        public UserController(UserRepository userRepository, RecaptchaService recaptchaService, EmailService emailService)
        {
            _userRepository = userRepository;
            _recaptchaService = recaptchaService;
            _emailService = emailService;
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
        public async Task<Result<UserModel>> AddUser([FromBody] SignupRequest request)
        {
            var captchaValid = await _recaptchaService.VerifyAsync(request.CaptchaToken);
            if (!captchaValid)
                return Result<UserModel>.Fail("CAPTCHA verification failed. Please try again.");

            var user = new User
            {
                Email = request.Email,
                Password = request.Password,
                FirstName = request.FirstName,
                LastName = request.LastName
            };

            var result = await _userRepository.AddUserAsync(user);
            if (!result.Success)
                return Result<UserModel>.Fail(result.Message ?? "Failed to add user.");

            _ = _emailService.SendWelcomeEmail(user.Email, user.FirstName ?? user.Email);
            return Result<UserModel>.Ok(UserMapper.ToDto(result.Model!));
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
