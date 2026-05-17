namespace zListBack.Dtos
{
    public class ListRunHistoryModel
    {
        public int Id { get; set; }
        public int ListId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int TotalItems { get; set; }
        public int CompletedItems { get; set; }
    }
}
