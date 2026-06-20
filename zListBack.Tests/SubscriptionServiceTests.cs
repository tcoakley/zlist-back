using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using zListBack.Configurations;
using zListBack.Dtos;
using zListBack.Models;
using zListBack.Repositories;
using zListBack.Services;

namespace zListBack.Tests;

public class SubscriptionServiceTests
{
    // === Helpers =============================================================

    private static (
        SubscriptionService svc,
        Mock<ISubscriptionRepository> subRepo,
        Mock<IUserRepository> userRepo,
        Mock<IUserPaymentHistoryRepository> paymentRepo
    ) Build()
    {
        var subRepo = new Mock<ISubscriptionRepository>();
        var userRepo = new Mock<IUserRepository>();
        var paymentRepo = new Mock<IUserPaymentHistoryRepository>();
        var listRepo = new Mock<ListRepository>(Mock.Of<System.Data.IDbConnection>(), NullLogger<ListRepository>.Instance);
        listRepo.Setup(r => r.RestoreArchivedLists(It.IsAny<int>())).Returns(Task.CompletedTask);
        var stripeOptions = Options.Create(new StripeSettings
        {
            PremiumPriceId = "price_test_premium",
            CollaboratorPriceId = "price_test_collab"
        });
        var svc = new SubscriptionService(subRepo.Object, userRepo.Object, paymentRepo.Object, listRepo.Object, null!, stripeOptions, NullLogger<SubscriptionService>.Instance);
        return (svc, subRepo, userRepo, paymentRepo);
    }

    private static User MakeUser(int id, string sub = "free", string source = "free",
        DateTime? expiresAt = null, DateTime? gracePeriodUntil = null) => new()
    {
        Id = id,
        Email = $"user{id}@test.com",
        FirstName = "Test",
        LastName = "User",
        Subscription = sub,
        SubscriptionSource = source,
        SubscriptionExpiresAt = expiresAt,
        GracePeriodUntil = gracePeriodUntil
    };

    // === IsPremium ============================================================

    [Fact]
    public async Task IsPremium_ActivePremium_ReturnsTrue()
    {
        var (svc, subRepo, _, _) = Build();
        subRepo.Setup(r => r.IsPremium(1)).ReturnsAsync(true);

        var result = await svc.IsPremium(1);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsPremium_FreeUser_ReturnsFalse()
    {
        var (svc, subRepo, _, _) = Build();
        subRepo.Setup(r => r.IsPremium(1)).ReturnsAsync(false);

        var result = await svc.IsPremium(1);

        result.Should().BeFalse();
    }

    // === CanCreateList ========================================================

    [Fact]
    public async Task CanCreateList_PremiumUser_ReturnsTrue()
    {
        var (svc, subRepo, _, _) = Build();
        subRepo.Setup(r => r.IsPremium(1)).ReturnsAsync(true);

        var result = await svc.CanCreateList(1);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanCreateList_FreeUserUnderLimit_ReturnsTrue()
    {
        var (svc, subRepo, _, _) = Build();
        subRepo.Setup(r => r.IsPremium(1)).ReturnsAsync(false);
        subRepo.Setup(r => r.GetOwnedListCount(1)).ReturnsAsync(1);

        var result = await svc.CanCreateList(1);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanCreateList_FreeUserAtLimit_ReturnsFalse()
    {
        var (svc, subRepo, _, _) = Build();
        subRepo.Setup(r => r.IsPremium(1)).ReturnsAsync(false);
        subRepo.Setup(r => r.GetOwnedListCount(1)).ReturnsAsync(2);

        var result = await svc.CanCreateList(1);

        result.Should().BeFalse();
    }

    // === IsFirstCollaboratorSlot ==============================================

    [Fact]
    public async Task IsFirstCollaboratorSlot_NoFreeSlotTaken_ReturnsTrue()
    {
        var (svc, subRepo, _, _) = Build();
        subRepo.Setup(r => r.HasActiveFreeSeatCollaborator(1)).ReturnsAsync(false);

        var result = await svc.IsFirstCollaboratorSlot(1);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsFirstCollaboratorSlot_FreeSlotAlreadyTaken_ReturnsFalse()
    {
        var (svc, subRepo, _, _) = Build();
        subRepo.Setup(r => r.HasActiveFreeSeatCollaborator(1)).ReturnsAsync(true);

        var result = await svc.IsFirstCollaboratorSlot(1);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsFirstCollaboratorSlot_HasPaidSeatButNoFreeSlot_ReturnsTrue()
    {
        // Regression: before IsFreeSeat column, any active seat blocked the free slot.
        // Now we check only IsFreeSeat=1 records, so paid-only sponsors can still add a free slot.
        var (svc, subRepo, _, _) = Build();
        subRepo.Setup(r => r.HasActiveFreeSeatCollaborator(1)).ReturnsAsync(false); // no free seat despite paid seats existing

        var result = await svc.IsFirstCollaboratorSlot(1);

        result.Should().BeTrue();
    }

    // === AddFreeCollaboratorByEmail ===========================================

    [Fact]
    public async Task AddFreeCollaboratorByEmail_FreeSlotTaken_ReturnsFail()
    {
        var (svc, subRepo, _, _) = Build();
        subRepo.Setup(r => r.HasActiveFreeSeatCollaborator(1)).ReturnsAsync(true);

        var result = await svc.AddFreeCollaboratorByEmail(1, "target@test.com");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("free collaborator slot is already taken");
    }

    [Fact]
    public async Task AddFreeCollaboratorByEmail_AlreadySponsored_ReturnsFail()
    {
        var (svc, subRepo, userRepo, _) = Build();
        subRepo.Setup(r => r.HasActiveFreeSeatCollaborator(1)).ReturnsAsync(false);
        userRepo.Setup(r => r.GetUserAsync(1)).ReturnsAsync(Result<User>.Ok(MakeUser(1, "premium", "stripe")));
        subRepo.Setup(r => r.GetUserByEmail("target@test.com")).ReturnsAsync(MakeUser(2));
        subRepo.Setup(r => r.HasActiveSponsoredCollaborator(1, 2)).ReturnsAsync(true);

        var result = await svc.AddFreeCollaboratorByEmail(1, "target@test.com");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("already one of your sponsored collaborators");
    }

    // === AddPaidCollaboratorByEmail ===========================================

    [Fact]
    public async Task AddPaidCollaboratorByEmail_NonStripePremium_ReturnsFail()
    {
        // Sponsored and admin-granted users must be blocked from adding paid seats.
        var (svc, subRepo, userRepo, _) = Build();
        userRepo.Setup(r => r.GetUserAsync(1)).ReturnsAsync(Result<User>.Ok(MakeUser(1, "free", "sponsored")));

        var result = await svc.AddPaidCollaboratorByEmail(1, "target@test.com");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Paid collaborator seats require");
    }

    [Fact]
    public async Task AddPaidCollaboratorByEmail_AlreadySponsored_ReturnsFail()
    {
        var (svc, subRepo, userRepo, _) = Build();
        userRepo.Setup(r => r.GetUserAsync(1)).ReturnsAsync(Result<User>.Ok(MakeUser(1, "premium", "stripe")));
        subRepo.Setup(r => r.GetUserByEmail("target@test.com")).ReturnsAsync(MakeUser(2));
        subRepo.Setup(r => r.HasActiveSponsoredCollaborator(1, 2)).ReturnsAsync(true);

        var result = await svc.AddPaidCollaboratorByEmail(1, "target@test.com");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("already one of your sponsored collaborators");
    }

    // === SponsorCollaborator ==================================================

    [Fact]
    public async Task SponsorCollaborator_FirstSlot_AddsWithoutStripeCall()
    {
        var (svc, subRepo, _, _) = Build();
        subRepo.Setup(r => r.HasActiveFreeSeatCollaborator(1)).ReturnsAsync(false);
        subRepo.Setup(r => r.AddSponsoredCollaborator(1, 2, true)).Returns(Task.CompletedTask);

        var result = await svc.SponsorCollaborator(1, 2);

        result.Success.Should().BeTrue();
        subRepo.Verify(r => r.AddSponsoredCollaborator(1, 2, true), Times.Once);
    }


    // === RemoveSponsoredCollaborator ==========================================

    [Fact]
    public async Task RemoveSponsoredCollaborator_StartsSevenDayGrace()
    {
        var (svc, subRepo, userRepo, _) = Build();
        subRepo.Setup(r => r.StartSponsorshipGrace(1, 2, It.IsAny<DateTime>())).Returns(Task.CompletedTask);
        // Stripe seat removal fetches the sponsor — no StripeSubscriptionId means early return
        userRepo.Setup(r => r.GetUserAsync(1)).ReturnsAsync(Result<User>.Ok(MakeUser(1, "premium")));
        // Email lookup for collaborator — return failure so email path is skipped (this test doesn't verify email)
        userRepo.Setup(r => r.GetUserAsync(2)).ReturnsAsync(Result<User>.Fail("not found"));

        await svc.RemoveSponsoredCollaborator(1, 2);

        subRepo.Verify(r => r.StartSponsorshipGrace(
            1, 2,
            It.Is<DateTime>(d => d > DateTime.UtcNow.AddDays(6) && d < DateTime.UtcNow.AddDays(8))
        ), Times.Once);
    }

    [Fact]
    public async Task RemoveSponsoredCollaborator_RevokesListAccessImmediately()
    {
        var (svc, subRepo, userRepo, _) = Build();
        subRepo.Setup(r => r.StartSponsorshipGrace(1, 2, It.IsAny<DateTime>())).Returns(Task.CompletedTask);
        subRepo.Setup(r => r.RevokeSharedListAccess(1, 2)).Returns(Task.CompletedTask);
        userRepo.Setup(r => r.GetUserAsync(1)).ReturnsAsync(Result<User>.Ok(MakeUser(1, "premium")));
        userRepo.Setup(r => r.GetUserAsync(2)).ReturnsAsync(Result<User>.Fail("not found"));

        await svc.RemoveSponsoredCollaborator(1, 2);

        subRepo.Verify(r => r.RevokeSharedListAccess(1, 2), Times.Once);
    }

    // === FinalizeCollaboratorDowngrade ========================================

    [Fact]
    public async Task FinalizeCollaboratorDowngrade_FreeSlotTaken_RevokesListAccess()
    {
        var (svc, subRepo, _, _) = Build();
        subRepo.Setup(r => r.DeactivateSponsoredCollaborator(1, 2)).Returns(Task.CompletedTask);
        subRepo.Setup(r => r.GetFreeCollaboratorCount(1)).ReturnsAsync(1); // free slot occupied by someone else
        subRepo.Setup(r => r.RevokeSharedListAccess(1, 2)).Returns(Task.CompletedTask);

        await svc.FinalizeCollaboratorDowngrade(1, 2);

        subRepo.Verify(r => r.RevokeSharedListAccess(1, 2), Times.Once);
    }

    [Fact]
    public async Task FinalizeCollaboratorDowngrade_FreeSlotEmpty_RetainsListAccess()
    {
        var (svc, subRepo, _, _) = Build();
        subRepo.Setup(r => r.DeactivateSponsoredCollaborator(1, 2)).Returns(Task.CompletedTask);
        subRepo.Setup(r => r.GetFreeCollaboratorCount(1)).ReturnsAsync(0); // they become the free slot

        await svc.FinalizeCollaboratorDowngrade(1, 2);

        subRepo.Verify(r => r.RevokeSharedListAccess(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    // === HandleSponsorLapse ===================================================

    [Fact]
    public async Task HandleSponsorLapse_StartsGraceOnSponsorAndAllCollaborators()
    {
        var (svc, subRepo, _, _) = Build();
        subRepo.Setup(r => r.StartAllSponsorshipsGrace(1, It.IsAny<DateTime>())).Returns(Task.CompletedTask);
        subRepo.Setup(r => r.SetGracePeriod(1, It.IsAny<DateTime>())).Returns(Task.CompletedTask);

        await svc.HandleSponsorLapse(1);

        subRepo.Verify(r => r.StartAllSponsorshipsGrace(
            1, It.Is<DateTime>(d => d > DateTime.UtcNow.AddDays(6) && d < DateTime.UtcNow.AddDays(8))
        ), Times.Once);
        subRepo.Verify(r => r.SetGracePeriod(
            1, It.Is<DateTime>(d => d > DateTime.UtcNow.AddDays(6) && d < DateTime.UtcNow.AddDays(8))
        ), Times.Once);
    }

    // === FinalizeSponsorCancellation ==========================================

    [Fact]
    public async Task FinalizeSponsorCancellation_DeactivatesAllAndRevokesAccess()
    {
        var (svc, subRepo, _, _) = Build();
        subRepo.Setup(r => r.DeactivateAllSponsorships(1)).Returns(Task.CompletedTask);
        subRepo.Setup(r => r.RevokeAllSharedListAccess(1)).Returns(Task.CompletedTask);
        subRepo.Setup(r => r.SetUserSubscription(1, "free", "free", null)).Returns(Task.CompletedTask);

        await svc.FinalizeSponsorCancellation(1);

        subRepo.Verify(r => r.DeactivateAllSponsorships(1), Times.Once);
        subRepo.Verify(r => r.RevokeAllSharedListAccess(1), Times.Once);
        subRepo.Verify(r => r.SetUserSubscription(1, "free", "free", null), Times.Once);
    }

    // === Upgrade =============================================================

    [Fact]
    public async Task Upgrade_UserNotFound_ReturnsFail()
    {
        // Upgrade with real Stripe requires integration tests (live API call).
        // This unit test covers the guard path that doesn't touch Stripe.
        var (svc, subRepo, userRepo, _) = Build();
        userRepo.Setup(r => r.GetUserAsync(99)).ReturnsAsync(Result<User>.Fail("User not found."));

        var result = await svc.Upgrade(99, "nobody@test.com");

        result.Success.Should().BeFalse();
        result.Message.Should().Be("User not found.");
    }

    // === Cancel ==============================================================

    [Fact]
    public async Task Cancel_UserHasNoStripeSubscription_ReturnsFail()
    {
        // Admin-granted or gift premium users have no StripeSubscriptionId;
        // Cancel should return Fail — there is nothing to cancel in Stripe.
        var (svc, subRepo, userRepo, _) = Build();
        var user = MakeUser(1, "premium");
        user.StripeSubscriptionId = null;
        userRepo.Setup(r => r.GetUserAsync(1)).ReturnsAsync(Result<User>.Ok(user));

        var result = await svc.Cancel(1);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("No active Stripe subscription found.");
        subRepo.Verify(r => r.SetUserSubscription(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>()), Times.Never);
    }

    [Fact]
    public async Task Cancel_UserNotFound_ReturnsFail()
    {
        var (svc, subRepo, userRepo, _) = Build();
        userRepo.Setup(r => r.GetUserAsync(99)).ReturnsAsync(Result<User>.Fail("User not found."));

        var result = await svc.Cancel(99);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("User not found.");
    }

    // === ApplyPendingSponsorshipOnSignup =========================================

    [Fact]
    public async Task ApplyPendingSponsorshipOnSignup_WithPendingInvite_AddsAsFreeSeat()
    {
        var (svc, subRepo, _, _) = Build();
        subRepo.Setup(r => r.GetPendingSponsorInvitationByEmail("new@test.com"))
               .ReturnsAsync(((int SponsorUserId, string Token)?)(42, "tok123"));
        subRepo.Setup(r => r.AddSponsoredCollaborator(42, 99, true)).Returns(Task.CompletedTask);
        subRepo.Setup(r => r.DeletePendingSponsorInvitationByEmail("new@test.com")).Returns(Task.CompletedTask);

        await svc.ApplyPendingSponsorshipOnSignup(99, "new@test.com");

        subRepo.Verify(r => r.AddSponsoredCollaborator(42, 99, true), Times.Once);
        subRepo.Verify(r => r.DeletePendingSponsorInvitationByEmail("new@test.com"), Times.Once);
    }

    [Fact]
    public async Task ApplyPendingSponsorshipOnSignup_NoPendingInvite_DoesNothing()
    {
        var (svc, subRepo, _, _) = Build();
        subRepo.Setup(r => r.GetPendingSponsorInvitationByEmail("new@test.com"))
               .ReturnsAsync(((int SponsorUserId, string Token)?)null);

        await svc.ApplyPendingSponsorshipOnSignup(99, "new@test.com");

        subRepo.Verify(r => r.AddSponsoredCollaborator(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
    }

    // === GrantPremium / RevokePremium =========================================

    [Fact]
    public async Task GrantPremium_KnownEmail_SetsSubscription()
    {
        var (svc, subRepo, _, _) = Build();
        var user = MakeUser(5);
        subRepo.Setup(r => r.GetUserByEmail("user5@test.com")).ReturnsAsync(user);
        subRepo.Setup(r => r.SetUserSubscription(5, "premium", "gift", null)).Returns(Task.CompletedTask);

        var result = await svc.GrantPremium("user5@test.com", "gift", null);

        result.Success.Should().BeTrue();
        subRepo.Verify(r => r.SetUserSubscription(5, "premium", "gift", null), Times.Once);
    }

    [Fact]
    public async Task GrantPremium_UnknownEmail_ReturnsFail()
    {
        var (svc, subRepo, _, _) = Build();
        subRepo.Setup(r => r.GetUserByEmail("nobody@test.com")).ReturnsAsync((User?)null);

        var result = await svc.GrantPremium("nobody@test.com", "gift", null);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task RevokePremium_KnownEmail_SetsToFreeAndDeactivatesSponsorships()
    {
        var (svc, subRepo, _, _) = Build();
        var user = MakeUser(5, "premium");
        subRepo.Setup(r => r.GetUserByEmail("user5@test.com")).ReturnsAsync(user);
        subRepo.Setup(r => r.SetUserSubscription(5, "free", "free", null)).Returns(Task.CompletedTask);
        subRepo.Setup(r => r.DeactivateAllSponsorships(5)).Returns(Task.CompletedTask);

        var result = await svc.RevokePremium("user5@test.com");

        result.Success.Should().BeTrue();
        subRepo.Verify(r => r.DeactivateAllSponsorships(5), Times.Once);
    }
}
