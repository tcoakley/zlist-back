using zListBack.Models;
using zListBack.Repositories;

namespace zListBack.Services
{
    public class ContactService
    {
        private readonly IUserRepository _userRepo;
        private readonly EmailService _emailService;

        public ContactService(IUserRepository userRepo, EmailService emailService)
        {
            _userRepo = userRepo;
            _emailService = emailService;
        }

        public async Task<Result<bool>> Submit(int userId, string firstName, string lastName, string contactType, string message)
        {
            var userResult = await _userRepo.GetUserAsync(userId);
            if (!userResult.Success || userResult.Model == null)
                return Result<bool>.Fail("User not found.");

            return await _emailService.SendContactEmail(
                userId,
                userResult.Model.Email,
                firstName,
                lastName,
                contactType,
                message
            );
        }
    }
}
