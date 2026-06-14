namespace zListBack.Dtos
{
    public class UserModel
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string Subscription { get; set; } = "free";
        public DateTime? SubscriptionExpiresAt { get; set; }
        public string SubscriptionSource { get; set; } = "free";
        public bool IsPremium { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsHelpEnabled { get; set; } = true;
        public bool SortCompletedToBottom { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
