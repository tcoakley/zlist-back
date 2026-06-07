namespace zListBack.Dtos
{
    public class ListModel
    {
        public int Id { get; set; }
        public string ListName { get; set; } = string.Empty;
        public string? ListDescription { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int ActiveRunId { get; set; } = 0;
        public int TotalRuns { get; set; }
        public DateTime? LastRun { get; set; }
        public int TotalItems { get; set; }
        public bool IsOwner { get; set; }
        public int MemberCount { get; set; }
        public string? OwnerName { get; set; }

        public List<ListItemModel> Items { get; set; } = new List<ListItemModel>();
    }
}
