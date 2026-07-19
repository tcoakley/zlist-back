using Microsoft.AspNetCore.Mvc;
using zListBack.Services;
using zListBack.Repositories;
using zListBack.Models;
using zListBack.Dtos;
using zListBack.Utils;

namespace zListBack.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LoginController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IUserRepository _userRepository;
        private readonly EmailService _emailService;
        private readonly AuthService _authService;
        private readonly SubscriptionService _subscriptionService;
        private readonly ILogger<LoginController> _logger;

        public LoginController(
            IConfiguration configuration,
            IUserRepository userRepository,
            EmailService emailService,
            AuthService authService,
            SubscriptionService subscriptionService,
            ILogger<LoginController> logger)
        {
            _configuration = configuration;
            _userRepository = userRepository;
            _emailService = emailService;
            _authService = authService;
            _subscriptionService = subscriptionService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> Login([FromBody] LoginRequestModel request)
        {
            var result = await _userRepository.CheckLoginAsync(request.Email, request.Password);
            if (!result.Success)
                return BadRequest(result.Message);

            var user = result.Model!;
            await _userRepository.UpdateLastActiveAt(user.Id);

            var accessToken = JwtTokenGenerator.GenerateToken(user, _configuration);
            var (refreshTokenString, expiresAt) = await _authService.CreateRefreshToken(user.Id);
            SetRefreshTokenCookie(refreshTokenString, expiresAt);

            var userDto = UserMapper.ToDto(user);
            userDto.IsPremium = await _subscriptionService.IsPremium(user.Id);

            return Ok(Result<object>.Ok(new { Token = accessToken, User = userDto }));
        }

        [HttpPost("forgotPassword")]
        public async Task<Result<string>> ForgotPassword([FromBody] ForgotPasswordModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Email))
                return Result<string>.Fail("Email is required.");

            return await _emailService.SendForgotPasswordEmail(model.Email);
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh()
        {
            if (!Request.Cookies.TryGetValue("refreshToken", out var refreshTokenString))
            {
                _logger.LogWarning("Refresh failed: no refreshToken cookie present on the request.");
                return Unauthorized("Refresh token not found.");
            }

            var rotated = await _authService.RotateRefreshToken(refreshTokenString);
            if (rotated == null)
                return Unauthorized("Invalid or expired refresh token.");

            var (user, newTokenString, expiresAt) = rotated.Value;
            var newAccessToken = JwtTokenGenerator.GenerateToken(user, _configuration);
            SetRefreshTokenCookie(newTokenString, expiresAt);

            return Ok(Result<object>.Ok(new { Token = newAccessToken }));
        }

        private void SetRefreshTokenCookie(string token, DateTime expiresAt)
        {
            Response.Cookies.Append("refreshToken", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = expiresAt
            });
        }
    }
}
