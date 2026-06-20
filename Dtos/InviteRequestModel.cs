namespace zListBack.Dtos
{
    public class InviteRequestModel
    {
        public string Email { get; set; } = string.Empty;
        // null = first call (check if sponsorship needed)
        // true = user confirmed they want to sponsor
        // false = user declined sponsorship (send RequiresPremium invite)
        public bool? SponsorConfirmed { get; set; }
    }

    public class InviteResultModel
    {
        public bool RequiresSponsor { get; set; }
        public string? Message { get; set; }
        // Used internally by the controller to send the email — not serialized to the response
        [System.Text.Json.Serialization.JsonIgnore]
        public string? Token { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public bool RequiresPremiumEmail { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public string? ListName { get; set; }
    }
}
