using Microsoft.AspNetCore.Mvc;
using zChecklist.Services;
using zChecklist.Repositories;
using zChecklist.Models;

namespace zChecklist.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LoginController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly UserRepository _userRepository;
        private readonly EmailService _emailService;

        public LoginController(IConfiguration configuration, UserRepository userRepository, EmailService emailService)
        {
            _configuration = configuration;
            _userRepository = userRepository;
            _emailService = emailService;
        }

        [HttpPost]
        public async Task<IActionResult> Login([FromBody] LoginRequestModel request)
        {
            var result = await _userRepository.CheckLoginAsync(request.Email, request.Password);
            if (!result.Success)
            {
                return Unauthorized(result.Message);
            }

            var token = JwtTokenGenerator.GenerateToken(result.Model!.Email, _configuration);

            return Ok(new
            {
                Token = token,
                User = result.Model
            });
        }

        [HttpPost("forgotPassword")]
        public async Task<IActionResult> ForgotPassword([FromBody] string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return BadRequest("Email is required.");
            }

            var result = await _userRepository.GetUserByEmailAsync(email);
            if (!result.Success)
            {
                return NotFound("User not found.");
            }

            await _emailService.SendForgotPasswordEmail(email);
            return Ok("If the email is registered, you will receive a password reset email shortly.");
        }

    }

}
