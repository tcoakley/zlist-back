namespace zListBack.Models
{
    public class List
    {
        public int Id { get; set; }
        public string ListName { get; set; } = String.Empty;
        public string? ListDescription { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int ActiveRunId { get; set; } = 0;
        public int TotalRuns { get; set; }
        public DateTime? LastRun { get; set; }
        public int TotalItems { get; set; }

        public List<ListItem> Items { get; set; } = new List<ListItem>();
    }

}
