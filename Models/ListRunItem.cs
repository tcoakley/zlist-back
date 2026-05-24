namespace zListBack.Models
{
    public class ListRunItem
    {
        public int Id { get; set; }
        public int ListRunId { get; set; }
        public int? ListItemId { get; set; }

        public string ListItemName { get; set; } = string.Empty;
        public string? ListItemDescription { get; set; }
        public int SortOrder { get; set; }

        public DateTime? CompletedAt { get; set; }
        public int? CompletedBy { get; set; }
        public string? CompletedByInitials { get; set; }
        public string? CompletedByName { get; set; }
    }


}
