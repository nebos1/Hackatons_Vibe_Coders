using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace EventsApp.Services.Email
{
    public class SmtpEmailSender : IEmailSender
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SmtpEmailSender> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public SmtpEmailSender(
            IConfiguration configuration,
            ILogger<SmtpEmailSender> logger,
            IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            if (!GetBool(false, "EMAIL_ENABLED", "Email:Enabled"))
            {
                _logger.LogInformation("Email sending is disabled. Skipping message to {Email} with subject {Subject}.", email, subject);
                return;
            }

            var brevoApiKey = GetSetting("BREVO_API_KEY", "BREVO_APIKEY", "BREVO__APIKEY", "Brevo__ApiKey", "Email:Brevo:ApiKey");
            if (!string.IsNullOrWhiteSpace(brevoApiKey))
            {
                await SendWithBrevoApiAsync(brevoApiKey, email, subject, htmlMessage);
                return;
            }

            var host = GetSetting("SMTP_HOST", "Email:Smtp:Host");
            var username = GetSetting("SMTP_USERNAME", "SMTP_USER", "Email:Smtp:Username");
            var fromEmail = GetSetting("SMTP_FROM_EMAIL", "Email:From:Email") ?? username;
            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(fromEmail))
            {
                _logger.LogWarning("Email is enabled but SMTP host or from email is missing.");
                return;
            }

            var port = GetInt(587, "SMTP_PORT", "Email:Smtp:Port");
            var enableSsl = GetBool(true, "SMTP_ENABLE_SSL", "SMTP_SSL", "Email:Smtp:EnableSsl");
            var password = GetSetting("SMTP_PASSWORD", "SMTP_PASS", "Email:Smtp:Password");
            var fromName = GetSetting("SMTP_FROM_NAME", "Email:From:Name") ?? "Evento";
            var timeoutSeconds = GetInt(15, "SMTP_TIMEOUT_SECONDS", "Email:Smtp:TimeoutSeconds");

            using var client = new SmtpClient(host, port)
            {
                EnableSsl = enableSsl,
                UseDefaultCredentials = false,
                Timeout = Math.Max(5, timeoutSeconds) * 1000,
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

        private async Task SendWithBrevoApiAsync(string apiKey, string email, string subject, string htmlMessage)
        {
            var fromEmail = GetSetting("SMTP_FROM_EMAIL", "Email:From:Email");
            var fromName = GetSetting("SMTP_FROM_NAME", "Email:From:Name") ?? "Evento";
            if (string.IsNullOrWhiteSpace(fromEmail))
            {
                _logger.LogWarning("Email is enabled but Brevo API sender email is missing.");
                return;
            }

            var request = new
            {
                sender = new { name = fromName, email = fromEmail },
                to = new[] { new { email } },
                subject,
                htmlContent = htmlMessage,
            };

            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(GetInt(20, "BREVO_TIMEOUT_SECONDS", "Email:Brevo:TimeoutSeconds"));
            using var message = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email")
            {
                Content = JsonContent.Create(request),
            };
            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            message.Headers.Add("api-key", apiKey);

            using var response = await client.SendAsync(message);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Sent email to {Email} with Brevo API and subject {Subject}.", email, subject);
                return;
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "Brevo API email failed with status {StatusCode}. Response: {Response}",
                (int)response.StatusCode,
                responseBody);
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
