using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace zChecklist.Services
{
    public class EmailService
    {
        private const string SmtpServer = "smtp.gmail.com";
        private const int SmtpPort = 587;
        private const string SenderEmail = "zchecklistteam@gmail.com";
        private const string SenderPassword = "MpiB00mk1n";

        public async Task SendForgotPasswordEmail(string recipientEmail)
        {
            try
            {
                using (var client = new SmtpClient(SmtpServer, SmtpPort))
                {
                    client.Credentials = new NetworkCredential(SenderEmail, SenderPassword);
                    client.EnableSsl = true;

                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(SenderEmail),
                        Subject = "Password Reset Request",
                        Body = $"Password reset information belongs here.",
                        IsBodyHtml = true
                    };
                    mailMessage.To.Add(recipientEmail);

                    await client.SendMailAsync(mailMessage);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending email: {ex.Message}");
            }
        }
    }
}
