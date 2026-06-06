namespace zListBack.Configurations
{
    public class StripeSettings
    {
        public string SecretKey { get; set; } = string.Empty;
        public string PublishableKey { get; set; } = string.Empty;
        public string WebhookSecret { get; set; } = string.Empty;
        public string PremiumProductId { get; set; } = string.Empty;
        public string PremiumPriceId { get; set; } = string.Empty;
        public string CollaboratorProductId { get; set; } = string.Empty;
        public string CollaboratorPriceId { get; set; } = string.Empty;
    }
}
