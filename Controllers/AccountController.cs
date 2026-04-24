using EventsApp.Common;
using EventsApp.Data;
using EventsApp.Models;
using EventsApp.ViewModels.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventsApp.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public AccountController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault() ?? "User";

            var orgData = await _db.OrganizerData
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrganizerId == user.Id);

            var vm = new AccountOverviewViewModel
            {
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                CreatedAt = user.CreatedAt,
                ProfileImageUrl = user.ProfileImageUrl,
                Bio = user.Bio,
                Role = role,
                HasApplied = orgData != null,
                IsApproved = orgData?.Approved ?? false,
                OrganizationName = orgData?.OrganizationName,
                ApplicationDate = orgData?.CreatedAt,
            };

            if (role == GlobalConstants.Roles.Organizer)
            {
                vm.VenuesCount = await _db.Venues.CountAsync(v => v.OrganizerId == user.Id);
                vm.EventsCount = await _db.Events.CountAsync(e => e.OrganizerId == user.Id);
                vm.PostsCount = await _db.Posts.CountAsync(p => p.OrganizerId == user.Id);
            }

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Apply()
        {
            var userId = _userManager.GetUserId(User)!;

            // Вече е кандидатствал или вече е организатор
            if (await _db.OrganizerData.AnyAsync(o => o.OrganizerId == userId))
            {
                TempData["StatusMessage"] = "You have already submitted an application.";
                return RedirectToAction(nameof(Index));
            }

            return View(new ApplyOrganizerViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Apply(ApplyOrganizerViewModel input)
        {
            if (!ModelState.IsValid) return View(input);

            var userId = _userManager.GetUserId(User)!;

            if (await _db.OrganizerData.AnyAsync(o => o.OrganizerId == userId))
            {
                TempData["StatusMessage"] = "You have already submitted an application.";
                return RedirectToAction(nameof(Index));
            }

            _db.OrganizerData.Add(new OrganizerData
            {
                OrganizerId = userId,
                OrganizationName = input.OrganizationName,
                Description = input.Description,
                PhoneNumber = input.PhoneNumber,
                Website = input.Website,
                CompanyNumber = input.CompanyNumber,
                Approved = false,
            });

            await _db.SaveChangesAsync();
            TempData["StatusMessage"] = "Application submitted! An admin will review it shortly.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> EditApplication()
        {
            var userId = _userManager.GetUserId(User)!;
            var orgData = await _db.OrganizerData.AsNoTracking().FirstOrDefaultAsync(o => o.OrganizerId == userId);
            if (orgData == null) return RedirectToAction(nameof(Apply));

            // Одобреният организатор редактира профила от OrganizerController
            if (orgData.Approved && User.IsInRole(GlobalConstants.Roles.Organizer))
                return RedirectToAction("Profile", "Organizer");

            return View(new ApplyOrganizerViewModel
            {
                OrganizationName = orgData.OrganizationName,
                Description = orgData.Description,
                PhoneNumber = orgData.PhoneNumber,
                Website = orgData.Website,
                CompanyNumber = orgData.CompanyNumber,
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditApplication(ApplyOrganizerViewModel input)
        {
            if (!ModelState.IsValid) return View(input);

            var userId = _userManager.GetUserId(User)!;
            var orgData = await _db.OrganizerData.FirstOrDefaultAsync(o => o.OrganizerId == userId);
            if (orgData == null) return RedirectToAction(nameof(Apply));

            orgData.OrganizationName = input.OrganizationName;
            orgData.Description = input.Description;
            orgData.PhoneNumber = input.PhoneNumber;
            orgData.Website = input.Website;
            orgData.CompanyNumber = input.CompanyNumber;

            await _db.SaveChangesAsync();
            TempData["StatusMessage"] = "Application updated.";
            return RedirectToAction(nameof(Index));
        }
    }
}
