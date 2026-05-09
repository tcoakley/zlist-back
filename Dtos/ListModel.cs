namespace zListBack.Dtos
{
    public class ListModel
    {
        public int Id { get; set; }
        public string ListName { get; set; } = string.Empty;
        public string? ListDescription { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public List<ListItemModel> Items { get; set; } = new List<ListItemModel>();
    }
}
