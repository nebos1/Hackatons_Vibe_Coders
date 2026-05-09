using System.ComponentModel.DataAnnotations;
using EventsApp.Common;
using EventsApp.Data;
using EventsApp.Models;
using EventsApp.Services.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace EventsApp.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public class RegisterModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUserStore<ApplicationUser> _userStore;
        private readonly IUserEmailStore<ApplicationUser> _emailStore;
        private readonly IEmailConfirmationSender _emailConfirmationSender;
        private readonly ILogger<RegisterModel> _logger;

        public RegisterModel(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IUserStore<ApplicationUser> userStore,
            IEmailConfirmationSender emailConfirmationSender,
            ILogger<RegisterModel> logger)
        {
            _db = db;
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _emailConfirmationSender = emailConfirmationSender;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string? ReturnUrl { get; set; }

        public class InputModel : IValidatableObject
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

            [Display(Name = "Аз съм организатор, продуцент или място за събития")]
            public bool RegisterAsOrganizer { get; set; }

            [StringLength(GlobalConstants.Organizer.OrganizationNameMaxLength, MinimumLength = GlobalConstants.Organizer.OrganizationNameMinLength)]
            [Display(Name = "Име на организация / място")]
            public string? OrganizationName { get; set; }

            [StringLength(GlobalConstants.Organizer.PhoneNumberMaxLength)]
            [Display(Name = "Телефон")]
            public string? PhoneNumber { get; set; }

            [StringLength(80)]
            [Display(Name = "Страна")]
            public string? Country { get; set; } = "Bulgaria";

            [StringLength(GlobalConstants.Organizer.CityMaxLength)]
            [Display(Name = "Град")]
            public string? City { get; set; }

            [StringLength(120)]
            [Display(Name = "Как научи за Evento?")]
            public string? ReferralSource { get; set; }

            [StringLength(GlobalConstants.Organizer.WebsiteMaxLength)]
            [Display(Name = "Уебсайт")]
            public string? Website { get; set; }

            [StringLength(GlobalConstants.Organizer.CompanyNumberMaxLength)]
            [Display(Name = "Фирмен номер / ЕИК")]
            public string? CompanyNumber { get; set; }

            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
                if (!RegisterAsOrganizer)
                {
                    yield break;
                }

                if (string.IsNullOrWhiteSpace(OrganizationName))
                {
                    yield return new ValidationResult("Въведи име на организация, място или бранд.", new[] { nameof(OrganizationName) });
                }

                if (string.IsNullOrWhiteSpace(PhoneNumber))
                {
                    yield return new ValidationResult("Въведи телефон за връзка.", new[] { nameof(PhoneNumber) });
                }

                if (string.IsNullOrWhiteSpace(Country))
                {
                    yield return new ValidationResult("Избери държава.", new[] { nameof(Country) });
                }

                if (string.IsNullOrWhiteSpace(City))
                {
                    yield return new ValidationResult("Въведи град.", new[] { nameof(City) });
                }
            }
        }

        public void OnGet(string? returnUrl = null, bool organizer = false)
        {
            ReturnUrl = returnUrl;
            Input.RegisterAsOrganizer = organizer;
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ReturnUrl = returnUrl;
            NormalizeInput();

            if (!ModelState.IsValid)
            {
                return Page();
            }

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
                FirstName = Input.FirstName,
                LastName = Input.LastName,
                PhoneNumber = Input.RegisterAsOrganizer ? Input.PhoneNumber : null,
            };

            await _userStore.SetUserNameAsync(user, Input.UserName, CancellationToken.None);
            await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);

            var result = await _userManager.CreateAsync(user, Input.Password);

            if (result.Succeeded)
            {
                _logger.LogInformation("User {UserName} registered.", Input.UserName);

                await _userManager.AddToRoleAsync(user, GlobalConstants.Roles.User);

                if (Input.RegisterAsOrganizer)
                {
                    _db.OrganizerData.Add(new OrganizerData
                    {
                        OrganizerId = user.Id,
                        OrganizationName = Input.OrganizationName!,
                        Description = "Заявка, създадена при регистрация.",
                        PhoneNumber = Input.PhoneNumber,
                        City = Input.City,
                        Country = Input.Country,
                        ReferralSource = Input.ReferralSource,
                        Website = Input.Website,
                        CompanyNumber = Input.CompanyNumber,
                        Approved = false,
                    });

                    await _db.SaveChangesAsync();
                }

                var nextUrl = Input.RegisterAsOrganizer
                    ? Url.Action("EditApplication", "Account", new { welcome = "organizer" }) ?? "/Account/EditApplication?welcome=organizer"
                    : Url.Content("~/Preferences/Edit?welcome=1");

                await _emailConfirmationSender.SendAsync(user, Request, nextUrl, Input.RegisterAsOrganizer);

                TempData["StatusMessage"] = Input.RegisterAsOrganizer
                    ? "Акаунтът е създаден. Изпратихме ти имейл за потвърждение. След потвърждение ще можеш да влезеш и да довършиш заявката за организатор."
                    : "Акаунтът е създаден. Изпратихме ти имейл за потвърждение. Влез след като го потвърдиш.";

                return RedirectToPage("./Login", new { returnUrl = nextUrl });
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

        private void NormalizeInput()
        {
            Input.UserName = Input.UserName?.Trim() ?? string.Empty;
            Input.Email = Input.Email?.Trim() ?? string.Empty;
            Input.FirstName = Input.FirstName?.Trim() ?? string.Empty;
            Input.LastName = Input.LastName?.Trim() ?? string.Empty;
            Input.OrganizationName = string.IsNullOrWhiteSpace(Input.OrganizationName) ? null : Input.OrganizationName.Trim();
            Input.PhoneNumber = string.IsNullOrWhiteSpace(Input.PhoneNumber) ? null : Input.PhoneNumber.Trim();
            Input.Country = string.IsNullOrWhiteSpace(Input.Country) ? null : Input.Country.Trim();
            Input.City = string.IsNullOrWhiteSpace(Input.City) ? null : Input.City.Trim();
            Input.ReferralSource = string.IsNullOrWhiteSpace(Input.ReferralSource) ? null : Input.ReferralSource.Trim();
            Input.CompanyNumber = string.IsNullOrWhiteSpace(Input.CompanyNumber) ? null : Input.CompanyNumber.Trim();
            Input.Website = NormalizeUrl(Input.Website);
        }

        private static string? NormalizeUrl(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            return trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                    ? trimmed
                    : "https://" + trimmed;
        }

    }
}
