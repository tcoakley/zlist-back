using System;
using System.Text.Json.Serialization;

namespace zListBack.Models
{
    public class User
    {
        public int Id { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string Password { get; set; } = string.Empty;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? ResetPassword { get; set; }
        public required string Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string Subscription { get; set; } = "free";
        public DateTime? SubscriptionExpiresAt { get; set; }
        public bool IsHelpEnabled { get; set; } = true;
        public bool SortCompletedToBottom { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
