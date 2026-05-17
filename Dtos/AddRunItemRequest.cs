namespace zListBack.Dtos
{
    public class AddRunItemRequest
    {
        public int ListRunId { get; set; }
        public int ListId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public bool OneTime { get; set; }
    }
}
