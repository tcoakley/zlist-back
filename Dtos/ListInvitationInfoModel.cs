namespace zListBack.Dtos
{
    public class ListInvitationInfoModel
    {
        public int ListId { get; set; }
        public string ListName { get; set; } = string.Empty;
        public string InvitedByName { get; set; } = string.Empty;
        public string InvitedEmail { get; set; } = string.Empty;
        public string Status { get; set; } = "pending";
        public bool IsExpired { get; set; }
    }
}
