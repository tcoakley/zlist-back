namespace zListBack.Dtos
{
    public class CollaboratorCheckModel
    {
        public bool Exists { get; set; }
        public bool IsPremium { get; set; }
        /// <summary>stripe | sponsored | admin | gift | null</summary>
        public string? PremiumSource { get; set; }
        public bool IsAlreadyYourCollaborator { get; set; }
        /// <summary>True when the user is sponsored by a different sponsor.</summary>
        public bool IsAlreadySponsoredByOther { get; set; }
    }
}
