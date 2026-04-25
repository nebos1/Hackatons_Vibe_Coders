using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;

namespace EventsApp.Services
{
    public class EmailSender : IEmailSender
    {
        private readonly EmailSettings _settings;
        private readonly ILogger<EmailSender> _logger;
        private readonly IWebHostEnvironment _env;

        public EmailSender(
            IOptions<EmailSettings> settings,
            ILogger<EmailSender> logger,
            IWebHostEnvironment env)
        {
            _settings = settings.Value;
            _logger = logger;
            _env = env;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            var smtp = _settings.Smtp;
            var from = _settings.FromAddress;
            var hasSmtpConfig = !string.IsNullOrWhiteSpace(smtp.Host)
                                 && !string.IsNullOrWhiteSpace(from);

            if (!hasSmtpConfig)
            {
                await WriteToDevDropAsync(email, subject, htmlMessage);
                return;
            }

            using var message = new MailMessage
            {
                From = new MailAddress(from!, _settings.FromName ?? "EventsApp"),
                Subject = subject,
                Body = htmlMessage,
                IsBodyHtml = true,
            };
            message.To.Add(email);

            using var client = new SmtpClient(smtp.Host!, smtp.Port)
            {
                EnableSsl = smtp.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
            };

            if (!string.IsNullOrWhiteSpace(smtp.UserName))
            {
                client.UseDefaultCredentials = false;
                client.Credentials = new NetworkCredential(smtp.UserName, smtp.Password);
            }

            try
            {
                await client.SendMailAsync(message);
                _logger.LogInformation("Sent email to {Email} subject={Subject}", email, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SMTP send failed for {Email}. Falling back to dev drop.", email);
                await WriteToDevDropAsync(email, subject, htmlMessage);
            }
        }

        private async Task WriteToDevDropAsync(string email, string subject, string htmlMessage)
        {
            var dir = Path.Combine(_env.ContentRootPath, "App_Data", "emails");
            Directory.CreateDirectory(dir);
            var safeEmail = string.Concat(email.Where(c => char.IsLetterOrDigit(c) || c == '@' || c == '.'));
            var fileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{safeEmail}.html";
            var path = Path.Combine(dir, fileName);
            var content = $"<!-- To: {email} -->\n<!-- Subject: {subject} -->\n{htmlMessage}";
            await File.WriteAllTextAsync(path, content);
            _logger.LogWarning(
                "SMTP not configured. Wrote email to {Path} (To: {Email}, Subject: {Subject}).",
                path, email, subject);
        }
    }
}