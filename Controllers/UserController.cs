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
        private readonly IUserRepository _userRepository;
        private readonly RecaptchaService _recaptchaService;
        private readonly EmailService _emailService;
        private readonly SubscriptionService _subscriptionService;

        public UserController(IUserRepository userRepository, RecaptchaService recaptchaService, EmailService emailService, SubscriptionService subscriptionService)
        {
            _userRepository = userRepository;
            _recaptchaService = recaptchaService;
            _emailService = emailService;
            _subscriptionService = subscriptionService;
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
            if (!result.Success) return Result<UserModel>.Fail(result.Message ?? "Failed to retrieve user profile.");
            var dto = UserMapper.ToDto(result.Model!);
            dto.IsPremium = await _subscriptionService.IsPremium(userId);
            return Result<UserModel>.Ok(dto);
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
            await _subscriptionService.ApplyPendingSponsorshipOnSignup(result.Model!.Id, request.Email);
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
