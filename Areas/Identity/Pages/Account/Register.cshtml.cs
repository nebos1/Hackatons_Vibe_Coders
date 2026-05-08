using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using EventsApp.Common;
using EventsApp.Models;
using EventsApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.RateLimiting;

namespace EventsApp.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUserStore<ApplicationUser> _userStore;
        private readonly IUserEmailStore<ApplicationUser> _emailStore;
        private readonly IEmailSender _emailSender;
        private readonly IAppLinkService _appLinks;
        private readonly ILogger<RegisterModel> _logger;

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            IUserStore<ApplicationUser> userStore,
            SignInManager<ApplicationUser> signInManager,
            IEmailSender emailSender,
            IAppLinkService appLinks,
            ILogger<RegisterModel> logger)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _emailSender = emailSender;
            _appLinks = appLinks;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string? ReturnUrl { get; set; }

        public class InputModel
        {
            [Required]
            [StringLength(GlobalConstants.User.FirstNameMaxLength,
                MinimumLength = GlobalConstants.User.FirstNameMinLength)]
            [Display(Name = "Име")]
            public string FirstName { get; set; } = null!;

            [Required]
            [StringLength(GlobalConstants.User.LastNameMaxLength,
                MinimumLength = GlobalConstants.User.LastNameMinLength)]
            [Display(Name = "Фамилия")]
            public string LastName { get; set; } = null!;

            [Required]
            [StringLength(GlobalConstants.User.UserNameMaxLength,
                MinimumLength = GlobalConstants.User.UserNameMinLength)]
            [RegularExpression("^[a-zA-Z0-9._]+$",
                ErrorMessage = "Потребителското име може да съдържа само букви, цифри, точки и долни черти.")]
            [Display(Name = "Потребителско име")]
            public string UserName { get; set; } = null!;

            [Required]
            [EmailAddress]
            [Display(Name = "Имейл")]
            public string Email { get; set; } = null!;

            [Required]
            [StringLength(100, ErrorMessage = "{0} трябва да е поне {2} символа.", MinimumLength = 5)]
            [DataType(DataType.Password)]
            [Display(Name = "Парола")]
            public string Password { get; set; } = null!;

            [DataType(DataType.Password)]
            [Display(Name = "Потвърди паролата")]
            [Compare("Password", ErrorMessage = "Паролите не съвпадат.")]
            public string ConfirmPassword { get; set; } = null!;
        }

        public void OnGet(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ReturnUrl = returnUrl;

            if (!ModelState.IsValid)
            {
                return Page();
            }

            Input.UserName = Input.UserName.Trim();
            Input.Email = Input.Email.Trim();
            Input.FirstName = Input.FirstName.Trim();
            Input.LastName = Input.LastName.Trim();

            var existingByName = await _userManager.FindByNameAsync(Input.UserName);
            if (existingByName != null)
            {
                ModelState.AddModelError("Input.UserName", "Това потребителско име вече е заето.");
                return Page();
            }

            var existingByEmail = await _userManager.FindByEmailAsync(Input.Email);
            if (existingByEmail != null)
            {
                ModelState.AddModelError("Input.Email", "Вече съществува акаунт с този имейл.");
                return Page();
            }

            var user = new ApplicationUser
            {
                FirstName = Input.FirstName.Trim(),
                LastName = Input.LastName.Trim(),
            };

            await _userStore.SetUserNameAsync(user, Input.UserName, CancellationToken.None);
            await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);

            var result = await _userManager.CreateAsync(user, Input.Password);

            if (result.Succeeded)
            {
                _logger.LogInformation("User {UserName} registered.", Input.UserName);

                await _userManager.AddToRoleAsync(user, GlobalConstants.Roles.User);
                await SendConfirmationEmailAsync(user);

                await _signInManager.SignInAsync(user, isPersistent: false);
                return Redirect("/Preferences/Edit?welcome=1");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }

        private IUserEmailStore<ApplicationUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException("The default UI requires a user store with email support.");
            }
            return (IUserEmailStore<ApplicationUser>)_userStore;
        }

        private async Task SendConfirmationEmailAsync(ApplicationUser user)
        {
            if (string.IsNullOrWhiteSpace(user.Email))
            {
                return;
            }

            try
            {
                var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

                var confirmPath = QueryHelpers.AddQueryString(
                    "/confirm-email",
                    new Dictionary<string, string?>
                    {
                        ["userId"] = user.Id,
                        ["code"] = code,
                    });

                var confirmUrl = _appLinks.ToAbsoluteUrl(Request, confirmPath);
                var encodedUrl = HtmlEncoder.Default.Encode(confirmUrl);

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
                                                <p style="margin:0 0 18px">Добре дошъл в Evento. Натисни бутона, за да потвърдиш, че този имейл е твой.</p>
                                                <table role="presentation" cellspacing="0" cellpadding="0" border="0" style="border-collapse:collapse;margin:0 0 20px">
                                                    <tr>
                                                        <td bgcolor="#5b4bff" style="border-radius:12px">
                                                            <a href="{encodedUrl}" style="display:inline-block;padding:13px 20px;font-family:Arial,sans-serif;font-size:15px;font-weight:800;color:#ffffff;text-decoration:none;border-radius:12px">Потвърди имейла</a>
                                                        </td>
                                                    </tr>
                                                </table>
                                                <p style="margin:0 0 8px;color:#475569">Ако бутонът не се отваря, копирай този линк в браузъра:</p>
                                                <p style="margin:0 0 18px;word-break:break-all"><a href="{encodedUrl}" style="color:#4f46e5;text-decoration:underline">{encodedUrl}</a></p>
                                                <p style="margin:0;color:#475569">Ако не си създавал акаунт в Evento, можеш спокойно да игнорираш този имейл.</p>
                                            </td>
                                        </tr>
                                    </table>
                                </td>
                            </tr>
                        </table>
                    </div>
                    """);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send confirmation email to {Email}.", user.Email);
            }
        }
    }
}
