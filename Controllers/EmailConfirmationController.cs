using System.Text;
using System.Text.Encodings.Web;
using EventsApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace EventsApp.Controllers
{
    [AllowAnonymous]
    public class EmailConfirmationController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<EmailConfirmationController> _logger;

        public EmailConfirmationController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<EmailConfirmationController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Index(string? userId = null, string? code = null, string? returnUrl = null)
        {
            Response.ContentType = "text/html; charset=utf-8";
            Response.Headers.Remove("Content-Disposition");
            Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            Response.Headers.Pragma = "no-cache";
            Response.Headers.Expires = "0";

            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(code))
            {
                return ConfirmationPage(
                    "Невалиден линк",
                    "Линкът за потвърждение не е валиден. Влез в акаунта си и поискай нов линк.",
                    "/Identity/Account/Login",
                    "Към вход");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return ConfirmationPage(
                    "Невалиден линк",
                    "Не намерихме акаунт за този линк.",
                    "/Identity/Account/Login",
                    "Към вход");
            }

            string decodedCode;
            try
            {
                decodedCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            }
            catch (FormatException)
            {
                return ConfirmationPage(
                    "Невалиден линк",
                    "Линкът за потвърждение не е валиден. Поискай нов линк за имейл потвърждение.",
                    "/Identity/Account/Login",
                    "Към вход");
            }

            if (!await _userManager.IsEmailConfirmedAsync(user))
            {
                var result = await _userManager.ConfirmEmailAsync(user, decodedCode);
                if (!result.Succeeded)
                {
                    return ConfirmationPage(
                        "Не успяхме да потвърдим имейла",
                        "Линкът може да е изтекъл или вече да е използван. Влез и поискай нов линк.",
                        "/Identity/Account/Login",
                        "Към вход");
                }
            }

            await _signInManager.SignInAsync(user, isPersistent: false);
            _logger.LogInformation("User {UserId} confirmed their email.", user.Id);

            var nextUrl = IsSafeLocalUrl(returnUrl) ? returnUrl! : "/";
            var nextText = nextUrl.Contains("EditApplication", StringComparison.OrdinalIgnoreCase)
                ? "Продължи към заявката за организатор"
                : "Продължи в Evento";

            return ConfirmationPage(
                "Имейлът е потвърден",
                "Готово. Имейлът ти вече е потвърден и можеш да използваш акаунта си нормално.",
                nextUrl,
                nextText,
                success: true);
        }

        private ContentResult ConfirmationPage(
            string title,
            string message,
            string actionUrl,
            string actionText,
            bool success = false)
        {
            var encodedTitle = HtmlEncoder.Default.Encode(title);
            var encodedMessage = HtmlEncoder.Default.Encode(message);
            var encodedActionUrl = HtmlEncoder.Default.Encode(actionUrl);
            var encodedActionText = HtmlEncoder.Default.Encode(actionText);
            var mark = success ? "✓" : "!";
            var accent = success ? "#16a34a" : "#4f46e5";

            var html = $$"""
                <!doctype html>
                <html lang="bg">
                <head>
                    <meta charset="utf-8">
                    <meta name="viewport" content="width=device-width, initial-scale=1">
                    <meta http-equiv="cache-control" content="no-store">
                    <title>{{encodedTitle}} - Evento</title>
                    <style>
                        :root { color-scheme: light dark; }
                        body { margin:0; min-height:100vh; display:grid; place-items:center; background:#f4f6fb; color:#111827; font-family:Arial, sans-serif; }
                        .card { width:min(92vw, 520px); background:#fff; border:1px solid #e5e7eb; border-radius:24px; padding:34px; box-shadow:0 22px 60px rgba(15,23,42,.12); text-align:center; }
                        .brand { font-weight:900; letter-spacing:.08em; text-transform:uppercase; color:#4f46e5; font-size:13px; }
                        .mark { width:64px; height:64px; border-radius:999px; display:grid; place-items:center; margin:18px auto 14px; background:{{accent}}; color:#fff; font-size:34px; font-weight:900; }
                        h1 { margin:0 0 10px; font-size:30px; line-height:1.12; }
                        p { margin:0 auto 24px; color:#475569; line-height:1.55; max-width:420px; }
                        a { display:inline-flex; align-items:center; justify-content:center; min-height:46px; padding:0 22px; border-radius:999px; background:#111827; color:#fff; text-decoration:none; font-weight:800; }
                        @media (prefers-color-scheme: dark) {
                            body { background:#0f1220; color:#fff; }
                            .card { background:#171a2b; border-color:#2b3148; }
                            p { color:#cbd5e1; }
                            a { background:#fff; color:#111827; }
                        }
                    </style>
                </head>
                <body>
                    <main class="card">
                        <div class="brand">Evento</div>
                        <div class="mark">{{mark}}</div>
                        <h1>{{encodedTitle}}</h1>
                        <p>{{encodedMessage}}</p>
                        <a href="{{encodedActionUrl}}">{{encodedActionText}}</a>
                    </main>
                </body>
                </html>
                """;

            return Content(html, "text/html; charset=utf-8");
        }

        private static bool IsSafeLocalUrl(string? url)
        {
            return !string.IsNullOrWhiteSpace(url)
                && url.StartsWith("/", StringComparison.Ordinal)
                && !url.StartsWith("//", StringComparison.Ordinal);
        }

        [HttpGet]
        public IActionResult RedirectToCanonical(string? userId = null, string? code = null, string? returnUrl = null)
        {
            var query = new Dictionary<string, string?>
            {
                ["userId"] = userId,
                ["code"] = code,
                ["returnUrl"] = returnUrl,
            };

            return Redirect(QueryHelpers.AddQueryString("/email/confirm", query));
        }
    }
}
