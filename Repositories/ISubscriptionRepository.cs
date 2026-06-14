using zListBack.Dtos;
using zListBack.Models;

namespace zListBack.Repositories
{
    public interface ISubscriptionRepository
    {
        Task<bool> IsPremium(int userId);
        Task<int> GetOwnedListCount(int userId);
        Task<int> GetActiveSponsoredCount(int sponsorUserId);
        Task<bool> HasActiveFreeSeatCollaborator(int sponsorUserId);
        Task<bool> HasActiveSponsoredCollaborator(int sponsorUserId, int sponsoredUserId);
        Task AddSponsoredCollaborator(int sponsorUserId, int sponsoredUserId, bool isFreeSeat);
        Task StartSponsorshipGrace(int sponsorUserId, int sponsoredUserId, DateTime graceUntil);
        Task DeactivateSponsoredCollaborator(int sponsorUserId, int sponsoredUserId);
        Task StartAllSponsorshipsGrace(int sponsorUserId, DateTime graceUntil);
        Task DeactivateAllSponsorships(int sponsorUserId);
        Task SetUserSubscription(int userId, string subscription, string source, DateTime? expiresAt);
        Task SetStripeIds(int userId, string stripeCustomerId, string stripeSubscriptionId);
        Task SetGracePeriod(int userId, DateTime? graceUntil);
        Task SetCancellationScheduled(int userId, DateTime? date);
        Task<int> GetFreeCollaboratorCount(int sponsorUserId);
        Task RevokeSharedListAccess(int sponsorUserId, int collaboratorUserId);
        Task RevokeAllSharedListAccess(int sponsorUserId);
        Task<User?> GetSponsor(int sponsoredUserId);
        Task<IEnumerable<SponsoredCollaboratorModel>> GetSponsoredCollaborators(int sponsorUserId);
        Task<bool> NeedsDowngradeSelection(int userId);
        Task<User?> GetUserByEmail(string email);
        Task<User?> GetUserByStripeCustomerId(string stripeCustomerId);

        // Pending sponsor invitations
        Task CreatePendingSponsorInvitation(int sponsorUserId, string email, string token, DateTime expiresAt);
        Task<IEnumerable<PendingSponsorInvitationModel>> GetPendingSponsorInvitations(int sponsorUserId);
        Task<(int SponsorUserId, string Token)?> GetPendingSponsorInvitationByEmail(string email);
        Task DeletePendingSponsorInvitation(int sponsorUserId, string email);
        Task DeletePendingSponsorInvitationByEmail(string email);

        // Daily cleanup queries
        Task<IEnumerable<User>> GetInactivePremiumUsers(int inactiveDays);
        Task SetInactivityNoticeSent(int userId);
        Task<IEnumerable<(User Sponsor, IEnumerable<User> Collaborators)>> GetSponsorsWithInactiveCollaborators(int inactiveDays);
        Task<IEnumerable<User>> GetUsersWithBillingInDays(int days);
        Task SetBillingReminderSent(int userId, DateTime periodEnd);
        Task<IEnumerable<(User Sponsor, User Collaborator)>> GetCollaboratorsWhoUpgradedThemselves();
    }
}
