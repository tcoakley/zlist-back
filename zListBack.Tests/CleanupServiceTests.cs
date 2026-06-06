using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using zListBack.Configurations;
using zListBack.Models;
using zListBack.Repositories;
using zListBack.Services;

namespace zListBack.Tests;

public class CleanupServiceTests
{
    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static (
        CleanupService svc,
        Mock<ISubscriptionRepository> subRepo,
        Mock<IUserRepository> userRepo,
        Mock<EmailService> email,
        SubscriptionService subscriptionService
    ) Build()
    {
        var subRepo = new Mock<ISubscriptionRepository>();
        var userRepo = new Mock<IUserRepository>();
        var stripeOptions = Options.Create(new StripeSettings
        {
            PremiumPriceId = "price_test_premium",
            CollaboratorPriceId = "price_test_collab"
        });
        var paymentRepo = new Mock<IUserPaymentHistoryRepository>();
        var subSvc = new SubscriptionService(subRepo.Object, userRepo.Object, paymentRepo.Object, stripeOptions);

        var emailSection = new Mock<Microsoft.Extensions.Configuration.IConfigurationSection>();
        emailSection.Setup(s => s["SmtpServer"]).Returns("localhost");
        emailSection.Setup(s => s["SmtpPort"]).Returns("25");
        emailSection.Setup(s => s["SenderEmail"]).Returns("test@test.com");
        var config = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        config.Setup(c => c.GetSection("EmailSettings")).Returns(emailSection.Object);
        var email = new Mock<EmailService>(config.Object, userRepo.Object) { CallBase = false };

        // CleanupService now takes IServiceScopeFactory; tests call public methods directly
        var scopeFactory = new Mock<IServiceScopeFactory>();
        var svc = new CleanupService(scopeFactory.Object);

        return (svc, subRepo, userRepo, email, subSvc);
    }

    private static User MakePremiumUser(int id, DateTime? lastActiveAt = null, DateTime? expiresAt = null) => new()
    {
        Id = id,
        Email = $"user{id}@test.com",
        FirstName = "Test",
        LastName = $"User{id}",
        Subscription = "premium",
        SubscriptionSource = "stripe",
        SubscriptionExpiresAt = expiresAt ?? DateTime.UtcNow.AddDays(20),
        LastActiveAt = lastActiveAt
    };

    // ─── Billing reminder ─────────────────────────────────────────────────────

    [Fact]
    public async Task SendBillingReminders_UserBillingInTwoDays_SendsEmail()
    {
        var (svc, subRepo, _, email, _) = Build();
        var user = MakePremiumUser(1, expiresAt: DateTime.UtcNow.AddDays(2));
        subRepo.Setup(r => r.GetUsersWithBillingInDays(2)).ReturnsAsync(new[] { user });
        subRepo.Setup(r => r.GetActiveSponsoredCount(1)).ReturnsAsync(0);
        subRepo.Setup(r => r.SetBillingReminderSent(1, It.IsAny<DateTime>())).Returns(Task.CompletedTask);
        email.Setup(e => e.SendBillingReminderEmail(user.Email, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<decimal>()))
             .ReturnsAsync(Result<bool>.Ok(true));

        await svc.SendBillingReminders(subRepo.Object, email.Object);

        email.Verify(e => e.SendBillingReminderEmail(user.Email, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<decimal>()), Times.Once);
        subRepo.Verify(r => r.SetBillingReminderSent(1, It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task SendBillingReminders_NoUsersUpcoming_SendsNoEmails()
    {
        var (svc, subRepo, _, email, _) = Build();
        subRepo.Setup(r => r.GetUsersWithBillingInDays(2)).ReturnsAsync(Enumerable.Empty<User>());

        await svc.SendBillingReminders(subRepo.Object, email.Object);

        email.Verify(e => e.SendBillingReminderEmail(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<decimal>()), Times.Never);
    }

    // ─── Inactivity notices ───────────────────────────────────────────────────

    [Fact]
    public async Task SendInactivityNotices_UserInactiveOver45Days_SendsEmailAndMarks()
    {
        var (svc, subRepo, _, email, _) = Build();
        var user = MakePremiumUser(1, lastActiveAt: DateTime.UtcNow.AddDays(-50));
        subRepo.Setup(r => r.GetInactivePremiumUsers(45)).ReturnsAsync(new[] { user });
        subRepo.Setup(r => r.SetInactivityNoticeSent(1)).Returns(Task.CompletedTask);
        email.Setup(e => e.SendInactivityReminderEmail(user.Email, It.IsAny<string>(), It.IsAny<DateTime>()))
             .ReturnsAsync(Result<bool>.Ok(true));

        await svc.SendInactivityNotices(subRepo.Object, email.Object);

        email.Verify(e => e.SendInactivityReminderEmail(user.Email, It.IsAny<string>(), It.IsAny<DateTime>()), Times.Once);
        subRepo.Verify(r => r.SetInactivityNoticeSent(1), Times.Once);
    }

    [Fact]
    public async Task SendInactivityNotices_NoInactiveUsers_SendsNoEmails()
    {
        var (svc, subRepo, _, email, _) = Build();
        subRepo.Setup(r => r.GetInactivePremiumUsers(45)).ReturnsAsync(Enumerable.Empty<User>());

        await svc.SendInactivityNotices(subRepo.Object, email.Object);

        email.Verify(e => e.SendInactivityReminderEmail(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
    }

    // ─── Sponsor with inactive collaborators ─────────────────────────────────

    [Fact]
    public async Task NotifySponsorsOfInactiveCollaborators_HasInactive_SendsCombinedEmail()
    {
        var (svc, subRepo, _, email, _) = Build();
        var sponsor = MakePremiumUser(1);
        var collab1 = MakePremiumUser(2, lastActiveAt: DateTime.UtcNow.AddDays(-50));
        var collab2 = MakePremiumUser(3, lastActiveAt: DateTime.UtcNow.AddDays(-60));
        subRepo.Setup(r => r.GetSponsorsWithInactiveCollaborators(45))
               .ReturnsAsync(new[] { (Sponsor: sponsor, Collaborators: (IEnumerable<User>)new[] { collab1, collab2 }) });
        email.Setup(e => e.SendSponsorInactiveCollaboratorsEmail(sponsor.Email, It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<DateTime>()))
             .ReturnsAsync(Result<bool>.Ok(true));

        await svc.NotifySponsorsOfInactiveCollaborators(subRepo.Object, email.Object);

        email.Verify(e => e.SendSponsorInactiveCollaboratorsEmail(sponsor.Email, It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task NotifySponsorsOfInactiveCollaborators_NoInactive_SendsNoEmails()
    {
        var (svc, subRepo, _, email, _) = Build();
        subRepo.Setup(r => r.GetSponsorsWithInactiveCollaborators(45))
               .ReturnsAsync(Enumerable.Empty<(User, IEnumerable<User>)>());

        await svc.NotifySponsorsOfInactiveCollaborators(subRepo.Object, email.Object);

        email.Verify(e => e.SendSponsorInactiveCollaboratorsEmail(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<DateTime>()), Times.Never);
    }

    // ─── Collaborator upgraded themselves ────────────────────────────────────

    [Fact]
    public async Task NotifySponsorsOfUpgradedCollaborators_CollaboratorUpgraded_EmailsSponsor()
    {
        var (svc, subRepo, _, email, _) = Build();
        var sponsor = MakePremiumUser(1);
        var collab = MakePremiumUser(2);
        subRepo.Setup(r => r.GetCollaboratorsWhoUpgradedThemselves())
               .ReturnsAsync(new[] { (Sponsor: sponsor, Collaborator: collab) });
        email.Setup(e => e.SendCollaboratorUpgradedEmail(sponsor.Email, It.IsAny<string>(), It.IsAny<string>()))
             .ReturnsAsync(Result<bool>.Ok(true));

        await svc.NotifySponsorsOfUpgradedCollaborators(subRepo.Object, email.Object);

        email.Verify(e => e.SendCollaboratorUpgradedEmail(sponsor.Email, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task NotifySponsorsOfUpgradedCollaborators_NoneUpgraded_SendsNoEmails()
    {
        var (svc, subRepo, _, email, _) = Build();
        subRepo.Setup(r => r.GetCollaboratorsWhoUpgradedThemselves())
               .ReturnsAsync(Enumerable.Empty<(User, User)>());

        await svc.NotifySponsorsOfUpgradedCollaborators(subRepo.Object, email.Object);

        email.Verify(e => e.SendCollaboratorUpgradedEmail(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
