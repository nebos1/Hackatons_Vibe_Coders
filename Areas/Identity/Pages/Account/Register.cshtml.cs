using System.ComponentModel.DataAnnotations;
using EventsApp.Common;
using EventsApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EventsApp.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUserStore<ApplicationUser> _userStore;
        private readonly IUserEmailStore<ApplicationUser> _emailStore;
        private readonly ILogger<RegisterModel> _logger;

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            IUserStore<ApplicationUser> userStore,
            SignInManager<ApplicationUser> signInManager,
            ILogger<RegisterModel> logger)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
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
            [Display(Name = "First name")]
            public string FirstName { get; set; } = null!;

            [Required]
            [StringLength(GlobalConstants.User.LastNameMaxLength,
                MinimumLength = GlobalConstants.User.LastNameMinLength)]
            [Display(Name = "Last name")]
            public string LastName { get; set; } = null!;

            [Required]
            [StringLength(GlobalConstants.User.UserNameMaxLength,
                MinimumLength = GlobalConstants.User.UserNameMinLength)]
            [RegularExpression("^[a-zA-Z0-9._-]+$",
                ErrorMessage = "Username may contain only letters, digits, '.', '_' and '-'.")]
            [Display(Name = "Username")]
            public string UserName { get; set; } = null!;

            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; } = null!;

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 5)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; } = null!;

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "Passwords do not match.")]
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

            var existingByName = await _userManager.FindByNameAsync(Input.UserName);
            if (existingByName != null)
            {
                ModelState.AddModelError("Input.UserName", "This username is already taken.");
                return Page();
            }

            var existingByEmail = await _userManager.FindByEmailAsync(Input.Email);
            if (existingByEmail != null)
            {
                ModelState.AddModelError("Input.Email", "An account with this email already exists.");
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

                await _signInManager.SignInAsync(user, isPersistent: false);
                return LocalRedirect(returnUrl);
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
    }
}
