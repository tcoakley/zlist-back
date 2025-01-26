using System;
using System.Text.Json.Serialization;

namespace zChecklist.Models
{
    public class User
    {
        public int Id { get; set; }
        [JsonIgnore]
        public string Password { get; set; } = string.Empty;
        public required string Email { get; set; }
        public string? FirstName { get; set; }
        public string? Lastname { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
