using System;
using System.Net;
using System.Net.Mail;
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

        public EmailService(IConfiguration configuration, IUserRepository userRepository)
        {
            var emailSettings = configuration.GetSection("EmailSettings");
            _smtpServer = emailSettings["SmtpServer"]!;
            _smtpPort = int.Parse(emailSettings["SmtpPort"]!);
            _senderEmail = emailSettings["SenderEmail"]!;
            _senderPassword = Environment.GetEnvironmentVariable("Email_SenderPassword")!;
            _baseUrl = configuration["BaseUrl"] ?? "https://zchecklist.com";
            _userRepository = userRepository;
        }

        public virtual async Task<Result<bool>> SendWelcomeEmail(string recipientEmail, string firstName)
        {
            try
            {
                using var client = new SmtpClient(_smtpServer, _smtpPort);
                client.Credentials = new NetworkCredential(_senderEmail, _senderPassword);
                client.EnableSsl = true;

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_senderEmail),
                    Subject = "Welcome to zChecklist",
                    Body = $@"
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
                        <p>— The zChecklist Team</p>",
                    IsBodyHtml = true
                };
                mailMessage.To.Add(recipientEmail);

                await client.SendMailAsync(mailMessage);
                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail(ex.Message);
            }
        }

        public virtual async Task<Result<bool>> SendInvitationEmail(string recipientEmail, string listName, string appBaseUrl, string token)
        {
            try
            {
                var inviteLink = $"{appBaseUrl}/invite/{token}";
                using var client = new SmtpClient(_smtpServer, _smtpPort);
                client.Credentials = new NetworkCredential(_senderEmail, _senderPassword);
                client.EnableSsl = true;

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_senderEmail),
                    Subject = $"You've been invited to a zChecklist list",
                    Body = $@"
                        <p>You've been invited to collaborate on the list <strong>{listName}</strong> in zChecklist.</p>
                        <p><a href=""{inviteLink}"">Click here to accept the invitation</a></p>
                        <p>This link expires in 7 days.</p>",
                    IsBodyHtml = true
                };
                mailMessage.To.Add(recipientEmail);

                await client.SendMailAsync(mailMessage);

                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail(ex.Message);
            }
        }

        public virtual async Task<Result<bool>> SendPremiumRequiredInvitationEmail(string recipientEmail, string listName, string invitedByName)
        {
            try
            {
                using var client = new SmtpClient(_smtpServer, _smtpPort);
                client.Credentials = new NetworkCredential(_senderEmail, _senderPassword);
                client.EnableSsl = true;

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_senderEmail),
                    Subject = $"You've been invited to a zChecklist list",
                    Body = $@"
                        <p>{invitedByName} has invited you to collaborate on the list <strong>{listName}</strong> in zChecklist.</p>
                        <p>To accept this invitation, you will need a <strong>zChecklist Premium account</strong> ($1.99/month).</p>
                        <p>Sign up at <a href=""https://zchecklist.com"">zchecklist.com</a> and you will be able to accept the invitation once your account is active.</p>",
                    IsBodyHtml = true
                };
                mailMessage.To.Add(recipientEmail);

                await client.SendMailAsync(mailMessage);
                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail(ex.Message);
            }
        }

        public virtual async Task<Result<bool>> SendCollaboratorAddedEmail(
            string recipientEmail, string recipientFirstName,
            string sponsorName, bool isFreeSlot)
        {
            try
            {
                using var client = new SmtpClient(_smtpServer, _smtpPort);
                client.Credentials = new NetworkCredential(_senderEmail, _senderPassword);
                client.EnableSsl = true;

                string body;
                if (isFreeSlot)
                {
                    body = $@"
                        <p>Hi {recipientFirstName},</p>
                        <p><strong>{sponsorName}</strong> has added you as a collaborator on zChecklist.
                        They will be able to invite you to specific lists they want to share with you —
                        you'll receive a separate invitation for each one.</p>

                        <h3>What you can do now</h3>
                        <ul>
                            <li>Accept list invitations from {sponsorName} when they arrive</li>
                            <li>Check off items in real time alongside other collaborators</li>
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
                        <p>— The zChecklist Team</p>";
                }
                else
                {
                    body = $@"
                        <p>Hi {recipientFirstName},</p>
                        <p><strong>{sponsorName}</strong> has added you as a collaborator on zChecklist.
                        They will invite you to specific lists they want to share — you'll receive a
                        separate invitation for each one.</p>
                        <p><a href=""{_baseUrl}/lists"">Go to your lists</a></p>
                        <p>— The zChecklist Team</p>";
                }

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_senderEmail),
                    Subject = $"{sponsorName} added you as a collaborator on zChecklist",
                    Body = body,
                    IsBodyHtml = true
                };
                mailMessage.To.Add(recipientEmail);

                await client.SendMailAsync(mailMessage);
                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail(ex.Message);
            }
        }

        public virtual async Task<Result<bool>> SendCollaboratorRemovedEmail(
            string recipientEmail, string recipientFirstName,
            string sponsorName, DateTime graceUntil)
        {
            try
            {
                using var client = new SmtpClient(_smtpServer, _smtpPort);
                client.Credentials = new NetworkCredential(_senderEmail, _senderPassword);
                client.EnableSsl = true;

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_senderEmail),
                    Subject = "Your zChecklist collaborator access is ending",
                    Body = $@"
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

                        <p>— The zChecklist Team</p>",
                    IsBodyHtml = true
                };
                mailMessage.To.Add(recipientEmail);

                await client.SendMailAsync(mailMessage);
                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail(ex.Message);
            }
        }

        public virtual async Task<Result<bool>> SendSubscriptionActivatedEmail(string recipientEmail, string firstName)
        {
            try
            {
                using var client = new SmtpClient(_smtpServer, _smtpPort);
                client.Credentials = new NetworkCredential(_senderEmail, _senderPassword);
                client.EnableSsl = true;

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_senderEmail),
                    Subject = "You're now on Premium — welcome!",
                    Body = $@"
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
                        <p>— The zChecklist Team</p>",
                    IsBodyHtml = true
                };
                mailMessage.To.Add(recipientEmail);
                await client.SendMailAsync(mailMessage);
                return Result<bool>.Ok(true);
            }
            catch (Exception ex) { return Result<bool>.Fail(ex.Message); }
        }

        public virtual async Task<Result<bool>> SendPaymentFailedEmail(string recipientEmail, string firstName, DateTime lastAccessDate)
        {
            try
            {
                using var client = new SmtpClient(_smtpServer, _smtpPort);
                client.Credentials = new NetworkCredential(_senderEmail, _senderPassword);
                client.EnableSsl = true;

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_senderEmail),
                    Subject = "Action needed — zChecklist payment issue",
                    Body = $@"
                        <p>Hi {firstName},</p>
                        <p>We weren't able to process your most recent zChecklist payment. These things happen —
                        please update your billing details to avoid any interruption to your service.</p>
                        <p>Your account will remain active until <strong>{lastAccessDate:MMMM d, yyyy}</strong>.
                        Updating your payment before that date will keep everything running without interruption.</p>
                        <p><a href=""{_baseUrl}/account"">Update billing details</a></p>
                        <p>If you have any questions, please don't hesitate to reach out.</p>
                        <p>— The zChecklist Team</p>",
                    IsBodyHtml = true
                };
                mailMessage.To.Add(recipientEmail);
                await client.SendMailAsync(mailMessage);
                return Result<bool>.Ok(true);
            }
            catch (Exception ex) { return Result<bool>.Fail(ex.Message); }
        }

        public virtual async Task<Result<bool>> SendSubscriptionCancelledEmail(string recipientEmail, string firstName, DateTime lastAccessDate)
        {
            try
            {
                using var client = new SmtpClient(_smtpServer, _smtpPort);
                client.Credentials = new NetworkCredential(_senderEmail, _senderPassword);
                client.EnableSsl = true;

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_senderEmail),
                    Subject = "Your zChecklist subscription has been cancelled",
                    Body = $@"
                        <p>Hi {firstName},</p>
                        <p>Your zChecklist Premium subscription has been cancelled. You will retain full access
                        to your lists and all Premium features until <strong>{lastAccessDate:MMMM d, yyyy}</strong>.</p>
                        <p>After that date your account will move to the free plan (up to 2 lists).
                        Your run history is always preserved.</p>
                        <p>We'd love to have you back anytime —
                        <a href=""{_baseUrl}/account"">reactivate Premium</a> whenever you're ready.</p>
                        <p>— The zChecklist Team</p>",
                    IsBodyHtml = true
                };
                mailMessage.To.Add(recipientEmail);
                await client.SendMailAsync(mailMessage);
                return Result<bool>.Ok(true);
            }
            catch (Exception ex) { return Result<bool>.Fail(ex.Message); }
        }

        public virtual async Task<Result<bool>> SendBillingReminderEmail(string recipientEmail, string firstName, DateTime billingDate, decimal amount)
        {
            try
            {
                using var client = new SmtpClient(_smtpServer, _smtpPort);
                client.Credentials = new NetworkCredential(_senderEmail, _senderPassword);
                client.EnableSsl = true;

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_senderEmail),
                    Subject = "Your zChecklist billing is coming up",
                    Body = $@"
                        <p>Hi {firstName},</p>
                        <p>Just a heads-up — your next zChecklist Premium payment of
                        <strong>${amount:F2}</strong> is scheduled for <strong>{billingDate:MMMM d, yyyy}</strong>.</p>
                        <p>No action needed unless you'd like to make changes to your plan.</p>
                        <p><a href=""{_baseUrl}/account"">View your account</a></p>
                        <p>— The zChecklist Team</p>",
                    IsBodyHtml = true
                };
                mailMessage.To.Add(recipientEmail);
                await client.SendMailAsync(mailMessage);
                return Result<bool>.Ok(true);
            }
            catch (Exception ex) { return Result<bool>.Fail(ex.Message); }
        }

        public virtual async Task<Result<bool>> SendInactivityReminderEmail(string recipientEmail, string firstName, DateTime nextBillingDate)
        {
            try
            {
                using var client = new SmtpClient(_smtpServer, _smtpPort);
                client.Credentials = new NetworkCredential(_senderEmail, _senderPassword);
                client.EnableSsl = true;

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_senderEmail),
                    Subject = "We haven't seen you in a while — zChecklist",
                    Body = $@"
                        <p>Hi {firstName},</p>
                        <p>We noticed you haven't used zChecklist in a while. Your Premium subscription
                        continues and your next billing date is <strong>{nextBillingDate:MMMM d, yyyy}</strong>.</p>
                        <p>Your lists and run history are right where you left them whenever you're ready.</p>
                        <p><a href=""{_baseUrl}/lists"">Go to your lists</a></p>
                        <p>If you no longer need Premium, you can cancel anytime from your
                        <a href=""{_baseUrl}/account"">Account page</a>.</p>
                        <p>— The zChecklist Team</p>",
                    IsBodyHtml = true
                };
                mailMessage.To.Add(recipientEmail);
                await client.SendMailAsync(mailMessage);
                return Result<bool>.Ok(true);
            }
            catch (Exception ex) { return Result<bool>.Fail(ex.Message); }
        }

        public virtual async Task<Result<bool>> SendSponsorInactiveCollaboratorsEmail(
            string sponsorEmail, string sponsorFirstName,
            IEnumerable<string> inactiveCollaboratorNames, DateTime nextBillingDate)
        {
            try
            {
                using var client = new SmtpClient(_smtpServer, _smtpPort);
                client.Credentials = new NetworkCredential(_senderEmail, _senderPassword);
                client.EnableSsl = true;

                var nameList = string.Join("", inactiveCollaboratorNames.Select(n => $"<li>{n}</li>"));

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_senderEmail),
                    Subject = "Some of your zChecklist collaborators haven't been active",
                    Body = $@"
                        <p>Hi {sponsorFirstName},</p>
                        <p>The following collaborators you're sponsoring on zChecklist haven't been active in over 45 days:</p>
                        <ul>{nameList}</ul>
                        <p>You're currently paying for their access. If they no longer need it, you can
                        remove them from your <a href=""{_baseUrl}/account"">Account page</a> to reduce your monthly cost.</p>
                        <p>Your next billing date is <strong>{nextBillingDate:MMMM d, yyyy}</strong>.</p>
                        <p>— The zChecklist Team</p>",
                    IsBodyHtml = true
                };
                sponsorEmail.Split(',').ToList().ForEach(e => mailMessage.To.Add(e.Trim()));
                mailMessage.To.Clear();
                mailMessage.To.Add(sponsorEmail);
                await client.SendMailAsync(mailMessage);
                return Result<bool>.Ok(true);
            }
            catch (Exception ex) { return Result<bool>.Fail(ex.Message); }
        }

        public virtual async Task<Result<bool>> SendCollaboratorUpgradedEmail(
            string sponsorEmail, string sponsorFirstName,
            string collaboratorName)
        {
            try
            {
                using var client = new SmtpClient(_smtpServer, _smtpPort);
                client.Credentials = new NetworkCredential(_senderEmail, _senderPassword);
                client.EnableSsl = true;

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_senderEmail),
                    Subject = $"{collaboratorName} now has their own Premium subscription",
                    Body = $@"
                        <p>Hi {sponsorFirstName},</p>
                        <p><strong>{collaboratorName}</strong>, one of the collaborators you sponsor on zChecklist,
                        has upgraded to their own Premium subscription.</p>
                        <p>You may want to remove them as a sponsored collaborator on your
                        <a href=""{_baseUrl}/account"">Account page</a> — their access is no longer dependent on you,
                        and removing them would reduce your monthly cost.</p>
                        <p>— The zChecklist Team</p>",
                    IsBodyHtml = true
                };
                mailMessage.To.Add(sponsorEmail);
                await client.SendMailAsync(mailMessage);
                return Result<bool>.Ok(true);
            }
            catch (Exception ex) { return Result<bool>.Fail(ex.Message); }
        }

        public virtual async Task<Result<bool>> SendAdminGrantedEmail(
            string recipientEmail, string firstName, string source, DateTime? expiresAt)
        {
            try
            {
                using var client = new SmtpClient(_smtpServer, _smtpPort);
                client.Credentials = new NetworkCredential(_senderEmail, _senderPassword);
                client.EnableSsl = true;

                var expiryLine = expiresAt.HasValue
                    ? $"<p>Your access is active until <strong>{expiresAt.Value:MMMM d, yyyy}</strong>.</p>"
                    : string.Empty;

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_senderEmail),
                    Subject = "You've been given zChecklist Premium",
                    Body = $@"
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
                        <p>— The zChecklist Team</p>",
                    IsBodyHtml = true
                };
                mailMessage.To.Add(recipientEmail);
                await client.SendMailAsync(mailMessage);
                return Result<bool>.Ok(true);
            }
            catch (Exception ex) { return Result<bool>.Fail(ex.Message); }
        }

        public virtual async Task<Result<bool>> SendAdminRevokedEmail(string recipientEmail, string firstName)
        {
            try
            {
                using var client = new SmtpClient(_smtpServer, _smtpPort);
                client.Credentials = new NetworkCredential(_senderEmail, _senderPassword);
                client.EnableSsl = true;

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_senderEmail),
                    Subject = "Your zChecklist Premium access has ended",
                    Body = $@"
                        <p>Hi {firstName},</p>
                        <p>Your zChecklist Premium access has been removed. Your account is now on the free plan,
                        which includes up to 2 checklists and full run history.</p>
                        <p>Your existing lists and run history are not affected.</p>
                        <p>If you'd like to continue with Premium, you can subscribe anytime from your
                        <a href=""{_baseUrl}/account"">Account page</a>.</p>
                        <p>— The zChecklist Team</p>",
                    IsBodyHtml = true
                };
                mailMessage.To.Add(recipientEmail);
                await client.SendMailAsync(mailMessage);
                return Result<bool>.Ok(true);
            }
            catch (Exception ex) { return Result<bool>.Fail(ex.Message); }
        }

        public virtual async Task<Result<bool>> SendContactEmail(int userId, string userEmail, string firstName, string lastName, string contactType, string message)
        {
            try
            {
                using var client = new SmtpClient(_smtpServer, _smtpPort);
                client.Credentials = new NetworkCredential(_senderEmail, _senderPassword);
                client.EnableSsl = true;

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_senderEmail),
                    Subject = $"Site Contact - {contactType}",
                    Body = $@"
                        <p><strong>From:</strong> {firstName} {lastName} ({userEmail})</p>
                        <p><strong>User ID:</strong> {userId}</p>
                        <p><strong>Type:</strong> {contactType}</p>
                        <hr/>
                        <p style=""white-space: pre-wrap"">{System.Net.WebUtility.HtmlEncode(message)}</p>",
                    IsBodyHtml = true
                };
                mailMessage.To.Add(_senderEmail);

                await client.SendMailAsync(mailMessage);
                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
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
                using (var client = new SmtpClient(_smtpServer, _smtpPort))
                {
                    client.Credentials = new NetworkCredential(_senderEmail, _senderPassword);
                    client.EnableSsl = true;

                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(_senderEmail),
                        Subject = "Password Reset Request",
                        Body = $@"
                        <p>Hi,</p>
                        <p>We received a request to reset your zChecklist password. We've created a temporary password for you below.</p>

                        <p style=""font-size: 1.1em;"">
                            Temporary password: <strong><code>{passwordReset}</code></strong>
                        </p>

                        <p>Once you log in with this password, we recommend updating it from your <strong>Profile</strong> page.</p>
                        <p><a href=""{_baseUrl}/login"">Log in to zChecklist</a></p>
                        <p>If you didn't request a password reset, you can safely ignore this email.</p>
                        <p>— The zChecklist Team</p>",
                        IsBodyHtml = true
                    };
                    mailMessage.To.Add(recipientEmail);

                    await client.SendMailAsync(mailMessage);

                    return Result<string>.Ok("If the email is registered, you will receive a password reset email shortly.");
                }
            }
            catch (Exception ex)
            {
                return Result<string>.Fail(ex.Message);
            }
        }
    }
}
