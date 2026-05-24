namespace zListBack.Dtos
{
    public class ListPendingInviteModel
    {
        public string InvitedEmail { get; set; } = string.Empty;
        public bool IsExpired { get; set; }
    }
}
