using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using zListBack.Dtos;
using zListBack.Models;
using zListBack.Repositories;
using zListBack.Services;

namespace zListBack.Controllers
{
    [ApiController]
    [Route("api/contact")]
    [Authorize]
    public class ContactController : ControllerBase
    {
        private readonly EmailService _emailService;
        private readonly IUserRepository _userRepo;

        private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        public ContactController(EmailService emailService, IUserRepository userRepo)
        {
            _emailService = emailService;
            _userRepo = userRepo;
        }

        [HttpPost]
        public async Task<Result<bool>> Submit([FromBody] ContactRequest request)
        {
            var validTypes = new[] { "Question", "Bug", "Feature Request", "Other" };
            if (string.IsNullOrWhiteSpace(request.FirstName) ||
                string.IsNullOrWhiteSpace(request.LastName) ||
                string.IsNullOrWhiteSpace(request.Message) ||
                !validTypes.Contains(request.ContactType))
                return Result<bool>.Fail("All fields are required.");

            var userId = UserId;
            var userResult = await _userRepo.GetUserAsync(userId);
            if (!userResult.Success || userResult.Model == null)
                return Result<bool>.Fail("User not found.");

            return await _emailService.SendContactEmail(
                userId,
                userResult.Model.Email,
                request.FirstName,
                request.LastName,
                request.ContactType,
                request.Message
            );
        }
    }
}
