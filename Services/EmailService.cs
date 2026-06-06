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
        private readonly UserRepository _userRepository;

        public EmailService(IConfiguration configuration, UserRepository userRepository)
        {
            var emailSettings = configuration.GetSection("EmailSettings");
            _smtpServer = emailSettings["SmtpServer"]!;
            _smtpPort = int.Parse(emailSettings["SmtpPort"]!);
            _senderEmail = emailSettings["SenderEmail"]!;
            _senderPassword = Environment.GetEnvironmentVariable("Email_SenderPassword")!;
            _baseUrl = configuration["BaseUrl"] ?? "https://zchecklist.com";
            _userRepository = userRepository;
        }

        public async Task<Result<bool>> SendWelcomeEmail(string recipientEmail, string firstName)
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

        public async Task<Result<bool>> SendInvitationEmail(string recipientEmail, string listName, string appBaseUrl, string token)
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

        public async Task<Result<bool>> SendPremiumRequiredInvitationEmail(string recipientEmail, string listName, string invitedByName)
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

        public async Task<Result<bool>> SendCollaboratorAddedEmail(
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
                        <p><strong>{sponsorName}</strong> has added you as a collaborator on their zChecklist lists.
                        You now have access to all of their shared lists at no cost to you.</p>

                        <h3>What you can do now</h3>
                        <ul>
                            <li>Join and run any list {sponsorName} shares with you</li>
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
                        <p><strong>{sponsorName}</strong> has added you as a collaborator on their zChecklist lists.
                        Your access is covered by {sponsorName} and you can join all of their shared lists.</p>
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

        public async Task<Result<bool>> SendCollaboratorRemovedEmail(
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
                        <p><strong>{sponsorName}</strong> has removed you as a collaborator on their zChecklist lists.</p>
                        <p>You will retain access to their shared lists through <strong>{graceUntil:MMMM d, yyyy}</strong>,
                        after which you will no longer be able to view or run them.</p>
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

        public async Task<Result<bool>> SendContactEmail(int userId, string userEmail, string firstName, string lastName, string contactType, string message)
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

        public async Task<Result<string>> SendForgotPasswordEmail(string recipientEmail)
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
