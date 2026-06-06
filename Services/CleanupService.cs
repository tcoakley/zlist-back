using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using zListBack.Repositories;

namespace zListBack.Services
{
    public class CleanupService : BackgroundService
    {
        private const int InactiveDays = 45;
        private const int BillingReminderDays = 2;
        private static readonly TimeSpan RunInterval = TimeSpan.FromHours(24);

        private readonly IServiceScopeFactory _scopeFactory;

        public CleanupService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _scopeFactory.CreateScope();
                var subRepo = scope.ServiceProvider.GetRequiredService<ISubscriptionRepository>();
                var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
                var subSvc = scope.ServiceProvider.GetRequiredService<SubscriptionService>();
                var emailSvc = scope.ServiceProvider.GetRequiredService<EmailService>();

                await RunAllJobs(subRepo, userRepo, subSvc, emailSvc);
                await Task.Delay(RunInterval, stoppingToken);
            }
        }

        public async Task RunAllJobs(
            ISubscriptionRepository subRepo,
            IUserRepository userRepo,
            SubscriptionService subscriptionService,
            EmailService emailService)
        {
            await SendBillingReminders(subRepo, emailService);
            await SendInactivityNotices(subRepo, emailService);
            await NotifySponsorsOfInactiveCollaborators(subRepo, emailService);
            await NotifySponsorsOfUpgradedCollaborators(subRepo, emailService);
        }

        // ─── Billing reminder (2 days before next billing date) ──────────────────

        public async Task SendBillingReminders(ISubscriptionRepository subRepo, EmailService emailService)
        {
            var users = await subRepo.GetUsersWithBillingInDays(BillingReminderDays);
            foreach (var user in users)
            {
                var billingDate = user.SubscriptionExpiresAt ?? DateTime.UtcNow.AddDays(BillingReminderDays);
                var activeSponsoredCount = await subRepo.GetActiveSponsoredCount(user.Id);
                var paidSeats = Math.Max(0, activeSponsoredCount - 1);
                var amount = 1.99m + (paidSeats * 1.00m);

                var firstName = user.FirstName ?? user.Email;
                _ = emailService.SendBillingReminderEmail(user.Email, firstName, billingDate, amount);
                await subRepo.SetBillingReminderSent(user.Id, billingDate);
            }
        }

        // ─── Inactivity notice (premium users inactive 45+ days) ─────────────────

        public async Task SendInactivityNotices(ISubscriptionRepository subRepo, EmailService emailService)
        {
            var users = await subRepo.GetInactivePremiumUsers(InactiveDays);
            foreach (var user in users)
            {
                var nextBillingDate = user.SubscriptionExpiresAt ?? DateTime.UtcNow.AddMonths(1);
                var firstName = user.FirstName ?? user.Email;
                _ = emailService.SendInactivityReminderEmail(user.Email, firstName, nextBillingDate);
                await subRepo.SetInactivityNoticeSent(user.Id);
            }
        }

        // ─── Sponsor with inactive collaborators ─────────────────────────────────

        public async Task NotifySponsorsOfInactiveCollaborators(ISubscriptionRepository subRepo, EmailService emailService)
        {
            var groups = await subRepo.GetSponsorsWithInactiveCollaborators(InactiveDays);
            foreach (var (sponsor, collaborators) in groups)
            {
                var inactiveNames = collaborators.Select(c =>
                {
                    var name = $"{c.FirstName} {c.LastName}".Trim();
                    return string.IsNullOrEmpty(name) ? c.Email : name;
                }).ToList();

                if (!inactiveNames.Any()) continue;

                var nextBillingDate = sponsor.SubscriptionExpiresAt ?? DateTime.UtcNow.AddMonths(1);
                var sponsorFirstName = sponsor.FirstName ?? sponsor.Email;
                _ = emailService.SendSponsorInactiveCollaboratorsEmail(
                    sponsor.Email, sponsorFirstName, inactiveNames, nextBillingDate);
            }
        }

        // ─── Collaborator upgraded themselves ────────────────────────────────────

        public async Task NotifySponsorsOfUpgradedCollaborators(ISubscriptionRepository subRepo, EmailService emailService)
        {
            var pairs = await subRepo.GetCollaboratorsWhoUpgradedThemselves();
            foreach (var (sponsor, collaborator) in pairs)
            {
                var collaboratorName = $"{collaborator.FirstName} {collaborator.LastName}".Trim();
                if (string.IsNullOrEmpty(collaboratorName)) collaboratorName = collaborator.Email;

                var sponsorFirstName = sponsor.FirstName ?? sponsor.Email;
                _ = emailService.SendCollaboratorUpgradedEmail(sponsor.Email, sponsorFirstName, collaboratorName);
            }
        }
    }
}
