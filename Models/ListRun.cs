namespace zListBack.Models
{
    public class ListRun
    {
        public int Id { get; set; }
        public int ListId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int? CompletedBy { get; set; }

        public List<ListRunItem> Items { get; set; } = new List<ListRunItem>();
    }

}
