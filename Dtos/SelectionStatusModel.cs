namespace zListBack.Dtos
{
    public class SelectionStatusModel
    {
        public bool NeedsSelection { get; set; }
        public List<SelectionListItem> Lists { get; set; } = [];
        public int AllowedCount { get; set; } = 2;
    }

    public class SelectionListItem
    {
        public int Id { get; set; }
        public string ListName { get; set; } = string.Empty;
        public int TotalItems { get; set; }
        public bool IsArchived { get; set; }
    }
}
