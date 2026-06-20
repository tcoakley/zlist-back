using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Stripe;
using zListBack.Configurations;
using zListBack.Dtos;
using zListBack.Models;
using zListBack.Repositories;
using zListBack.Services;
using AppSubscriptionService = zListBack.Services.SubscriptionService;

namespace zListBack.Tests;

/// <summary>
/// Tests for the Stripe webhook event handlers on SubscriptionService, plus
/// GetSubscriptionStatus and AdminGrant/AdminRevoke which are subscription-status critical.
/// All tests are unit tests (no live Stripe API calls).
/// </summary>
public class StripeWebhookHandlerTests
{
    // === Build helper ========================================================

    private static (
        AppSubscriptionService svc,
        Mock<ISubscriptionRepository> subRepo,
        Mock<IUserRepository> userRepo,
        Mock<IUserPaymentHistoryRepository> paymentRepo,
        Mock<ListRepository> listRepo,
        Mock<EmailService> emailSvc
    ) Build()
    {
        var subRepo = new Mock<ISubscriptionRepository>();
        var userRepo = new Mock<IUserRepository>();
        var paymentRepo = new Mock<IUserPaymentHistoryRepository>();
        var listRepo = new Mock<ListRepository>(Mock.Of<System.Data.IDbConnection>(), NullLogger<ListRepository>.Instance);
        listRepo.Setup(r => r.RestoreArchivedLists(It.IsAny<int>())).Returns(Task.CompletedTask);

        var emailSection = new Mock<Microsoft.Extensions.Configuration.IConfigurationSection>();
        emailSection.Setup(s => s["SmtpServer"]).Returns("localhost");
        emailSection.Setup(s => s["SmtpPort"]).Returns("25");
        emailSection.Setup(s => s["SenderEmail"]).Returns("test@test.com");
        var config = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        config.Setup(c => c.GetSection("EmailSettings")).Returns(emailSection.Object);
        var emailSvc = new Mock<EmailService>(config.Object, userRepo.Object, NullLogger<EmailService>.Instance) { CallBase = false };

        var stripeOptions = Options.Create(new StripeSettings
        {
            PremiumPriceId = "price_test_premium",
            CollaboratorPriceId = "price_test_collab"
        });

        var svc = new AppSubscriptionService(
            subRepo.Object, userRepo.Object, paymentRepo.Object,
            listRepo.Object, emailSvc.Object, stripeOptions,
            NullLogger<AppSubscriptionService>.Instance);

        return (svc, subRepo, userRepo, paymentRepo, listRepo, emailSvc);
    }

    private static Models.User MakeUser(int id, string sub = "free", string source = "free") => new()
    {
        Id = id,
        Email = $"user{id}@test.com",
        FirstName = "Test",
        LastName = $"User{id}",
        Subscription = sub,
        SubscriptionSource = source
    };

    private static Invoice MakeInvoice(string customerId, long amountPaid = 199, DateTime? periodEnd = null) => new()
    {
        CustomerId = customerId,
        AmountPaid = amountPaid,
        Currency = "usd",
        StatusTransitions = new InvoiceStatusTransitions { PaidAt = DateTime.UtcNow },
        Lines = new StripeList<InvoiceLineItem>
        {
            Data = new List<InvoiceLineItem>
            {
                new() { Period = new InvoiceLineItemPeriod { End = periodEnd ?? DateTime.UtcNow.AddMonths(1) } }
            }
        }
    };

    // === HandleStripePaymentSucceeded ========================================

    [Fact]
    public async Task PaymentSucceeded_EmptyCustomerId_NoDbCallsMade()
    {
        var (svc, subRepo, _, paymentRepo, _, _) = Build();
        var invoice = new Invoice { CustomerId = string.Empty };

        await svc.HandleStripePaymentSucceeded(invoice, "evt_test");

        subRepo.Verify(r => r.GetUserByStripeCustomerId(It.IsAny<string>()), Times.Never);
        paymentRepo.Verify(r => r.AddAsync(It.IsAny<UserPaymentHistory>()), Times.Never);
    }

    [Fact]
    public async Task PaymentSucceeded_UnknownCustomer_NoSubscriptionUpdate()
    {
        var (svc, subRepo, _, paymentRepo, _, _) = Build();
        subRepo.Setup(r => r.GetUserByStripeCustomerId("cus_unknown")).ReturnsAsync((Models.User?)null);
        var invoice = MakeInvoice("cus_unknown");

        await svc.HandleStripePaymentSucceeded(invoice, "evt_test");

        subRepo.Verify(r => r.SetUserSubscription(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>()), Times.Never);
        paymentRepo.Verify(r => r.AddAsync(It.IsAny<UserPaymentHistory>()), Times.Never);
    }

    [Fact]
    public async Task PaymentSucceeded_ValidInvoice_SetsSubscriptionAndClearsGrace()
    {
        var (svc, subRepo, userRepo, paymentRepo, listRepo, emailSvc) = Build();
        var user = MakeUser(1, "free");
        subRepo.Setup(r => r.GetUserByStripeCustomerId("cus_1")).ReturnsAsync(user);
        subRepo.Setup(r => r.SetUserSubscription(1, "premium", "stripe", It.IsAny<DateTime>())).Returns(Task.CompletedTask);
        subRepo.Setup(r => r.SetGracePeriod(1, It.IsAny<DateTime?>()!)).Returns(Task.CompletedTask);
        subRepo.Setup(r => r.SetCancellationScheduled(1, null)).Returns(Task.CompletedTask);
        subRepo.Setup(r => r.GetActiveSponsorRecord(1)).ReturnsAsync(((int, bool)?)null);
        paymentRepo.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(Enumerable.Empty<UserPaymentHistory>());
        paymentRepo.Setup(r => r.AddAsync(It.IsAny<UserPaymentHistory>())).ReturnsAsync(Result<UserPaymentHistory>.Ok(new UserPaymentHistory()));

        var invoice = MakeInvoice("cus_1", periodEnd: DateTime.UtcNow.AddMonths(1));

        await svc.HandleStripePaymentSucceeded(invoice, "evt_001");

        subRepo.Verify(r => r.SetUserSubscription(1, "premium", "stripe", It.IsAny<DateTime>()), Times.Once);
        listRepo.Verify(r => r.RestoreArchivedLists(1), Times.Once);
        subRepo.Verify(r => r.SetGracePeriod(1, It.IsAny<DateTime?>()!), Times.Once);
        subRepo.Verify(r => r.SetCancellationScheduled(1, null), Times.Once);
    }

    [Fact]
    public async Task PaymentSucceeded_FirstPayment_SendsActivationEmail()
    {
        var (svc, subRepo, _, paymentRepo, _, emailSvc) = Build();
        var user = MakeUser(1);
        subRepo.Setup(r => r.GetUserByStripeCustomerId("cus_1")).ReturnsAsync(user);
        subRepo.Setup(r => r.SetUserSubscription(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>())).Returns(Task.CompletedTask);
        subRepo.Setup(r => r.SetGracePeriod(It.IsAny<int>(), It.IsAny<DateTime?>()!)).Returns(Task.CompletedTask);
        subRepo.Setup(r => r.SetCancellationScheduled(It.IsAny<int>(), It.IsAny<DateTime?>())).Returns(Task.CompletedTask);
        subRepo.Setup(r => r.GetActiveSponsorRecord(1)).ReturnsAsync(((int, bool)?)null);
        // No prior payment history → first payment
        paymentRepo.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(Enumerable.Empty<UserPaymentHistory>());
        paymentRepo.Setup(r => r.AddAsync(It.IsAny<UserPaymentHistory>())).ReturnsAsync(Result<UserPaymentHistory>.Ok(new UserPaymentHistory()));
        emailSvc.Setup(e => e.SendSubscriptionActivatedEmail(user.Email, It.IsAny<string>())).ReturnsAsync(Result<bool>.Ok(true));

        await svc.HandleStripePaymentSucceeded(MakeInvoice("cus_1"), "evt_001");

        emailSvc.Verify(e => e.SendSubscriptionActivatedEmail(user.Email, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task PaymentSucceeded_RenewalPayment_NoActivationEmail()
    {
        // Renewals have a new event ID each month so AddAsync always succeeds.
        // The welcome email must NOT fire — only send on first payment.
        var (svc, subRepo, _, paymentRepo, _, emailSvc) = Build();
        var user = MakeUser(1);
        subRepo.Setup(r => r.GetUserByStripeCustomerId("cus_1")).ReturnsAsync(user);
        subRepo.Setup(r => r.SetUserSubscription(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>())).Returns(Task.CompletedTask);
        subRepo.Setup(r => r.SetGracePeriod(It.IsAny<int>(), It.IsAny<DateTime?>()!)).Returns(Task.CompletedTask);
        subRepo.Setup(r => r.SetCancellationScheduled(It.IsAny<int>(), It.IsAny<DateTime?>())).Returns(Task.CompletedTask);
        subRepo.Setup(r => r.GetActiveSponsorRecord(1)).ReturnsAsync(((int, bool)?)null);
        // Prior payment history exists → renewal, not first payment
        paymentRepo.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(new[] { new UserPaymentHistory { UserId = 1 } });
        paymentRepo.Setup(r => r.AddAsync(It.IsAny<UserPaymentHistory>())).ReturnsAsync(Result<UserPaymentHistory>.Ok(new UserPaymentHistory()));

        await svc.HandleStripePaymentSucceeded(MakeInvoice("cus_1"), "evt_002_renewal");

        emailSvc.Verify(e => e.SendSubscriptionActivatedEmail(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PaymentSucceeded_RecordsCorrectAmountAndCurrency()
    {
        var (svc, subRepo, _, paymentRepo, _, _) = Build();
        var user = MakeUser(1);
        subRepo.Setup(r => r.GetUserByStripeCustomerId("cus_1")).ReturnsAsync(user);
        subRepo.Setup(r => r.SetUserSubscription(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>())).Returns(Task.CompletedTask);
        subRepo.Setup(r => r.SetGracePeriod(It.IsAny<int>(), It.IsAny<DateTime?>()!)).Returns(Task.CompletedTask);
        subRepo.Setup(r => r.SetCancellationScheduled(It.IsAny<int>(), It.IsAny<DateTime?>())).Returns(Task.CompletedTask);
        subRepo.Setup(r => r.GetActiveSponsorRecord(1)).ReturnsAsync(((int, bool)?)null);
        paymentRepo.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(Enumerable.Empty<UserPaymentHistory>());
        UserPaymentHistory? recorded = null;
        paymentRepo.Setup(r => r.AddAsync(It.IsAny<UserPaymentHistory>()))
            .Callback<UserPaymentHistory>(h => recorded = h)
            .ReturnsAsync(Result<UserPaymentHistory>.Ok(new UserPaymentHistory()));

        var invoice = MakeInvoice("cus_1", amountPaid: 199);
        await svc.HandleStripePaymentSucceeded(invoice, "evt_001");

        recorded.Should().NotBeNull();
        recorded!.AmountPaid.Should().Be(1.99m); // Stripe amounts are in cents
        recorded.Currency.Should().Be("usd");
        recorded.PlanType.Should().Be("premium");
        recorded.StripeEventId.Should().Be("evt_001");
    }

    // === HandleStripePaymentFailed ===========================================

    [Fact]
    public async Task PaymentFailed_EmptyCustomerId_NoDbCallsMade()
    {
        var (svc, subRepo, _, _, _, emailSvc) = Build();
        var invoice = new Invoice { CustomerId = string.Empty };

        await svc.HandleStripePaymentFailed(invoice);

        subRepo.Verify(r => r.GetUserByStripeCustomerId(It.IsAny<string>()), Times.Never);
        subRepo.Verify(r => r.StartAllSponsorshipsGrace(It.IsAny<int>(), It.IsAny<DateTime>()), Times.Never);
        emailSvc.Verify(e => e.SendPaymentFailedEmail(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task PaymentFailed_UnknownCustomer_NoGraceStarted()
    {
        var (svc, subRepo, _, _, _, _) = Build();
        subRepo.Setup(r => r.GetUserByStripeCustomerId("cus_unknown")).ReturnsAsync((Models.User?)null);

        await svc.HandleStripePaymentFailed(new Invoice { CustomerId = "cus_unknown" });

        subRepo.Verify(r => r.StartAllSponsorshipsGrace(It.IsAny<int>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task PaymentFailed_ValidInvoice_StartsGraceOnSponsorAndCollaborators()
    {
        var (svc, subRepo, _, _, _, emailSvc) = Build();
        var user = MakeUser(1, "premium", "stripe");
        subRepo.Setup(r => r.GetUserByStripeCustomerId("cus_1")).ReturnsAsync(user);
        subRepo.Setup(r => r.StartAllSponsorshipsGrace(1, It.IsAny<DateTime>())).Returns(Task.CompletedTask);
        subRepo.Setup(r => r.SetGracePeriod(1, It.IsAny<DateTime>())).Returns(Task.CompletedTask);
        emailSvc.Setup(e => e.SendPaymentFailedEmail(user.Email, It.IsAny<string>(), It.IsAny<DateTime>())).ReturnsAsync(Result<bool>.Ok(true));

        await svc.HandleStripePaymentFailed(new Invoice { CustomerId = "cus_1" });

        subRepo.Verify(r => r.StartAllSponsorshipsGrace(
            1, It.Is<DateTime>(d => d > DateTime.UtcNow.AddDays(6) && d < DateTime.UtcNow.AddDays(8))
        ), Times.Once);
        subRepo.Verify(r => r.SetGracePeriod(
            1, It.Is<DateTime>(d => d > DateTime.UtcNow.AddDays(6) && d < DateTime.UtcNow.AddDays(8))
        ), Times.Once);
        emailSvc.Verify(e => e.SendPaymentFailedEmail(user.Email, It.IsAny<string>(), It.IsAny<DateTime>()), Times.Once);
    }

    // === HandleStripeSubscriptionDeleted =====================================

    [Fact]
    public async Task SubscriptionDeleted_EmptyCustomerId_NoDbCallsMade()
    {
        var (svc, subRepo, _, _, _, emailSvc) = Build();
        var subscription = new Stripe.Subscription { CustomerId = string.Empty };

        await svc.HandleStripeSubscriptionDeleted(subscription);

        subRepo.Verify(r => r.GetUserByStripeCustomerId(It.IsAny<string>()), Times.Never);
        emailSvc.Verify(e => e.SendSubscriptionCancelledEmail(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task SubscriptionDeleted_UnknownCustomer_NoDowngrade()
    {
        var (svc, subRepo, _, _, _, _) = Build();
        subRepo.Setup(r => r.GetUserByStripeCustomerId("cus_unknown")).ReturnsAsync((Models.User?)null);

        await svc.HandleStripeSubscriptionDeleted(new Stripe.Subscription { CustomerId = "cus_unknown" });

        subRepo.Verify(r => r.SetUserSubscription(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>()), Times.Never);
    }

    [Fact]
    public async Task SubscriptionDeleted_ValidSubscription_DowngradesUserToFree()
    {
        var (svc, subRepo, _, _, _, emailSvc) = Build();
        var user = MakeUser(1, "premium", "stripe");
        subRepo.Setup(r => r.GetUserByStripeCustomerId("cus_1")).ReturnsAsync(user);
        subRepo.Setup(r => r.SetCancellationScheduled(1, null)).Returns(Task.CompletedTask);
        subRepo.Setup(r => r.StartAllSponsorshipsGrace(1, It.IsAny<DateTime>())).Returns(Task.CompletedTask);
        subRepo.Setup(r => r.SetGracePeriod(1, It.IsAny<DateTime>())).Returns(Task.CompletedTask);
        subRepo.Setup(r => r.DeactivateAllSponsorships(1)).Returns(Task.CompletedTask);
        subRepo.Setup(r => r.RevokeAllSharedListAccess(1)).Returns(Task.CompletedTask);
        subRepo.Setup(r => r.SetUserSubscription(1, "free", "free", null)).Returns(Task.CompletedTask);
        emailSvc.Setup(e => e.SendSubscriptionCancelledEmail(user.Email, It.IsAny<string>(), It.IsAny<DateTime>())).ReturnsAsync(Result<bool>.Ok(true));

        var subscription = new Stripe.Subscription { CustomerId = "cus_1", EndedAt = DateTime.UtcNow };

        await svc.HandleStripeSubscriptionDeleted(subscription);

        subRepo.Verify(r => r.SetCancellationScheduled(1, null), Times.Once);
        subRepo.Verify(r => r.DeactivateAllSponsorships(1), Times.Once);
        subRepo.Verify(r => r.RevokeAllSharedListAccess(1), Times.Once);
        subRepo.Verify(r => r.SetUserSubscription(1, "free", "free", null), Times.Once);
        emailSvc.Verify(e => e.SendSubscriptionCancelledEmail(user.Email, It.IsAny<string>(), It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task SubscriptionDeleted_NullEndedAt_UsesCurrentTimeForEmail()
    {
        // When Stripe doesn't provide EndedAt, we fall back to DateTime.UtcNow — still sends the email.
        var (svc, subRepo, _, _, _, emailSvc) = Build();
        var user = MakeUser(1, "premium", "stripe");
        subRepo.Setup(r => r.GetUserByStripeCustomerId("cus_1")).ReturnsAsync(user);
        subRepo.Setup(r => r.SetCancellationScheduled(1, null)).Returns(Task.CompletedTask);
        subRepo.Setup(r => r.StartAllSponsorshipsGrace(1, It.IsAny<DateTime>())).Returns(Task.CompletedTask);
        subRepo.Setup(r => r.SetGracePeriod(1, It.IsAny<DateTime>())).Returns(Task.CompletedTask);
        subRepo.Setup(r => r.DeactivateAllSponsorships(1)).Returns(Task.CompletedTask);
        subRepo.Setup(r => r.RevokeAllSharedListAccess(1)).Returns(Task.CompletedTask);
        subRepo.Setup(r => r.SetUserSubscription(1, "free", "free", null)).Returns(Task.CompletedTask);
        emailSvc.Setup(e => e.SendSubscriptionCancelledEmail(user.Email, It.IsAny<string>(), It.IsAny<DateTime>())).ReturnsAsync(Result<bool>.Ok(true));

        var subscription = new Stripe.Subscription { CustomerId = "cus_1", EndedAt = null };

        await svc.HandleStripeSubscriptionDeleted(subscription);

        emailSvc.Verify(e => e.SendSubscriptionCancelledEmail(user.Email, It.IsAny<string>(), It.IsAny<DateTime>()), Times.Once);
    }

    // === GetSubscriptionStatus ===============================================

    [Fact]
    public async Task GetSubscriptionStatus_UserNotFound_ReturnsNull()
    {
        var (svc, _, userRepo, _, _, _) = Build();
        userRepo.Setup(r => r.GetUserAsync(99)).ReturnsAsync(Result<Models.User>.Fail("not found"));

        var result = await svc.GetSubscriptionStatus(99);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetSubscriptionStatus_FreeUser_CorrectModel()
    {
        var (svc, subRepo, userRepo, _, _, _) = Build();
        var user = MakeUser(1, "free", "free");
        userRepo.Setup(r => r.GetUserAsync(1)).ReturnsAsync(Result<Models.User>.Ok(user));
        subRepo.Setup(r => r.IsPremium(1)).ReturnsAsync(false);
        subRepo.Setup(r => r.GetOwnedListCount(1)).ReturnsAsync(1);

        var result = await svc.GetSubscriptionStatus(1);

        result.Should().NotBeNull();
        result!.IsPremium.Should().BeFalse();
        result.IsSponsored.Should().BeFalse();
        result.OwnedListLimit.Should().Be(2);
        result.OwnedListCount.Should().Be(1);
        result.SponsorName.Should().BeNull();
        // Should never look up a sponsor for a free user
        subRepo.Verify(r => r.GetSponsor(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetSubscriptionStatus_PremiumUser_CorrectModel()
    {
        var (svc, subRepo, userRepo, _, _, _) = Build();
        var user = MakeUser(1, "premium", "stripe");
        userRepo.Setup(r => r.GetUserAsync(1)).ReturnsAsync(Result<Models.User>.Ok(user));
        subRepo.Setup(r => r.IsPremium(1)).ReturnsAsync(true);
        subRepo.Setup(r => r.GetOwnedListCount(1)).ReturnsAsync(5);

        var result = await svc.GetSubscriptionStatus(1);

        result!.IsPremium.Should().BeTrue();
        result.IsSponsored.Should().BeFalse();
        result.OwnedListLimit.Should().Be(-1); // unlimited
        result.OwnedListCount.Should().Be(5);
        subRepo.Verify(r => r.GetSponsor(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetSubscriptionStatus_SponsoredUser_IsSponsoredTrueAndSponsorNameSet()
    {
        // A sponsored user has Subscription="free" but IsPremium()=true via SponsoredCollaborators.
        var (svc, subRepo, userRepo, _, _, _) = Build();
        var user = MakeUser(1, "free", "free");
        var sponsor = new Models.User { Id = 2, Email = "sponsor@test.com", FirstName = "Jane", LastName = "Doe" };
        userRepo.Setup(r => r.GetUserAsync(1)).ReturnsAsync(Result<Models.User>.Ok(user));
        subRepo.Setup(r => r.IsPremium(1)).ReturnsAsync(true);
        subRepo.Setup(r => r.GetOwnedListCount(1)).ReturnsAsync(0);
        subRepo.Setup(r => r.GetSponsor(1)).ReturnsAsync(sponsor);

        var result = await svc.GetSubscriptionStatus(1);

        result!.IsSponsored.Should().BeTrue();
        result.SponsorName.Should().Be("Jane Doe");
        result.IsPremium.Should().BeTrue();
        result.OwnedListLimit.Should().Be(-1);
    }

    [Fact]
    public async Task GetSubscriptionStatus_SponsoredUser_NoSponsorRecord_SponsorNameNull()
    {
        // IsSponsored=true but GetSponsor returns null (edge case: record deleted between calls).
        var (svc, subRepo, userRepo, _, _, _) = Build();
        var user = MakeUser(1, "free", "free");
        userRepo.Setup(r => r.GetUserAsync(1)).ReturnsAsync(Result<Models.User>.Ok(user));
        subRepo.Setup(r => r.IsPremium(1)).ReturnsAsync(true);
        subRepo.Setup(r => r.GetOwnedListCount(1)).ReturnsAsync(0);
        subRepo.Setup(r => r.GetSponsor(1)).ReturnsAsync((Models.User?)null);

        var result = await svc.GetSubscriptionStatus(1);

        result!.IsSponsored.Should().BeTrue();
        result.SponsorName.Should().BeNull();
    }

    // === AdminGrantPremium ===================================================

    [Fact]
    public async Task AdminGrantPremium_UnknownEmail_ReturnsFail()
    {
        var (svc, subRepo, _, _, _, emailSvc) = Build();
        subRepo.Setup(r => r.GetUserByEmail("nobody@test.com")).ReturnsAsync((Models.User?)null);

        var result = await svc.AdminGrantPremium("nobody@test.com", "gift", null);

        result.Success.Should().BeFalse();
        emailSvc.Verify(e => e.SendAdminGrantedEmail(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>()), Times.Never);
    }

    [Fact]
    public async Task AdminGrantPremium_KnownEmail_SetsSubscriptionAndSendsEmail()
    {
        var (svc, subRepo, _, _, listRepo, emailSvc) = Build();
        var user = MakeUser(5, "free");
        subRepo.Setup(r => r.GetUserByEmail(user.Email)).ReturnsAsync(user);
        subRepo.Setup(r => r.SetUserSubscription(5, "premium", "gift", null)).Returns(Task.CompletedTask);
        emailSvc.Setup(e => e.SendAdminGrantedEmail(user.Email, It.IsAny<string>(), "gift", null)).ReturnsAsync(Result<bool>.Ok(true));

        var result = await svc.AdminGrantPremium(user.Email, "gift", null);

        result.Success.Should().BeTrue();
        subRepo.Verify(r => r.SetUserSubscription(5, "premium", "gift", null), Times.Once);
        listRepo.Verify(r => r.RestoreArchivedLists(5), Times.Once);
        emailSvc.Verify(e => e.SendAdminGrantedEmail(user.Email, It.IsAny<string>(), "gift", null), Times.Once);
    }

    [Fact]
    public async Task AdminGrantPremium_WithExpiry_ExpiryPassedThrough()
    {
        var (svc, subRepo, _, _, _, emailSvc) = Build();
        var user = MakeUser(5);
        var expiry = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        subRepo.Setup(r => r.GetUserByEmail(user.Email)).ReturnsAsync(user);
        subRepo.Setup(r => r.SetUserSubscription(5, "premium", "admin", expiry)).Returns(Task.CompletedTask);
        emailSvc.Setup(e => e.SendAdminGrantedEmail(user.Email, It.IsAny<string>(), "admin", expiry)).ReturnsAsync(Result<bool>.Ok(true));

        var result = await svc.AdminGrantPremium(user.Email, "admin", expiry);

        result.Success.Should().BeTrue();
        subRepo.Verify(r => r.SetUserSubscription(5, "premium", "admin", expiry), Times.Once);
        emailSvc.Verify(e => e.SendAdminGrantedEmail(user.Email, It.IsAny<string>(), "admin", expiry), Times.Once);
    }

    // === AdminRevokePremium ==================================================

    [Fact]
    public async Task AdminRevokePremium_UnknownEmail_ReturnsFail()
    {
        var (svc, subRepo, _, _, _, emailSvc) = Build();
        subRepo.Setup(r => r.GetUserByEmail("nobody@test.com")).ReturnsAsync((Models.User?)null);

        var result = await svc.AdminRevokePremium("nobody@test.com");

        result.Success.Should().BeFalse();
        emailSvc.Verify(e => e.SendAdminRevokedEmail(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task AdminRevokePremium_KnownEmail_SetsToFreeDeactivatesSponsorshipsAndSendsEmail()
    {
        var (svc, subRepo, _, _, _, emailSvc) = Build();
        var user = MakeUser(5, "premium");
        subRepo.Setup(r => r.GetUserByEmail(user.Email)).ReturnsAsync(user);
        subRepo.Setup(r => r.SetUserSubscription(5, "free", "free", null)).Returns(Task.CompletedTask);
        subRepo.Setup(r => r.DeactivateAllSponsorships(5)).Returns(Task.CompletedTask);
        emailSvc.Setup(e => e.SendAdminRevokedEmail(user.Email, It.IsAny<string>())).ReturnsAsync(Result<bool>.Ok(true));

        var result = await svc.AdminRevokePremium(user.Email);

        result.Success.Should().BeTrue();
        subRepo.Verify(r => r.SetUserSubscription(5, "free", "free", null), Times.Once);
        subRepo.Verify(r => r.DeactivateAllSponsorships(5), Times.Once);
        emailSvc.Verify(e => e.SendAdminRevokedEmail(user.Email, It.IsAny<string>()), Times.Once);
    }
}
