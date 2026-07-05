using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Logging;
using zListBack.Models;
using zListBack.Repositories;

namespace zListBack.Services
{
    public class EmailService
    {

        private readonly string _smtpServer;
        private readonly int _smtpPort;
        private readonly string _senderEmail;
        private readonly string _senderPassword;
        private readonly string _baseUrl;
        private readonly IUserRepository _userRepository;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, IUserRepository userRepository, ILogger<EmailService> logger)
        {
            var emailSettings = configuration.GetSection("EmailSettings");
            _smtpServer = emailSettings["SmtpServer"]!;
            _smtpPort = int.Parse(emailSettings["SmtpPort"]!);
            _senderEmail = emailSettings["SenderEmail"]!;
            _senderPassword = Environment.GetEnvironmentVariable("Email_SenderPassword")!;
            _baseUrl = configuration["BaseUrl"] ?? "https://zchecklist.com";
            _userRepository = userRepository;
            _logger = logger;
        }

        private async Task SendAsync(string to, string subject, string body)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("zChecklist", _senderEmail));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = body };

            using var client = new SmtpClient();
            await client.ConnectAsync(_smtpServer, _smtpPort, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_senderEmail, _senderPassword);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }

        public virtual async Task<Result<bool>> SendWelcomeEmail(string recipientEmail, string firstName)
        {
            try
            {
                await SendAsync(recipientEmail, "Welcome to zChecklist", $@"
                    <p>Hi {firstName},</p>
                    <p>Welcome to zChecklist — your repeatable checklist app. Here's how to get started:</p>

                    <h3>Creating a list</h3>
                    <ol>
                        <li>From the <strong>Lists</strong> page, tap <strong>Create New List</strong>.</li>
                        <li>Give your list a name (and an optional description), then tap <strong>Create</strong>.</li>
                        <li>Open the list and add your items using the <strong>Add Item</strong> field. Drag items to reorder them.</li>
                    </ol>

                    <h3>Running a list</h3>
                    <ol>
                        <li>On the Lists page, tap the <strong>play button</strong> next to your list to start a new run.</li>
                        <li>Check off each item as you complete it. Your progress is saved automatically.</li>
                        <li>When everything is done, tap <strong>Complete</strong> to finish the run and record it in your history.</li>
                        <li>The list resets and is ready for your next run whenever you need it.</li>
                    </ol>

                    <p><a href=""{_baseUrl}/lists"">Go to your lists</a></p>
                    <p>If you have any questions, just reply to this email or use the Contact form in the app.</p>
                    <p>— The zChecklist Team</p>");
                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendWelcomeEmail failed. RecipientEmail={RecipientEmail}", recipientEmail);
                return Result<bool>.Fail(ex.Message);
            }
        }

        public virtual async Task<Result<bool>> SendInvitationEmail(string recipientEmail, string listName, string appBaseUrl, string token)
        {
            try
            {
                var inviteLink = $"{appBaseUrl}/invite/{token}";
                await SendAsync(recipientEmail, "You've been invited to a zChecklist list", $@"
                    <p>You've been invited to collaborate on the list <strong>{listName}</strong> in zChecklist.</p>
                    <p><a href=""{inviteLink}"">Click here to accept the invitation</a></p>
                    <p>This link expires in 7 days.</p>");
                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendInvitationEmail failed. RecipientEmail={RecipientEmail}", recipientEmail);
                return Result<bool>.Fail(ex.Message);
            }
        }

        public virtual async Task<Result<bool>> SendPremiumRequiredInvitationEmail(string recipientEmail, string listName, string invitedByName)
        {
            try
            {
                await SendAsync(recipientEmail, "You've been invited to a zChecklist list", $@"
                    <p>{invitedByName} has invited you to collaborate on the list <strong>{listName}</strong> in zChecklist.</p>
                    <p>To accept this invitation, you will need a <strong>zChecklist Premium account</strong> ($1.99/month).</p>
                    <p>Sign up at <a href=""{_baseUrl}"">zchecklist.com</a> and you will be able to accept the invitation once your account is active.</p>");
                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendPremiumRequiredInvitationEmail failed. RecipientEmail={RecipientEmail}", recipientEmail);
                return Result<bool>.Fail(ex.Message);
            }
        }

        public virtual async Task<Result<bool>> SendCollaboratorAddedEmail(
            string recipientEmail, string recipientFirstName,
            string sponsorName, bool isFreeSlot)
        {
            try
            {
                string body = isFreeSlot
                    ? $@"<p>Hi {recipientFirstName},</p>
                        <p><strong>{sponsorName}</strong> has added you as a collaborator on zChecklist.
                        They can share specific lists with you directly — any lists they add you to will
                        appear in your lists view.</p>
                        <h3>What you can do now</h3>
                        <ul>
                            <li>Collaborate in real time on any lists {sponsorName} shares with you</li>
                            <li>Check off items alongside other collaborators as they happen</li>
                            <li>Create and run up to 2 lists of your own for free</li>
                        </ul>
                        <h3>Want more?</h3>
                        <p>Upgrade to <strong>Premium ($1.99/month)</strong> to unlock:</p>
                        <ul>
                            <li>Unlimited lists of your own</li>
                            <li>Create and manage your own shared lists</li>
                            <li>Add your own free collaborator</li>
                        </ul>
                        <p><a href=""{_baseUrl}/account"">View plans and upgrade</a></p>
                        <p><a href=""{_baseUrl}/lists"">Go to your lists</a></p>
                        <p>— The zChecklist Team</p>"
                    : $@"<p>Hi {recipientFirstName},</p>
                        <p><strong>{sponsorName}</strong> has added you as a sponsored collaborator on zChecklist.
                        You now have full Premium access — unlimited lists, real-time collaboration, and the ability
                        to sponsor a collaborator of your own.</p>
                        <p>{sponsorName} can share specific lists with you directly. Any lists they add you to
                        will appear in your lists view.</p>
                        <p><a href=""{_baseUrl}/lists"">Go to your lists</a></p>
                        <p>— The zChecklist Team</p>";

                await SendAsync(recipientEmail, $"{sponsorName} added you as a collaborator on zChecklist", body);
                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendCollaboratorAddedEmail failed. RecipientEmail={RecipientEmail}", recipientEmail);
                return Result<bool>.Fail(ex.Message);
            }
        }

        public virtual async Task<Result<bool>> SendSponsorInvitationEmail(
            string recipientEmail, string sponsorName, bool includesPremium = true)
        {
            try
            {
                var signupUrl = $"{_baseUrl}/signup?inviteEmail={Uri.EscapeDataString(recipientEmail)}";
                string subject, body;
                if (includesPremium)
                {
                    subject = $"{sponsorName} invited you to zChecklist — premium access included";
                    body = $@"<p>Hi there,</p>
                        <p><strong>{sponsorName}</strong> has invited you to join zChecklist and is covering your Premium membership.</p>
                        <p>Create your free account to get started — no payment needed. Your premium access will be applied automatically once you sign up.</p>
                        <p><a href=""{signupUrl}"" style=""display:inline-block;padding:10px 20px;background:#228B22;color:white;text-decoration:none;border-radius:4px;"">Create Account</a></p>
                        <p>— The zChecklist Team</p>";
                }
                else
                {
                    subject = $"{sponsorName} invited you to collaborate on zChecklist";
                    body = $@"<p>Hi there,</p>
                        <p><strong>{sponsorName}</strong> has invited you to collaborate on zChecklist.</p>
                        <p>Create your free account to get started.</p>
                        <p><a href=""{signupUrl}"" style=""display:inline-block;padding:10px 20px;background:#228B22;color:white;text-decoration:none;border-radius:4px;"">Create Account</a></p>
                        <p>Once you've signed up, don't forget to ask {sponsorName} to add you to their lists from the Members tab.</p>
                        <p>— The zChecklist Team</p>";
                }
                await SendAsync(recipientEmail, subject, body);
                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendSponsorInvitationEmail failed. RecipientEmail={RecipientEmail}", recipientEmail);
                return Result<bool>.Fail(ex.Message);
            }
        }

        public virtual async Task<Result<bool>> SendCollaboratorRemovedEmail(
            string recipientEmail, string recipientFirstName,
            string sponsorName, DateTime graceUntil)
        {
            try
            {
                await SendAsync(recipientEmail, "Your zChecklist collaborator access is ending", $@"
                    <p>Hi {recipientFirstName},</p>
                    <p><strong>{sponsorName}</strong> has removed you as a collaborator on zChecklist.</p>
                    <p>You will retain access to any lists they have shared with you through
                    <strong>{graceUntil:MMMM d, yyyy}</strong>,
                    after which you will no longer be able to view or run those lists.</p>
                    <p>Your own lists and run history are not affected.</p>
                    <h3>Keep your full access</h3>
                    <p>Upgrade to <strong>Premium ($1.99/month)</strong> to create and manage your own shared lists
                    and add your own collaborators.</p>
                    <p><a href=""{_baseUrl}/account"">View plans and upgrade</a></p>
                    <p>— The zChecklist Team</p>");
                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendCollaboratorRemovedEmail failed. RecipientEmail={RecipientEmail}", recipientEmail);
                return Result<bool>.Fail(ex.Message);
            }
        }

        public virtual async Task<Result<bool>> SendSubscriptionActivatedEmail(string recipientEmail, string firstName)
        {
            try
            {
                await SendAsync(recipientEmail, "You're now on Premium — welcome!", $@"
                    <p>Hi {firstName},</p>
                    <p>Your zChecklist Premium subscription is now active. Here's what you have access to:</p>
                    <ul>
                        <li>Unlimited checklists</li>
                        <li>Create and manage shared lists</li>
                        <li>1 free collaborator included — invite them from your <a href=""{_baseUrl}/account"">Account page</a></li>
                        <li>Unlimited premium collaborators</li>
                        <li>Add additional collaborators for just $1/month each — they get the full Premium experience</li>
                        <li>7-day grace period on any billing issues</li>
                    </ul>
                    <p><a href=""{_baseUrl}/lists"">Go to your lists</a></p>
                    <p>— The zChecklist Team</p>");
                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendSubscriptionActivatedEmail failed. RecipientEmail={RecipientEmail}", recipientEmail);
                return Result<bool>.Fail(ex.Message);
            }
        }

        public virtual async Task<Result<bool>> SendPaymentFailedEmail(string recipientEmail, string firstName, DateTime lastAccessDate)
        {
            try
            {
                await SendAsync(recipientEmail, "Action needed — zChecklist payment issue", $@"
                    <p>Hi {firstName},</p>
                    <p>We weren't able to process your most recent zChecklist payment. These things happen —
                    please update your billing details to avoid any interruption to your service.</p>
                    <p>Your account will remain active until <strong>{lastAccessDate:MMMM d, yyyy}</strong>.
                    Updating your payment before that date will keep everything running without interruption.</p>
                    <p><a href=""{_baseUrl}/account"">Update billing details</a></p>
                    <p>If you have any questions, please don't hesitate to reach out.</p>
                    <p>— The zChecklist Team</p>");
                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendPaymentFailedEmail failed. RecipientEmail={RecipientEmail}", recipientEmail);
                return Result<bool>.Fail(ex.Message);
            }
        }

        public virtual async Task<Result<bool>> SendSubscriptionCancelledEmail(string recipientEmail, string firstName, DateTime lastAccessDate)
        {
            try
            {
                await SendAsync(recipientEmail, "Your zChecklist subscription has been cancelled", $@"
                    <p>Hi {firstName},</p>
                    <p>Your zChecklist Premium subscription has been cancelled. You will retain full access
                    to your lists and all Premium features until <strong>{lastAccessDate:MMMM d, yyyy}</strong>.</p>
                    <p>After that date your account will move to the free plan (up to 2 lists).
                    Your run history is always preserved.</p>
                    <p>We'd love to have you back anytime —
                    <a href=""{_baseUrl}/account"">reactivate Premium</a> whenever you're ready.</p>
                    <p>— The zChecklist Team</p>");
                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendSubscriptionCancelledEmail failed. RecipientEmail={RecipientEmail}", recipientEmail);
                return Result<bool>.Fail(ex.Message);
            }
        }

        public virtual async Task<Result<bool>> SendBillingReminderEmail(string recipientEmail, string firstName, DateTime billingDate, decimal amount)
        {
            try
            {
                await SendAsync(recipientEmail, "Your zChecklist billing is coming up", $@"
                    <p>Hi {firstName},</p>
                    <p>Just a heads-up — your next zChecklist Premium payment of
                    <strong>${amount:F2}</strong> is scheduled for <strong>{billingDate:MMMM d, yyyy}</strong>.</p>
                    <p>No action needed unless you'd like to make changes to your plan.</p>
                    <p><a href=""{_baseUrl}/account"">View your account</a></p>
                    <p>— The zChecklist Team</p>");
                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendBillingReminderEmail failed. RecipientEmail={RecipientEmail}", recipientEmail);
                return Result<bool>.Fail(ex.Message);
            }
        }

        public virtual async Task<Result<bool>> SendInactivityReminderEmail(string recipientEmail, string firstName, DateTime nextBillingDate)
        {
            try
            {
                await SendAsync(recipientEmail, "We haven't seen you in a while — zChecklist", $@"
                    <p>Hi {firstName},</p>
                    <p>We noticed you haven't used zChecklist in a while. Your Premium subscription
                    continues and your next billing date is <strong>{nextBillingDate:MMMM d, yyyy}</strong>.</p>
                    <p>Your lists and run history are right where you left them whenever you're ready.</p>
                    <p><a href=""{_baseUrl}/lists"">Go to your lists</a></p>
                    <p>If you no longer need Premium, you can cancel anytime from your
                    <a href=""{_baseUrl}/account"">Account page</a>.</p>
                    <p>— The zChecklist Team</p>");
                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendInactivityReminderEmail failed. RecipientEmail={RecipientEmail}", recipientEmail);
                return Result<bool>.Fail(ex.Message);
            }
        }

        public virtual async Task<Result<bool>> SendSponsorInactiveCollaboratorsEmail(
            string sponsorEmail, string sponsorFirstName,
            IEnumerable<string> inactiveCollaboratorNames, DateTime nextBillingDate)
        {
            try
            {
                var nameList = string.Join("", inactiveCollaboratorNames.Select(n => $"<li>{n}</li>"));
                await SendAsync(sponsorEmail, "Some of your zChecklist collaborators haven't been active", $@"
                    <p>Hi {sponsorFirstName},</p>
                    <p>The following collaborators you're sponsoring on zChecklist haven't been active in over 45 days:</p>
                    <ul>{nameList}</ul>
                    <p>You're currently paying for their access. If they no longer need it, you can
                    remove them from your <a href=""{_baseUrl}/account"">Account page</a> to reduce your monthly cost.</p>
                    <p>Your next billing date is <strong>{nextBillingDate:MMMM d, yyyy}</strong>.</p>
                    <p>— The zChecklist Team</p>");
                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendSponsorInactiveCollaboratorsEmail failed. SponsorEmail={SponsorEmail}", sponsorEmail);
                return Result<bool>.Fail(ex.Message);
            }
        }

        public virtual async Task<Result<bool>> SendCollaboratorUpgradedEmail(
            string sponsorEmail, string sponsorFirstName,
            string collaboratorName)
        {
            try
            {
                await SendAsync(sponsorEmail, $"{collaboratorName} now has their own Premium subscription", $@"
                    <p>Hi {sponsorFirstName},</p>
                    <p><strong>{collaboratorName}</strong> has upgraded to their own Premium subscription on zChecklist.</p>
                    <p>We've automatically released their seat from your account — you're no longer being charged for them.</p>
                    <p>— The zChecklist Team</p>");
                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendCollaboratorUpgradedEmail failed. SponsorEmail={SponsorEmail}", sponsorEmail);
                return Result<bool>.Fail(ex.Message);
            }
        }

        public virtual async Task<Result<bool>> SendFreeSeatReleasedEmail(
            string sponsorEmail, string sponsorFirstName, string collaboratorName)
        {
            try
            {
                await SendAsync(sponsorEmail, "Your free collaborator slot has been released", $@"
                    <p>Hi {sponsorFirstName},</p>
                    <p><strong>{collaboratorName}</strong> is now covered by a paid sponsorship, so their access is no longer dependent on your free slot.</p>
                    <p>Your free collaborator slot has been released — you can now add someone new from your <a href=""{_baseUrl}/account"">Account page</a>.</p>
                    <p>— The zChecklist Team</p>");
                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendFreeSeatReleasedEmail failed. SponsorEmail={SponsorEmail}", sponsorEmail);
                return Result<bool>.Fail(ex.Message);
            }
        }

        public virtual async Task<Result<bool>> SendAdminGrantedEmail(
            string recipientEmail, string firstName, string source, DateTime? expiresAt)
        {
            try
            {
                var expiryLine = expiresAt.HasValue
                    ? $"<p>Your access is active until <strong>{expiresAt.Value:MMMM d, yyyy}</strong>.</p>"
                    : string.Empty;
                await SendAsync(recipientEmail, "You've been given zChecklist Premium", $@"
                    <p>Hi {firstName},</p>
                    <p>Your zChecklist account has been upgraded to <strong>Premium</strong>. Here's what you now have access to:</p>
                    <ul>
                        <li>Unlimited checklists</li>
                        <li>Create and manage shared lists</li>
                        <li>1 free collaborator included</li>
                        <li>7-day grace period on any billing issues</li>
                    </ul>
                    {expiryLine}
                    <p><a href=""{_baseUrl}/lists"">Go to your lists</a></p>
                    <p>— The zChecklist Team</p>");
                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendAdminGrantedEmail failed. RecipientEmail={RecipientEmail}", recipientEmail);
                return Result<bool>.Fail(ex.Message);
            }
        }

        public virtual async Task<Result<bool>> SendAdminRevokedEmail(string recipientEmail, string firstName)
        {
            try
            {
                await SendAsync(recipientEmail, "Your zChecklist Premium access has ended", $@"
                    <p>Hi {firstName},</p>
                    <p>Your zChecklist Premium access has been removed. Your account is now on the free plan,
                    which includes up to 2 checklists and full run history.</p>
                    <p>Your existing lists and run history are not affected.</p>
                    <p>If you'd like to continue with Premium, you can subscribe anytime from your
                    <a href=""{_baseUrl}/account"">Account page</a>.</p>
                    <p>— The zChecklist Team</p>");
                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendAdminRevokedEmail failed. RecipientEmail={RecipientEmail}", recipientEmail);
                return Result<bool>.Fail(ex.Message);
            }
        }

        public virtual async Task<Result<bool>> SendContactEmail(int userId, string userEmail, string firstName, string lastName, string contactType, string message)
        {
            try
            {
                await SendAsync(_senderEmail, $"Site Contact - {contactType}", $@"
                    <p><strong>From:</strong> {firstName} {lastName} ({userEmail})</p>
                    <p><strong>User ID:</strong> {userId}</p>
                    <p><strong>Type:</strong> {contactType}</p>
                    <hr/>
                    <p style=""white-space: pre-wrap"">{System.Net.WebUtility.HtmlEncode(message)}</p>");
                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendContactEmail failed. UserEmail={UserEmail}, UserId={UserId}", userEmail, userId);
                return Result<bool>.Fail(ex.Message);
            }
        }

        public virtual async Task<Result<bool>> SendPaidSeatAccountCreatedEmail(
            string recipientEmail, string sponsorName, string tempPassword)
        {
            try
            {
                await SendAsync(recipientEmail, $"{sponsorName} set up a Premium zChecklist account for you", $@"
                    <p>Hi there,</p>
                    <p><strong>{sponsorName}</strong> has created a Premium zChecklist account for you and is covering your monthly access.</p>
                    <p>Your account is ready. Use the temporary password below to log in, then update it from your Profile.</p>
                    <p style=""font-size:1.1em;"">Email: <strong>{recipientEmail}</strong><br/>
                    Temporary password: <strong><code>{tempPassword}</code></strong></p>
                    <p><a href=""{_baseUrl}/login"" style=""display:inline-block;padding:10px 20px;background:#228B22;color:white;text-decoration:none;border-radius:4px;"">Log in to zChecklist</a></p>
                    <p>— The zChecklist Team</p>");
                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendPaidSeatAccountCreatedEmail failed. RecipientEmail={RecipientEmail}", recipientEmail);
                return Result<bool>.Fail(ex.Message);
            }
        }

        public virtual async Task<Result<bool>> SendAccountDeletedEmail(string recipientEmail, string firstName)
        {
            try
            {
                await SendAsync(recipientEmail, "Your zChecklist account has been deleted", $@"
                    <p>Hi {firstName},</p>
                    <p>Your zChecklist account has been permanently deleted. All of your lists, run history,
                    and account data have been removed from our system.</p>
                    <p>If you had an active subscription, it has been cancelled and you will not be charged again.</p>
                    <p>We're sorry to see you go. If you ever want to come back, you're always welcome to
                    create a new account at <a href=""{_baseUrl}/signup"">{_baseUrl}</a>.</p>
                    <p>— The zChecklist Team</p>");
                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendAccountDeletedEmail failed. RecipientEmail={RecipientEmail}", recipientEmail);
                return Result<bool>.Fail(ex.Message);
            }
        }

        public virtual async Task<Result<string>> SendForgotPasswordEmail(string recipientEmail)
        {
            try
            {
                var result = await _userRepository.GenerateResetPassword(recipientEmail);
                if (!result.Success) 
                {
                    return result;
                }
                var passwordReset = result.Model as string;
                await SendAsync(recipientEmail, "Password Reset Request", $@"
                    <p>Hi,</p>
                    <p>We received a request to reset your zChecklist password. We've created a temporary password for you below.</p>
                    <p style=""font-size: 1.1em;"">
                        Temporary password: <strong><code>{passwordReset}</code></strong>
                    </p>
                    <p>Once you log in with this password, we recommend updating it from your <strong>Profile</strong> page.</p>
                    <p><a href=""{_baseUrl}/login"">Log in to zChecklist</a></p>
                    <p>If you didn't request a password reset, you can safely ignore this email.</p>
                    <p>— The zChecklist Team</p>");
                return Result<string>.Ok("If the email is registered, you will receive a password reset email shortly.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendForgotPasswordEmail failed. RecipientEmail={RecipientEmail}", recipientEmail);
                return Result<string>.Fail(ex.Message);
            }
        }
    }
}
