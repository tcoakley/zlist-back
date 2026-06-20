using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stripe;
using zListBack.Configurations;
using zListBack.Services;
using AppSubscriptionService = zListBack.Services.SubscriptionService;

namespace zListBack.Controllers
{
    [ApiController]
    [Route("api/stripe/webhook")]
    public class StripeWebhookController : ControllerBase
    {
        private readonly AppSubscriptionService _subscriptionService;
        private readonly string _webhookSecret;
        private readonly ILogger<StripeWebhookController> _logger;

        public StripeWebhookController(
            AppSubscriptionService subscriptionService,
            IOptions<StripeSettings> stripeOptions,
            ILogger<StripeWebhookController> logger)
        {
            _subscriptionService = subscriptionService;
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
                    if (stripeEvent.Data.Object is Invoice invoice1)
                        await _subscriptionService.HandleStripePaymentSucceeded(invoice1, stripeEvent.Id);
                    break;

                case EventTypes.InvoicePaymentFailed:
                    if (stripeEvent.Data.Object is Invoice invoice2)
                        await _subscriptionService.HandleStripePaymentFailed(invoice2);
                    break;

                case EventTypes.CustomerSubscriptionDeleted:
                    if (stripeEvent.Data.Object is Subscription subscription)
                        await _subscriptionService.HandleStripeSubscriptionDeleted(subscription);
                    break;
            }

            return Ok();
        }
    }
}
