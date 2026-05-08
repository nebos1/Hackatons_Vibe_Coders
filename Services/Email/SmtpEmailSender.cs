using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace EventsApp.Services.Email
{
    public class SmtpEmailSender : IEmailSender
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SmtpEmailSender> _logger;

        public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            if (!GetBool(false, "EMAIL_ENABLED", "Email:Enabled"))
            {
                _logger.LogInformation("Email sending is disabled. Skipping message to {Email} with subject {Subject}.", email, subject);
                return;
            }

            var host = GetSetting("Email:Smtp:Host", "SMTP_HOST");
            var username = GetSetting("Email:Smtp:Username", "SMTP_USERNAME", "SMTP_USER");
            var fromEmail = GetSetting("Email:From:Email", "SMTP_FROM_EMAIL") ?? username;
            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(fromEmail))
            {
                _logger.LogWarning("Email is enabled but SMTP host or from email is missing.");
                return;
            }

            var port = GetInt(587, "Email:Smtp:Port", "SMTP_PORT");
            var enableSsl = GetBool(true, "SMTP_ENABLE_SSL", "SMTP_SSL", "Email:Smtp:EnableSsl");
            var password = GetSetting("Email:Smtp:Password", "SMTP_PASSWORD", "SMTP_PASS");
            var fromName = GetSetting("Email:From:Name", "SMTP_FROM_NAME") ?? "Evento";

            using var client = new SmtpClient(host, port)
            {
                EnableSsl = enableSsl,
                UseDefaultCredentials = false,
            };

            if (!string.IsNullOrWhiteSpace(username))
            {
                client.Credentials = new NetworkCredential(username, password);
            }

            using var message = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName),
                Subject = subject,
                Body = htmlMessage,
                IsBodyHtml = true,
            };
            message.To.Add(email);

            await client.SendMailAsync(message);
        }

        private string? GetSetting(params string[] keys)
        {
            foreach (var key in keys)
            {
                var value = _configuration[key];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }

        private int GetInt(int fallback, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (int.TryParse(_configuration[key], out var value))
                {
                    return value;
                }
            }

            return fallback;
        }

        private bool GetBool(bool fallback, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (bool.TryParse(_configuration[key], out var value))
                {
                    return value;
                }
            }

            return fallback;
        }
    }
}
