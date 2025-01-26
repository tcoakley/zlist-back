using Microsoft.AspNetCore.Mvc;
using zListBack.Services;
using zChecklist.Repositories;
using zChecklist.Models;
using zListBack.Models;

namespace zListBack.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LoginController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly UserRepository _userRepository;

        public LoginController(IConfiguration configuration, UserRepository userRepository)
        {
            _configuration = configuration;
            _userRepository = userRepository;
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

    }

}
