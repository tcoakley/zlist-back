namespace zListBack.Dtos
{
    public class AdminGrantPremiumRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Source { get; set; } = "gift"; // 'gift' | 'admin'
        public DateTime? ExpiresAt { get; set; }      // null = never expires
    }

    public class AdminRevokeRequest
    {
        public string Email { get; set; } = string.Empty;
    }
}
