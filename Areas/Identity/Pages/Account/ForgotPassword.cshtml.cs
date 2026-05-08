using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using EventsApp.Models;
using EventsApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace EventsApp.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly IAppLinkService _appLinks;
        private readonly ILogger<ForgotPasswordModel> _logger;

        public ForgotPasswordModel(
            UserManager<ApplicationUser> userManager,
            IEmailSender emailSender,
            IAppLinkService appLinks,
            ILogger<ForgotPasswordModel> logger)
        {
            _userManager = userManager;
            _emailSender = emailSender;
            _appLinks = appLinks;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required]
            [EmailAddress]
            [Display(Name = "Имейл")]
            public string Email { get; set; } = string.Empty;
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var email = Input.Email.Trim();
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return RedirectToPage("./ForgotPasswordConfirmation");
            }

            var code = await _userManager.GeneratePasswordResetTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var resetQuery = new Dictionary<string, string?>
            {
                ["code"] = code,
                ["email"] = email,
            };
            var resetPath = QueryHelpers.AddQueryString("/reset-password", resetQuery);
            var resetUrl = _appLinks.ToAbsoluteUrl(Request, resetPath);
            if (!Uri.TryCreate(resetUrl, UriKind.Absolute, out var resetUri) ||
                string.IsNullOrWhiteSpace(resetUri.Host) ||
                (!string.Equals(resetUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(resetUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
            {
                resetUrl = QueryHelpers.AddQueryString("https://evento.business/reset-password", resetQuery);
            }
            var encodedUrl = HtmlEncoder.Default.Encode(resetUrl);

            try
            {
                await _emailSender.SendEmailAsync(
                    email,
                    "Смяна на парола - Evento",
                    $"""
                    <div style="font-family:Arial,sans-serif;line-height:1.5;color:#111827">
                        <h2>Смяна на парола</h2>
                        <p>Получихме заявка за смяна на паролата в Evento.</p>
                        <p><a href="{encodedUrl}" target="_blank" rel="noopener" style="display:inline-block;background:#5b4bff;color:#ffffff;padding:12px 18px;border-radius:10px;text-decoration:none;font-weight:700">Смени паролата</a></p>
                        <p style="margin-top:18px">Ако бутонът не се отваря, копирай този линк в браузъра:</p>
                        <p style="word-break:break-all"><a href="{encodedUrl}" target="_blank" rel="noopener">{encodedUrl}</a></p>
                        <p>Ако не си заявил това, можеш спокойно да игнорираш този имейл.</p>
                    </div>
                    """);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send password reset email to {Email}.", email);
            }

            return RedirectToPage("./ForgotPasswordConfirmation");
        }
    }
}
