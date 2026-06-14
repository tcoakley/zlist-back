namespace zListBack.Dtos
{
    public class SponsoredCollaboratorModel
    {
        public int UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
        public DateTime? GraceUntil { get; set; }
        public bool IsFreeSeat { get; set; }
    }

    public class AddCollaboratorRequest
    {
        public string Email { get; set; } = string.Empty;
    }
}
