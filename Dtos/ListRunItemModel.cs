using System;

namespace zListBack.Dtos
{
    public class ListRunItemModel
    {
        public int Id { get; set; }
        public int ListRunId { get; set; }
        public int? ListItemId { get; set; }

        public string ListItemName { get; set; } = string.Empty;
        public string? ListItemDescription { get; set; }

        public DateTime? CompletedAt { get; set; }
        public int? CompletedBy { get; set; }
    }
}
