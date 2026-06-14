namespace zListBack.Dtos
{
    public class PendingSponsorInvitationModel
    {
        public int Id { get; set; }
        public string InvitedEmail { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
