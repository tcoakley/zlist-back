namespace zListBack.Models
{
    public class ListInvitation
    {
        public int Id { get; set; }
        public int ListId { get; set; }
        public int InvitedByUserId { get; set; }
        public string InvitedEmail { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string Status { get; set; } = "pending";
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public int? AcceptedByUserId { get; set; }

        // Populated from JOINs
        public string? ListName { get; set; }
        public string? InvitedByFirstName { get; set; }
        public string? InvitedByLastName { get; set; }
        public bool HasAccount { get; set; }
    }
}
