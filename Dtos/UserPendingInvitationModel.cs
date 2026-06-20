namespace zListBack.Dtos
{
    public class UserPendingInvitationModel
    {
        public string Token { get; set; } = string.Empty;
        public int ListId { get; set; }
        public string ListName { get; set; } = string.Empty;
        public string InvitedByName { get; set; } = string.Empty;
        public bool RequiresPremium { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
