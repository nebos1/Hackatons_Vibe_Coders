using System.ComponentModel.DataAnnotations;
using EventsApp.Common;
using EventsApp.Data;
using EventsApp.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EventsApp.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext db,
            ILogger<LoginModel> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _db = db;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string? ReturnUrl { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public class InputModel
        {
            [Required]
            [Display(Name = "Имейл или потребителско име")]
            public string EmailOrUserName { get; set; } = null!;

            [Required]
            [DataType(DataType.Password)]
            [Display(Name = "Парола")]
            public string Password { get; set; } = null!;

            [Display(Name = "Запомни ме")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string? returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            returnUrl ??= Url.Content("~/");

            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

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

            var loginId = Input.EmailOrUserName.Trim();
            var user = await _userManager.FindByEmailAsync(loginId)
                ?? await _userManager.FindByNameAsync(loginId);

            if (user == null || string.IsNullOrWhiteSpace(user.UserName))
            {
                ModelState.AddModelError(string.Empty, "Невалиден опит за вход.");
                return Page();
            }

            var result = await _signInManager.PasswordSignInAsync(
                user.UserName,
                Input.Password,
                Input.RememberMe,
                lockoutOnFailure: false);

            if (result.Succeeded)
            {
                _logger.LogInformation("User {UserName} logged in.", user.UserName);

                var isPlainUser = await _userManager.IsInRoleAsync(user, GlobalConstants.Roles.User)
                    && !await _userManager.IsInRoleAsync(user, GlobalConstants.Roles.Organizer)
                    && !await _userManager.IsInRoleAsync(user, GlobalConstants.Roles.Admin);
                if (isPlainUser)
                {
                    var hasPrefs = await _db.UserPreferences.AsNoTracking().AnyAsync(p => p.UserId == user.Id);
                    if (!hasPrefs)
                    {
                        return Redirect("/Preferences/Edit?welcome=1");
                    }
                }

                return LocalRedirect(returnUrl);
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning("User account locked out.");
                ModelState.AddModelError(string.Empty, "Този акаунт е заключен.");
                return Page();
            }

            ModelState.AddModelError(string.Empty, "Невалиден опит за вход.");
            return Page();
        }
    }
}
