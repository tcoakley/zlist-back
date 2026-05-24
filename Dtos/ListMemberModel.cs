namespace zListBack.Dtos
{
    public class ListMemberModel
    {
        public int UserId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool IsOwner { get; set; }
    }
}
