using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using zListBack.Dtos;
using zListBack.Models;
using zListBack.Services;

namespace zListBack.Controllers
{
    [ApiController]
    [Route("api/contact")]
    [Authorize]
    public class ContactController : ControllerBase
    {
        private readonly ContactService _contactService;

        private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        public ContactController(ContactService contactService)
        {
            _contactService = contactService;
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

            return await _contactService.Submit(UserId, request.FirstName, request.LastName, request.ContactType, request.Message);
        }
    }
}
