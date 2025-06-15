using zListBack.Dtos;
using zListBack.Models;

public static class UserMapper
{
    public static UserModel ToDto(User user) => new UserModel
    {
        Id = user.Id,
        Email = user.Email,
        FirstName = user.FirstName,
        LastName = user.LastName,
        CreatedAt = user.CreatedAt,
        UpdatedAt = user.UpdatedAt
    };
}
