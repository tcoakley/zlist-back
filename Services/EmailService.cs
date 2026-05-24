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
        private readonly UserRepository _userRepository;

        public EmailService(IConfiguration configuration, UserRepository userRepository)
        {
            var emailSettings = configuration.GetSection("EmailSettings");
            _smtpServer = emailSettings["SmtpServer"]!;
            _smtpPort = int.Parse(emailSettings["SmtpPort"]!);
            _senderEmail = emailSettings["SenderEmail"]!;
            _senderPassword = Environment.GetEnvironmentVariable("Email_SenderPassword")!;
            _userRepository = userRepository;
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
					        <p>We have created a temporary password for you. It is recommended that you change your password once you use it to log in.</p>
					        <p><strong>Temporary Password: <i>{passwordReset}</i></strong></p>",
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
