namespace zListBack.Models
{
    public class SponsoredCollaborator
    {
        public int Id { get; set; }
        public int SponsorUserId { get; set; }
        public int SponsoredUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
        public DateTime? GraceUntil { get; set; }
    }
}
