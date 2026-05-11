using EventsApp.Data;
using EventsApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventsApp.Controllers.Api
{
    [ApiController]
    [Route("api/preferences")]
    [IgnoreAntiforgeryToken]
    [Authorize(Policy = "ApiAuth")]
    public class PreferencesApiController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public PreferencesApiController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var userId = _userManager.GetUserId(User)!;
            var preferences = await _db.UserPreferences.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId);

            return Ok(new
            {
                preferredGenres = preferences?.PreferredGenres.Select(g => g.ToString()).ToArray() ?? Array.Empty<string>(),
                preferredCity = preferences?.PreferredCity,
                minAge = preferences?.MinAge,
                maxDistanceKm = preferences?.MaxDistanceKm,
            });
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] PreferencesRequest request)
        {
            var userId = _userManager.GetUserId(User)!;
            var preferences = await _db.UserPreferences.FirstOrDefaultAsync(p => p.UserId == userId);
            if (preferences == null)
            {
                preferences = new UserPreferences { UserId = userId };
                _db.UserPreferences.Add(preferences);
            }

            var genres = new List<EventGenre>();
            foreach (var raw in request.PreferredGenres ?? Array.Empty<string>())
            {
                if (Enum.TryParse<EventGenre>(raw, ignoreCase: true, out var genre) && !genres.Contains(genre))
                {
                    genres.Add(genre);
                }
            }

            preferences.PreferredGenres = genres;
            preferences.PreferredCity = string.IsNullOrWhiteSpace(request.PreferredCity) ? null : request.PreferredCity.Trim();
            preferences.MinAge = request.MinAge;
            preferences.MaxDistanceKm = request.MaxDistanceKm;

            await _db.SaveChangesAsync();
            return Ok(new { saved = true });
        }
    }
}

public record PreferencesRequest(string[]? PreferredGenres, string? PreferredCity, int? MinAge, int? MaxDistanceKm);
