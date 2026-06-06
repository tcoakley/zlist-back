using zListBack.Models;

namespace zListBack.Repositories
{
    public interface IUserPaymentHistoryRepository
    {
        Task<Result<UserPaymentHistory>> AddAsync(UserPaymentHistory payment);
        Task<IEnumerable<UserPaymentHistory>> GetByUserIdAsync(int userId);
    }
}
