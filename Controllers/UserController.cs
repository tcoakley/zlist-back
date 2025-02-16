using Microsoft.AspNetCore.Mvc;
using zChecklist.Models;
using zChecklist.Repositories;

namespace zChecklist.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UserController : ControllerBase
    {
        private readonly UserRepository _userRepository;

        public UserController(UserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        [HttpGet("{email}")]
        public async Task<IActionResult> GetUser(string email)
        {
            var user = await _userRepository.GetUserByEmailAsync(email);
            if (user == null) return NotFound("User not found");
            return Ok(user);
        }

        [HttpPost("AddUser")]
        public async Task<IActionResult> AddUser([FromBody] User user)
        {
            var result = await _userRepository.AddUserAsync(user);

            if (result.Success)
                return Ok(result);
            else
                return BadRequest(result);
        }

        [HttpPut("{username}")]
        public async Task<IActionResult> UpdateUser(string username, User updatedUser)
        {
            var user = await _userRepository.GetUserByEmailAsync(username);
            if (user == null) return NotFound("User not found");

            user.Email = updatedUser.Email;
            await _userRepository.UpdateUserAsync(user);
            return NoContent();
        }

    }
}
