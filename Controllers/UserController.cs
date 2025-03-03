using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using zChecklist.Models;
using zChecklist.Repositories;

namespace zChecklist.Controllers
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
        public async Task<Result<User>> GetUser(string email)
        {
            return await _userRepository.GetUserByEmailAsync(email);
        }

        [HttpPost("AddUser")]
        [AllowAnonymous]
        public async Task<Result<User>> AddUser([FromBody] User user)
        {
            return await _userRepository.AddUserAsync(user);

        }

        [HttpPut("UpdateUser")]
        public async Task<Result> UpdateUser([FromBody] User updatedUser)
        {
            return await _userRepository.UpdateUserAsync(updatedUser);
        }

    }
}
