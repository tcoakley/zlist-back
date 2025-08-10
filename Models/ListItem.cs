namespace zListBack.Models
{
    public class ListItem
    {
        public int Id { get; set; }
        public int ListId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string? ItemDescription { get; set; }
        public int SortOrder { get; set; }
    }

}
