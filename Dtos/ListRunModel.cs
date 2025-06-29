namespace zListBack.Dtos
{
    public class ListRunModel
    {
        public int Id { get; set; }
        public int ListId { get; set; }
        public DateTime CreatedAt { get; set; }

        public List<ListRunItemModel> Items { get; set; } = new List<ListRunItemModel>();

        public bool IsComplete
        {
            get
            {
                if (Items == null || Items.Count == 0)
                    return false;

                return Items.All(item => item.CompletedAt.HasValue);
            }
        }
    }
}
