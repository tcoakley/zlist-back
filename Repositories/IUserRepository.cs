using zListBack.Models;

namespace zListBack.Repositories
{
    public interface IUserRepository
    {
        Task<Result<User>> GetUserAsync(int id);
        Task<Result<User>> GetUserByEmailAsync(string email);
        Task<Result<User>> AddUserAsync(User user);
        Task<Result<User>> UpdateUserAsync(User model);
        Task<Result<User>> CheckLoginAsync(string email, string password);
        Task<Result<string>> GenerateResetPassword(string email);
        Task UpdateLastActiveAt(int userId);
        Task ClearInactivityNotice(int userId);
    }
}
