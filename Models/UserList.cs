namespace zListBack.Models
{
    public class UserList
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int ListId { get; set; }
        public bool IsOwner { get; set; }
    }

}
