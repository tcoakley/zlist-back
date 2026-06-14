using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using zListBack.Configurations;
using zListBack.Models;
using zListBack.Repositories;
using zListBack.Services;
using AppSubscriptionService = zListBack.Services.SubscriptionService;

namespace zListBack.Controllers
{
    [ApiController]
    [Route("api/stripe/webhook")]
    public class StripeWebhookController : ControllerBase
    {
        private readonly AppSubscriptionService _subscriptionService;
        private readonly ISubscriptionRepository _subscriptionRepo;
        private readonly IUserPaymentHistoryRepository _paymentHistoryRepo;
        private readonly ListRepository _listRepo;
        private readonly EmailService _emailService;
        private readonly string _webhookSecret;
        private readonly ILogger<StripeWebhookController> _logger;

        public StripeWebhookController(
            AppSubscriptionService subscriptionService,
            ISubscriptionRepository subscriptionRepo,
            IUserPaymentHistoryRepository paymentHistoryRepo,
            ListRepository listRepo,
            EmailService emailService,
            IOptions<StripeSettings> stripeOptions,
            ILogger<StripeWebhookController> logger)
        {
            _subscriptionService = subscriptionService;
            _subscriptionRepo = subscriptionRepo;
            _paymentHistoryRepo = paymentHistoryRepo;
            _listRepo = listRepo;
            _emailService = emailService;
            _webhookSecret = stripeOptions.Value.WebhookSecret;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> Handle()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

            Event stripeEvent;
            try
            {
                stripeEvent = EventUtility.ConstructEvent(
                    json,
                    Request.Headers["Stripe-Signature"],
                    _webhookSecret
                );
            }
            catch (StripeException ex)
            {
                _logger.LogWarning(ex, "Stripe webhook signature validation failed.");
                return BadRequest("Invalid Stripe signature.");
            }

            switch (stripeEvent.Type)
            {
                case EventTypes.InvoicePaymentSucceeded:
                    await HandlePaymentSucceeded(stripeEvent);
                    break;

                case EventTypes.InvoicePaymentFailed:
                    await HandlePaymentFailed(stripeEvent);
                    break;

                case EventTypes.CustomerSubscriptionDeleted:
                    await HandleSubscriptionDeleted(stripeEvent);
                    break;
            }

            return Ok();
        }

        // === Event handlers ===========================================================

        private async Task HandlePaymentSucceeded(Event stripeEvent)
        {
            var invoice = stripeEvent.Data.Object as Invoice;
            if (invoice == null || string.IsNullOrEmpty(invoice.CustomerId)) return;

            var user = await _subscriptionRepo.GetUserByStripeCustomerId(invoice.CustomerId);
            if (user == null) return;

            // Determine period end from the first line item
            var periodEnd = invoice.Lines?.Data?.FirstOrDefault()?.Period?.End;
            if (periodEnd.HasValue)
            {
                await _subscriptionRepo.SetUserSubscription(user.Id, "premium", "stripe", periodEnd.Value);
                await _listRepo.RestoreArchivedLists(user.Id);
                await _subscriptionService.HandleCollaboratorSelfUpgrade(user.Id);
            }

            // Clear grace period and any pending cancellation — payment succeeded means they renewed
            await _subscriptionRepo.SetGracePeriod(user.Id, null!);
            await _subscriptionRepo.SetCancellationScheduled(user.Id, null);

            // Record payment in history (idempotent via unique StripeEventId)
            if (!string.IsNullOrEmpty(stripeEvent.Id))
            {
                var planType = "premium";
                var isFirstPayment = await _paymentHistoryRepo.AddAsync(new UserPaymentHistory
                {
                    UserId = user.Id,
                    StripeEventId = stripeEvent.Id,
                    AmountPaid = invoice.AmountPaid / 100m,
                    Currency = invoice.Currency ?? "usd",
                    PlanType = planType,
                    PaidAt = invoice.StatusTransitions?.PaidAt ?? DateTime.UtcNow
                });

                // Send welcome email on first successful payment only
                if (isFirstPayment.Success)
                {
                    var firstName = user.FirstName ?? user.Email;
                    _ = _emailService.SendSubscriptionActivatedEmail(user.Email, firstName);
                }
            }
        }

        private async Task HandlePaymentFailed(Event stripeEvent)
        {
            var invoice = stripeEvent.Data.Object as Invoice;
            if (invoice == null || string.IsNullOrEmpty(invoice.CustomerId)) return;

            var user = await _subscriptionRepo.GetUserByStripeCustomerId(invoice.CustomerId);
            if (user == null) return;

            await _subscriptionService.HandleSponsorLapse(user.Id);

            var gracePeriodUntil = DateTime.UtcNow.AddDays(7);
            var firstName = user.FirstName ?? user.Email;
            _ = _emailService.SendPaymentFailedEmail(user.Email, firstName, gracePeriodUntil);
        }

        private async Task HandleSubscriptionDeleted(Event stripeEvent)
        {
            var subscription = stripeEvent.Data.Object as Subscription;
            if (subscription == null || string.IsNullOrEmpty(subscription.CustomerId)) return;

            var user = await _subscriptionRepo.GetUserByStripeCustomerId(subscription.CustomerId);
            if (user == null) return;

            var lastAccessDate = subscription.EndedAt ?? DateTime.UtcNow;
            await _subscriptionRepo.SetCancellationScheduled(user.Id, null);
            await _subscriptionService.HandleSponsorCancellation(user.Id);
            await _subscriptionService.FinalizeSponsorCancellation(user.Id);

            var firstName = user.FirstName ?? user.Email;
            _ = _emailService.SendSubscriptionCancelledEmail(user.Email, firstName, lastAccessDate);
        }
    }
}
