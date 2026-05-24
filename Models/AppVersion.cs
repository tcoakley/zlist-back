namespace zListBack.Models
{
    public class AppVersion
    {
        public int Id { get; set; }
        public string Version { get; set; } = string.Empty;
        public DateTime ReleasedAt { get; set; }
        public string? Notes { get; set; }
    }
}
