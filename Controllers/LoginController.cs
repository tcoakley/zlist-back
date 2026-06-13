using Microsoft.AspNetCore.Mvc;
using zListBack.Services;
using zListBack.Repositories;
using zListBack.Models;
using zListBack.Dtos;
using zListBack.Utils; 
using Microsoft.AspNetCore.Http; 

namespace zListBack.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LoginController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IUserRepository _userRepository;
        private readonly EmailService _emailService;
        private readonly RefreshTokenRepository _refreshTokenRepository;

        public LoginController(
            IConfiguration configuration,
            IUserRepository userRepository,
            EmailService emailService,
            RefreshTokenRepository refreshTokenRepository)
        {
            _configuration = configuration;
            _userRepository = userRepository;
            _emailService = emailService;
            _refreshTokenRepository = refreshTokenRepository;
        }

        [HttpPost]
        public async Task<IActionResult> Login([FromBody] LoginRequestModel request)
        {
            var result = await _userRepository.CheckLoginAsync(request.Email, request.Password);
            if (!result.Success)
            {
                return BadRequest(result.Message);
            }

            var user = result.Model!;
            await _userRepository.UpdateLastActiveAt(user.Id);
            var accessToken = JwtTokenGenerator.GenerateToken(user, _configuration);


            var refreshTokenString = TokenHelper.GenerateRefreshToken();
            var refreshToken = new RefreshToken
            {
                UserId = user.Id,
                Token = refreshTokenString,
                ExpiresAt = DateTime.UtcNow.AddYears(5),
                CreatedAt = DateTime.UtcNow,
                Revoked = false
            };

            await _refreshTokenRepository.AddAsync(refreshToken);

            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true, 
                SameSite = SameSiteMode.Strict,
                Expires = refreshToken.ExpiresAt
            };
            Response.Cookies.Append("refreshToken", refreshTokenString, cookieOptions);

            return Ok(Result<object>.Ok(new
            {
                Token = accessToken,
                User = UserMapper.ToDto(user)
            }));
        }

        [HttpPost("forgotPassword")]
        public async Task<Result<string>> ForgotPassword([FromBody] ForgotPasswordModel model)
        {
            var email = model.Email;
            if (string.IsNullOrWhiteSpace(email))
            {
                return Result<string>.Fail("Email is required.");
            }
            return await _emailService.SendForgotPasswordEmail(email);
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh()
        {
            if (!Request.Cookies.TryGetValue("refreshToken", out var refreshTokenString))
                return Unauthorized("Refresh token not found.");

            var refreshToken = await _refreshTokenRepository.GetByTokenAsync(refreshTokenString);
            if (refreshToken == null || refreshToken.ExpiresAt < DateTime.UtcNow || refreshToken.Revoked)
                return Unauthorized("Invalid or expired refresh token.");

            var user = refreshToken.User;
            if (user == null)
                return Unauthorized("User not found.");

            var newAccessToken = JwtTokenGenerator.GenerateToken(user, _configuration);

            var newRefreshTokenString = TokenHelper.GenerateRefreshToken();
            var newRefreshToken = new RefreshToken
            {
                UserId = user.Id,
                Token = newRefreshTokenString,
                ExpiresAt = DateTime.UtcNow.AddYears(5),
                CreatedAt = DateTime.UtcNow,
                Revoked = false
            };

            await _refreshTokenRepository.InvalidateAsync(refreshTokenString);
            await _refreshTokenRepository.AddAsync(newRefreshToken);

            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = newRefreshToken.ExpiresAt
            };
            Response.Cookies.Append("refreshToken", newRefreshTokenString, cookieOptions);

            return Ok(Result<object>.Ok(new
            {
                Token = newAccessToken
            }));
        }

    }
}
