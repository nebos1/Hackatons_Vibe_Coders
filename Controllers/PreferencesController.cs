using EventsApp.Data;
using EventsApp.Models;
using EventsApp.ViewModels.Preferences;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventsApp.Controllers
{
    [Authorize]
    public class PreferencesController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public PreferencesController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User)!;
            var prefs = await _db.UserPreferences.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId);

            if (prefs == null) return View("Index", (PreferencesViewModel?)null);

            return View(new PreferencesViewModel
            {
                PreferredGenre = prefs.PreferredGenre,
                PreferredCity = prefs.PreferredCity,
                MinAge = prefs.MinAge,
                MaxDistanceKm = prefs.MaxDistanceKm,
            });
        }

        public async Task<IActionResult> Edit()
        {
            var userId = _userManager.GetUserId(User)!;
            var prefs = await _db.UserPreferences.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId);

            var vm = prefs == null
                ? new PreferencesViewModel()
                : new PreferencesViewModel
                {
                    PreferredGenre = prefs.PreferredGenre,
                    PreferredCity = prefs.PreferredCity,
                    MinAge = prefs.MinAge,
                    MaxDistanceKm = prefs.MaxDistanceKm,
                };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(PreferencesViewModel input)
        {
            if (!ModelState.IsValid) return View(input);

            var userId = _userManager.GetUserId(User)!;
            var prefs = await _db.UserPreferences.FirstOrDefaultAsync(p => p.UserId == userId);

            if (prefs == null)
            {
                _db.UserPreferences.Add(new UserPreferences
                {
                    UserId = userId,
                    PreferredGenre = input.PreferredGenre,
                    PreferredCity = input.PreferredCity,
                    MinAge = input.MinAge,
                    MaxDistanceKm = input.MaxDistanceKm,
                });
            }
            else
            {
                prefs.PreferredGenre = input.PreferredGenre;
                prefs.PreferredCity = input.PreferredCity;
                prefs.MinAge = input.MinAge;
                prefs.MaxDistanceKm = input.MaxDistanceKm;
            }

            await _db.SaveChangesAsync();
            TempData["StatusMessage"] = "Preferences saved.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete()
        {
            var userId = _userManager.GetUserId(User)!;
            var prefs = await _db.UserPreferences.FirstOrDefaultAsync(p => p.UserId == userId);
            if (prefs != null)
            {
                _db.UserPreferences.Remove(prefs);
                await _db.SaveChangesAsync();
            }
            TempData["StatusMessage"] = "Preferences cleared.";
            return RedirectToAction(nameof(Index));
        }
    }
}
