using EventsApp.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace EventsApp.Controllers.Api
{
    [ApiController]
    [Route("api/mentions")]
    [Authorize]
    [EnableRateLimiting("public-read")]
    public class MentionsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public MentionsApiController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search(string? q, CancellationToken ct)
        {
            q = (q ?? "").Trim();
            if (q.Length < 1)
            {
                return Ok(new { items = Array.Empty<object>() });
            }
            var prefix = q.ToUpperInvariant();

            var users = await _db.Users
                .AsNoTracking()
                .Where(u => u.UserName != null
                    && u.NormalizedUserName != null
                    && u.NormalizedUserName.StartsWith(prefix))
                .OrderBy(u => u.UserName)
                .Take(8)
                .Select(u => new
                {
                    userName = u.UserName!,
                    displayName = (u.FirstName ?? "") + " " + (u.LastName ?? ""),
                    imageUrl = u.ProfileImageUrl,
                })
                .ToListAsync(ct);

            return Ok(new { items = users });
        }
    }
}
