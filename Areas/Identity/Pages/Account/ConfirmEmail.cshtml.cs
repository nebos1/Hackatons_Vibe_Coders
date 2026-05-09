using System.Text;
using EventsApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace EventsApp.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ConfirmEmailModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<ConfirmEmailModel> _logger;

        public ConfirmEmailModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<ConfirmEmailModel> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
        }

        public string Title { get; private set; } = "Потвърждение";

        public string Message { get; private set; } = "Проверяваме линка.";

        public string? ContinueUrl { get; private set; }

        public string ContinueText { get; private set; } = "Продължи";

        public async Task OnGetAsync(string? userId = null, string? code = null, string? returnUrl = null)
        {
            Response.ContentType = "text/html; charset=utf-8";
            Response.Headers["Content-Disposition"] = "inline";
            Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            Response.Headers.Pragma = "no-cache";
            Response.Headers.Expires = "0";

            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(code))
            {
                Title = "Невалиден линк";
                Message = "Линкът за потвърждение не е валиден. Влез в акаунта си и поискай нов линк.";
                return;
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                Title = "Невалиден линк";
                Message = "Не намерихме акаунт за този линк.";
                return;
            }

            string decodedCode;
            try
            {
                decodedCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            }
            catch (FormatException)
            {
                Title = "Невалиден линк";
                Message = "Линкът за потвърждение не е валиден. Пусни нова заявка.";
                return;
            }

            var result = await _userManager.ConfirmEmailAsync(user, decodedCode);
            if (result.Succeeded)
            {
                Title = "Имейлът е потвърден";
                Message = "Готово. Имейлът ти вече е потвърден в Evento.";
                await _signInManager.SignInAsync(user, isPersistent: false);
                if (IsSafeLocalUrl(returnUrl))
                {
                    var safeReturnUrl = returnUrl!;
                    ContinueUrl = safeReturnUrl;
                    ContinueText = safeReturnUrl.Contains("EditApplication", StringComparison.OrdinalIgnoreCase)
                        ? "Продължи към заявката за организатор"
                        : "Продължи в Evento";
                }
                _logger.LogInformation("User {UserId} confirmed their email.", user.Id);
                return;
            }

            Title = "Не успяхме да потвърдим имейла";
            Message = "Линкът може да е изтекъл или вече да е използван. Влез в акаунта си и поискай нов линк.";
        }

        private static bool IsSafeLocalUrl(string? url)
        {
            return !string.IsNullOrWhiteSpace(url)
                && url.StartsWith("/", StringComparison.Ordinal)
                && !url.StartsWith("//", StringComparison.Ordinal);
        }
    }
}
