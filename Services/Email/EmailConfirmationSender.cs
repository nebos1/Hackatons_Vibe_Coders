using System.Text;
using System.Text.Encodings.Web;
using EventsApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.WebUtilities;

namespace EventsApp.Services.Email
{
    public interface IEmailConfirmationSender
    {
        Task SendAsync(ApplicationUser user, HttpRequest request, string? returnUrl, bool organizerSignup);
    }

    public class EmailConfirmationSender : IEmailConfirmationSender
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly IAppLinkService _appLinks;
        private readonly ILogger<EmailConfirmationSender> _logger;

        public EmailConfirmationSender(
            UserManager<ApplicationUser> userManager,
            IEmailSender emailSender,
            IAppLinkService appLinks,
            ILogger<EmailConfirmationSender> logger)
        {
            _userManager = userManager;
            _emailSender = emailSender;
            _appLinks = appLinks;
            _logger = logger;
        }

        public async Task SendAsync(ApplicationUser user, HttpRequest request, string? returnUrl, bool organizerSignup)
        {
            if (string.IsNullOrWhiteSpace(user.Email))
            {
                return;
            }

            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

            var confirmQuery = new Dictionary<string, string?>
            {
                ["userId"] = user.Id,
                ["code"] = code,
                ["returnUrl"] = returnUrl,
            };
            var confirmPath = QueryHelpers.AddQueryString("/email/confirm", confirmQuery);
            var confirmUrl = _appLinks.ToAbsoluteUrl(request, confirmPath);
            if (!IsUsableWebUrl(confirmUrl))
            {
                confirmUrl = QueryHelpers.AddQueryString("https://evento.business/email/confirm", confirmQuery);
            }

            LogConfirmationLinkTarget(confirmUrl, user.Email);
            var encodedUrl = HtmlEncoder.Default.Encode(confirmUrl);
            var intro = organizerSignup
                ? "Добре дошъл в Evento. Потвърди имейла си, за да продължиш спокойно със заявката си за организатор."
                : "Добре дошъл в Evento. Натисни бутона, за да потвърдиш, че този имейл е твой.";

            await _emailSender.SendEmailAsync(
                user.Email,
                "Потвърди имейла си - Evento",
                $"""
                <div style="margin:0;padding:0;background:#f2f5ff">
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="background:#f2f5ff;border-collapse:collapse">
                        <tr>
                            <td align="center" style="padding:28px 14px">
                                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="max-width:560px;background:#ffffff;border-collapse:collapse;border-radius:18px;overflow:hidden">
                                    <tr>
                                        <td style="background:#4f46e5;color:#ffffff;padding:28px 30px;font-family:Arial,sans-serif">
                                            <div style="font-size:12px;font-weight:800;letter-spacing:1px;text-transform:uppercase">Evento</div>
                                            <h1 style="margin:14px 0 0;font-size:28px;line-height:1.15;color:#ffffff">Потвърди имейла си</h1>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td style="padding:28px 30px;font-family:Arial,sans-serif;color:#111827;font-size:15px;line-height:1.55">
                                            <p style="margin:0 0 18px">{HtmlEncoder.Default.Encode(intro)}</p>
                                            <table role="presentation" cellspacing="0" cellpadding="0" border="0" style="border-collapse:collapse;margin:0 0 20px">
                                                <tr>
                                                    <td bgcolor="#5b4bff" style="border-radius:12px">
                                                        <a href="{encodedUrl}" target="_blank" rel="noopener" style="display:inline-block;padding:13px 20px;font-family:Arial,sans-serif;font-size:15px;font-weight:800;color:#ffffff;text-decoration:none;border-radius:12px">Потвърди имейла</a>
                                                    </td>
                                                </tr>
                                            </table>
                                            <p style="margin:0 0 8px;color:#475569">Ако бутонът не се отваря, копирай този линк в браузъра:</p>
                                            <p style="margin:0 0 18px;word-break:break-all"><a href="{encodedUrl}" target="_blank" rel="noopener" style="color:#4f46e5;text-decoration:underline">{encodedUrl}</a></p>
                                            <p style="margin:0;color:#475569">Ако не си създавал акаунт в Evento, може спокойно да игнорираш този имейл.</p>
                                        </td>
                                    </tr>
                                </table>
                            </td>
                        </tr>
                    </table>
                </div>
                """);

            _logger.LogInformation("Confirmation email was handed to the email sender for {Email}.", user.Email);
        }

        private void LogConfirmationLinkTarget(string confirmUrl, string email)
        {
            if (Uri.TryCreate(confirmUrl, UriKind.Absolute, out var uri))
            {
                _logger.LogInformation(
                    "Sending confirmation email to {Email}. Confirmation host={Host}, scheme={Scheme}.",
                    email,
                    uri.Host,
                    uri.Scheme);
            }
        }

        private static bool IsUsableWebUrl(string? url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(uri.Host) ||
                string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        }
    }
}
