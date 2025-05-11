using Microsoft.AspNetCore.Mvc;
using zListBack.Services;
using zListBack.Repositories;
using zListBack.Models;
using zListBack.Dtos;

namespace zListBack.Controllers
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

            var token = JwtTokenGenerator.GenerateToken(result.Model!, _configuration);   

            return Ok(new
            {
                Token = token,
                User = result.Model
            });
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


    }

}
