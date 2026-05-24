namespace zListBack.Dtos
{
    public class AppVersionModel
    {
        public string Version { get; set; } = string.Empty;
        public DateTime ReleasedAt { get; set; }
        public string? Notes { get; set; }
    }
}
