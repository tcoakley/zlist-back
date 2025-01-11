using Microsoft.AspNetCore.Mvc;
using zListBack.Services;
using zListBack.Models;

namespace zListBack.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LoginController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public LoginController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpPost]
        public IActionResult Login([FromBody] LoginRequestModel request)
        {
            // Simulate user validation (replace with actual user lookup)
            if (request.Username == "testuser" && request.Password == "password123")
            {
                var token = JwtTokenGenerator.GenerateToken(request.Username, _configuration);
                return Ok(new { Token = token });
            }

            return Unauthorized("Invalid credentials");
        }
    }

}
