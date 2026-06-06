namespace zListBack.Dtos
{
    public class SubscriptionStatusModel
    {
        public string Subscription { get; set; } = "free";
        public string SubscriptionSource { get; set; } = "free";
        public DateTime? ExpiresAt { get; set; }
        public DateTime? GracePeriodUntil { get; set; }
        public bool IsPremium { get; set; }
        public bool IsSponsored { get; set; }
        public string? SponsorName { get; set; }
        public int OwnedListCount { get; set; }
        public int OwnedListLimit { get; set; } // 2 for free, -1 for unlimited
    }
}
