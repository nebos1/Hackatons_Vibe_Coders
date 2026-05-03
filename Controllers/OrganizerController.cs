using EventsApp.Common;
using EventsApp.Data;
using EventsApp.Models;
using EventsApp.Services;
using EventsApp.ViewModels.Events;
using EventsApp.ViewModels.Organizer;
using EventsApp.ViewModels.Posts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventsApp.Controllers
{
    [Authorize(Roles = GlobalConstants.Roles.Admin + "," + GlobalConstants.Roles.Organizer)]
    public class OrganizerController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IMediaUploadService _mediaUpload;

        public OrganizerController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IMediaUploadService mediaUpload)
        {
            _db = db;
            _userManager = userManager;
            _mediaUpload = mediaUpload;
        }

        public async Task<IActionResult> Dashboard()
        {
            var userId = _userManager.GetUserId(User)!;

            var orgData = await _db.OrganizerData
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrganizerId == userId);

            if (orgData == null)
            {
                return RedirectToAction(nameof(Profile));
            }

            var recentEvents = await _db.Events
                .AsNoTracking()
                .Where(e => e.OrganizerId == userId)
                .OrderByDescending(e => e.CreatedAt)
                .Take(5)
                .Select(e => new EventCardViewModel
                {
                    Id = e.Id,
                    Title = e.Title,
                    ImageUrl = e.ImageUrl,
                    Address = e.Address,
                    City = e.City,
                    StartTime = e.StartTime,
                    Genre = e.Genre,
                    IsApproved = e.IsApproved,
                    OrganizerId = e.OrganizerId,
                    OrganizerName = e.Organizer.UserName ?? string.Empty,
                    LikesCount = e.Likes.Count,
                    CommentsCount = e.Comments.Count,
                    SavesCount = e.Saves.Count,
                    GoingCount = e.Attendances.Count(a => a.Status == EventAttendanceStatus.Going),
                    InterestedCount = e.Attendances.Count(a => a.Status == EventAttendanceStatus.Interested),
                    CurrentUserLiked = e.Likes.Any(l => l.UserId == userId),
                    CurrentUserSaved = e.Saves.Any(s => s.UserId == userId),
                    CurrentUserAttendanceStatus = e.Attendances
                        .Where(a => a.UserId == userId)
                        .Select(a => (EventAttendanceStatus?)a.Status)
                        .FirstOrDefault(),
                })
                .ToListAsync();

            var recentPosts = await _db.Posts
                .AsNoTracking()
                .Where(p => p.OrganizerId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .Take(5)
                .Select(p => new PostCardViewModel
                {
                    Id = p.Id,
                    OrganizerId = p.OrganizerId,
                    OrganizerName = p.Organizer.UserName ?? string.Empty,
                    Content = p.Content,
                    CreatedAt = p.CreatedAt,
                    EventId = p.EventId,
                    EventTitle = p.Event != null ? p.Event.Title : null,
                    FirstMediaUrl = p.Images.Select(i => i.ImageUrl).FirstOrDefault(),
                    FirstMediaType = p.Images.Select(i => i.MediaType).FirstOrDefault(),
                    LikesCount = p.Likes.Count,
                    CommentsCount = p.Comments.Count,
                    SavesCount = p.Saves.Count,
                    CurrentUserLiked = p.Likes.Any(l => l.UserId == userId),
                    CurrentUserSaved = p.Saves.Any(s => s.UserId == userId),
                    AuthorImageUrl = p.Organizer.ProfileImageUrl,
                    AuthorIsOrganizer = p.Organizer.OrganizerData != null && p.Organizer.OrganizerData.Approved,
                })
                .ToListAsync();

            var ticketTypesCount = await _db.Tickets
                .CountAsync(t => t.Event.OrganizerId == userId);

            var paid = GlobalConstants.TransactionStatuses.Paid;
            var soldQuery = _db.UserTickets
                .AsNoTracking()
                .Where(ut => ut.Ticket.Event.OrganizerId == userId
                             && ut.Transaction.Status == paid);

            var ticketsSoldCount = await soldQuery.CountAsync();
            var ticketsUsedCount = await soldQuery.CountAsync(ut => ut.IsUsed);
            var totalRevenue = await soldQuery.SumAsync(ut => (decimal?)ut.Ticket.Price) ?? 0m;
            var avgTicketPrice = ticketsSoldCount > 0 ? totalRevenue / ticketsSoldCount : 0m;

            var eventsWithTickets = await _db.Events
                .CountAsync(e => e.OrganizerId == userId && e.Tickets.Any(t => t.IsActive));

            var now = DateTime.UtcNow;
            var upcomingCount = await _db.Events
                .CountAsync(e => e.OrganizerId == userId && e.StartTime >= now);
            var pastCount = await _db.Events
                .CountAsync(e => e.OrganizerId == userId && e.EndTime < now);

            var totalLikes = await _db.EventLikes
                .CountAsync(l => l.Event.OrganizerId == userId);
            var totalComments = await _db.EventComments
                .CountAsync(c => c.Event.OrganizerId == userId);

            var since30 = now.AddDays(-29).Date;

            var dailyRaw = await soldQuery
                .Where(ut => ut.CreatedAt >= since30)
                .GroupBy(ut => ut.CreatedAt.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Sold = g.Count(),
                    Revenue = g.Sum(x => (decimal?)x.Ticket.Price) ?? 0m,
                })
                .ToListAsync();

            var dailyMap = dailyRaw.ToDictionary(d => d.Date, d => (d.Sold, d.Revenue));
            var salesSeries = new List<DailySalesPoint>(30);
            for (int i = 0; i < 30; i++)
            {
                var day = since30.AddDays(i);
                if (dailyMap.TryGetValue(day, out var v))
                    salesSeries.Add(new DailySalesPoint { Date = day, Sold = v.Sold, Revenue = v.Revenue });
                else
                    salesSeries.Add(new DailySalesPoint { Date = day, Sold = 0, Revenue = 0m });
            }
            var last30Sold = salesSeries.Sum(p => p.Sold);
            var last30Revenue = salesSeries.Sum(p => p.Revenue);

            var perEventStats = await _db.UserTickets
                .AsNoTracking()
                .Where(ut => ut.Ticket.Event.OrganizerId == userId
                             && ut.Transaction.Status == paid)
                .GroupBy(ut => new { ut.Ticket.EventId, ut.Ticket.Event.Title })
                .Select(g => new TopEventStat
                {
                    EventId = g.Key.EventId,
                    Title = g.Key.Title,
                    Sold = g.Count(),
                    Revenue = g.Sum(x => x.Ticket.Price),
                })
                .ToListAsync();

            var topByTicketsSold = perEventStats
                .OrderByDescending(s => s.Sold)
                .Take(5)
                .ToList();

            var topByRevenue = perEventStats
                .OrderByDescending(s => s.Revenue)
                .Take(5)
                .ToList();

            var genreBreakdown = await _db.Events
                .AsNoTracking()
                .Where(e => e.OrganizerId == userId)
                .GroupBy(e => e.Genre)
                .Select(g => new GenreCountStat { Genre = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .ToListAsync();

            var cityBreakdown = await _db.Events
                .AsNoTracking()
                .Where(e => e.OrganizerId == userId)
                .GroupBy(e => e.City)
                .Select(g => new CityCountStat { City = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .Take(8)
                .ToListAsync();

            var eventTicketRows = await _db.Events
                .AsNoTracking()
                .Where(e => e.OrganizerId == userId)
                .OrderByDescending(e => e.CreatedAt)
                .Take(8)
                .Select(e => new OrganizerEventTicketRowViewModel
                {
                    EventId = e.Id,
                    EventTitle = e.Title,
                    StartTime = e.StartTime,
                    HasActiveTickets = e.Tickets.Any(t => t.IsActive),
                    Sold = e.Tickets.SelectMany(t => t.UserTickets)
                        .Count(ut => ut.Transaction.Status == paid),
                })
                .ToListAsync();

            var vm = new OrganizerDashboardViewModel
            {
                HasProfile = true,
                OrganizationName = orgData.OrganizationName,
                Description = orgData.Description,
                PhoneNumber = orgData.PhoneNumber,
                Website = orgData.Website,
                CompanyNumber = orgData.CompanyNumber,
                Approved = orgData.Approved,
                EventsCount = await _db.Events.CountAsync(e => e.OrganizerId == userId),
                PostsCount = await _db.Posts.CountAsync(p => p.OrganizerId == userId),
                TicketTypesCount = ticketTypesCount,
                TicketsSoldCount = ticketsSoldCount,
                EventsWithTicketsCount = eventsWithTickets,
                LayoutsCount = await _db.VenueLayouts.CountAsync(l => l.OrganizerId == userId && l.Status != VenueLayoutStatus.Archived),
                UpcomingEventsCount = upcomingCount,
                PastEventsCount = pastCount,
                TicketsUsedCount = ticketsUsedCount,
                TotalLikes = totalLikes,
                TotalComments = totalComments,
                TotalRevenue = totalRevenue,
                AverageTicketPrice = avgTicketPrice,
                Last30DaysSold = last30Sold,
                Last30DaysRevenue = last30Revenue,
                TopByTicketsSold = topByTicketsSold,
                TopByRevenue = topByRevenue,
                GenreBreakdown = genreBreakdown,
                CityBreakdown = cityBreakdown,
                SalesLast30Days = salesSeries,
                RecentEvents = recentEvents,
                RecentPosts = recentPosts,
                EventTicketRows = eventTicketRows,
            };

            return View(vm);
        }

        public async Task<IActionResult> Profiles()
        {
            var userId = _userManager.GetUserId(User)!;
            await EnsureDefaultProfileFromOrganizerDataAsync(userId);

            var profiles = await _db.OrganizerProfiles
                .AsNoTracking()
                .Where(p => p.OwnerId == userId)
                .OrderByDescending(p => p.IsDefault)
                .ThenBy(p => p.DisplayName)
                .Select(p => new OrganizerProfileRowViewModel
                {
                    Id = p.Id,
                    DisplayName = p.DisplayName,
                    Tagline = p.Tagline,
                    City = p.City,
                    AvatarImageUrl = p.AvatarImageUrl,
                    CoverImageUrl = p.CoverImageUrl,
                    IsDefault = p.IsDefault,
                    IsActive = p.IsActive,
                    EventsCount = p.Events.Count,
                    CreatedAt = p.CreatedAt,
                })
                .ToListAsync();

            return View(new OrganizerProfileListViewModel { Profiles = profiles });
        }

        [HttpGet]
        public async Task<IActionResult> Profile(int? id)
        {
            var userId = _userManager.GetUserId(User)!;
            await EnsureDefaultProfileFromOrganizerDataAsync(userId);

            if (id.HasValue)
            {
                var profile = await _db.OrganizerProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == userId);
                if (profile == null) return NotFound();
                return View(ToProfileInput(profile, await GetApprovedStatusAsync(userId)));
            }

            var hasProfiles = await _db.OrganizerProfiles.AnyAsync(p => p.OwnerId == userId);
            var orgData = await _db.OrganizerData.AsNoTracking().FirstOrDefaultAsync(o => o.OrganizerId == userId);

            return View(new OrganizerProfileViewModel
            {
                OrganizationName = string.Empty,
                CompanyNumber = orgData?.CompanyNumber,
                Approved = orgData?.Approved ?? false,
                IsDefault = !hasProfiles,
                IsActive = true,
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(OrganizerProfileViewModel input)
        {
            var userId = _userManager.GetUserId(User)!;
            var approved = await GetApprovedStatusAsync(userId);
            input.Approved = approved;

            if (!ModelState.IsValid) return View(input);

            var profile = input.Id.HasValue
                ? await _db.OrganizerProfiles.FirstOrDefaultAsync(p => p.Id == input.Id && p.OwnerId == userId)
                : null;

            if (input.Id.HasValue && profile == null) return NotFound();

            var isNew = profile == null;
            profile ??= new OrganizerProfile { OwnerId = userId };

            profile.DisplayName = input.OrganizationName.Trim();
            profile.Tagline = string.IsNullOrWhiteSpace(input.Tagline) ? null : input.Tagline.Trim();
            profile.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
            profile.City = string.IsNullOrWhiteSpace(input.City) ? null : input.City.Trim();
            profile.PhoneNumber = string.IsNullOrWhiteSpace(input.PhoneNumber) ? null : input.PhoneNumber.Trim();
            profile.Website = string.IsNullOrWhiteSpace(input.Website) ? null : input.Website.Trim();
            profile.ContactEmail = string.IsNullOrWhiteSpace(input.ContactEmail) ? null : input.ContactEmail.Trim();
            profile.InstagramUrl = string.IsNullOrWhiteSpace(input.InstagramUrl) ? null : input.InstagramUrl.Trim();
            profile.FacebookUrl = string.IsNullOrWhiteSpace(input.FacebookUrl) ? null : input.FacebookUrl.Trim();
            profile.TikTokUrl = string.IsNullOrWhiteSpace(input.TikTokUrl) ? null : input.TikTokUrl.Trim();
            profile.BrandColor = string.IsNullOrWhiteSpace(input.BrandColor) ? null : input.BrandColor.Trim();
            profile.IsActive = input.IsActive;

            try
            {
                var avatar = await SaveOptionalImageAsync(input.AvatarFile, "organizers");
                if (avatar != null) profile.AvatarImageUrl = avatar.Url;

                var cover = await SaveOptionalImageAsync(input.CoverFile, "organizers");
                if (cover != null) profile.CoverImageUrl = cover.Url;
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                input.CurrentAvatarUrl = profile.AvatarImageUrl;
                input.CurrentCoverUrl = profile.CoverImageUrl;
                return View(input);
            }

            if (isNew)
            {
                _db.OrganizerProfiles.Add(profile);
            }

            var shouldBeDefault = input.IsDefault || !await _db.OrganizerProfiles.AnyAsync(p => p.OwnerId == userId && p.Id != profile.Id);
            if (shouldBeDefault)
            {
                await ClearDefaultProfileAsync(userId);
                profile.IsDefault = true;
            }

            await EnsureOrganizerDataAsync(userId, input, approved);
            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = isNew ? "Публичната организаторска страница е създадена." : "Публичната организаторска страница е обновена.";
            return RedirectToAction(nameof(Profiles));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetDefaultProfile(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var profile = await _db.OrganizerProfiles.FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == userId);
            if (profile == null) return NotFound();

            await ClearDefaultProfileAsync(userId);
            profile.IsDefault = true;
            profile.IsActive = true;
            await _db.SaveChangesAsync();
            TempData["StatusMessage"] = $"{profile.DisplayName} вече е основната ти организаторска страница.";
            return RedirectToAction(nameof(Profiles));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProfile(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var profile = await _db.OrganizerProfiles
                .Include(p => p.Events)
                .FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == userId);
            if (profile == null) return NotFound();

            if (profile.Events.Any())
            {
                profile.IsActive = false;
                TempData["StatusMessage"] = "Тази страница има събития, затова беше архивирана вместо изтрита.";
            }
            else
            {
                _db.OrganizerProfiles.Remove(profile);
                TempData["StatusMessage"] = "Публичната организаторска страница е изтрита.";
            }

            await _db.SaveChangesAsync();

            if (!await _db.OrganizerProfiles.AnyAsync(p => p.OwnerId == userId && p.IsDefault))
            {
                var fallback = await _db.OrganizerProfiles
                    .Where(p => p.OwnerId == userId && p.IsActive)
                    .OrderBy(p => p.DisplayName)
                    .FirstOrDefaultAsync();
                if (fallback != null)
                {
                    fallback.IsDefault = true;
                    await _db.SaveChangesAsync();
                }
            }

            return RedirectToAction(nameof(Profiles));
        }

        private static OrganizerProfileViewModel ToProfileInput(OrganizerProfile profile, bool approved)
        {
            return new OrganizerProfileViewModel
            {
                Id = profile.Id,
                OrganizationName = profile.DisplayName,
                Tagline = profile.Tagline,
                Description = profile.Description,
                City = profile.City,
                CurrentAvatarUrl = profile.AvatarImageUrl,
                CurrentCoverUrl = profile.CoverImageUrl,
                PhoneNumber = profile.PhoneNumber,
                Website = profile.Website,
                ContactEmail = profile.ContactEmail,
                InstagramUrl = profile.InstagramUrl,
                FacebookUrl = profile.FacebookUrl,
                TikTokUrl = profile.TikTokUrl,
                BrandColor = profile.BrandColor,
                IsDefault = profile.IsDefault,
                IsActive = profile.IsActive,
                Approved = approved,
            };
        }

        private async Task<MediaUploadResult?> SaveOptionalImageAsync(IFormFile? file, string folder)
        {
            if (file == null || file.Length == 0) return null;
            var media = await _mediaUpload.SaveAsync(file, folder);
            if (media?.MediaType != PostMediaType.Image)
            {
                throw new InvalidOperationException("Only image files are allowed here.");
            }
            return media;
        }

        private async Task EnsureDefaultProfileFromOrganizerDataAsync(string userId)
        {
            if (await _db.OrganizerProfiles.AnyAsync(p => p.OwnerId == userId)) return;

            var orgData = await _db.OrganizerData.AsNoTracking().FirstOrDefaultAsync(o => o.OrganizerId == userId);
            if (orgData == null) return;

            _db.OrganizerProfiles.Add(new OrganizerProfile
            {
                OwnerId = userId,
                DisplayName = orgData.OrganizationName,
                Description = orgData.Description,
                PhoneNumber = orgData.PhoneNumber,
                Website = orgData.Website,
                IsDefault = true,
                IsActive = true,
            });
            await _db.SaveChangesAsync();
        }

        private async Task EnsureOrganizerDataAsync(string userId, OrganizerProfileViewModel input, bool approved)
        {
            var orgData = await _db.OrganizerData.FirstOrDefaultAsync(o => o.OrganizerId == userId);
            if (orgData == null)
            {
                _db.OrganizerData.Add(new OrganizerData
                {
                    OrganizerId = userId,
                    OrganizationName = input.OrganizationName.Trim(),
                    Description = input.Description,
                    PhoneNumber = input.PhoneNumber,
                    Website = input.Website,
                    CompanyNumber = input.CompanyNumber,
                    Approved = approved,
                });
            }
            else if (input.IsDefault)
            {
                orgData.OrganizationName = input.OrganizationName.Trim();
                orgData.Description = input.Description;
                orgData.PhoneNumber = input.PhoneNumber;
                orgData.Website = input.Website;
                orgData.CompanyNumber = input.CompanyNumber;
            }
        }

        private async Task ClearDefaultProfileAsync(string userId)
        {
            var defaults = await _db.OrganizerProfiles.Where(p => p.OwnerId == userId && p.IsDefault).ToListAsync();
            foreach (var item in defaults)
            {
                item.IsDefault = false;
            }
        }

        private async Task<bool> GetApprovedStatusAsync(string userId)
        {
            return await _db.OrganizerData
                .AsNoTracking()
                .Where(o => o.OrganizerId == userId)
                .Select(o => o.Approved)
                .FirstOrDefaultAsync();
        }
    }
}
