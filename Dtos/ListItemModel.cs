namespace zListBack.Dtos
{
    public class ListItemModel
    {
        public int Id { get; set; }
        public int ListId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string? ItemDescription { get; set; }
    }
}
