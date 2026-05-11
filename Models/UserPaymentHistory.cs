namespace zListBack.Models
{
    public class UserPaymentHistory
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string StripeEventId { get; set; } = string.Empty;
        public decimal AmountPaid { get; set; }
        public string Currency { get; set; } = "usd";
        public string PlanType { get; set; } = string.Empty;
        public DateTime PaidAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
