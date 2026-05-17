namespace zListBack.Dtos
{
    public class ListRunModel
    {
        public int Id { get; set; }
        public int ListId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int? CompletedBy { get; set; }

        public List<ListRunItemModel> Items { get; set; } = new List<ListRunItemModel>();

        public bool IsComplete => CompletedAt.HasValue;
    }
}
