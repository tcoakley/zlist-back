namespace zListBack.Dtos
{
    public class ContactRequest
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string ContactType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
