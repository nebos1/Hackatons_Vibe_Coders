using EventsApp.Common;
using EventsApp.Data;
using EventsApp.Models;
using EventsApp.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EventsApp.Controllers.Api
{
    [ApiController]
    [Route("api/events")]
    [IgnoreAntiforgeryToken]
    public class EventsFullApiController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IMediaUploadService _media;
        private readonly IEventDeletionService _eventDeletion;
        private readonly IRecurringEventService _recurringEvents;

        public EventsFullApiController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IMediaUploadService media,
            IEventDeletionService eventDeletion,
            IRecurringEventService recurringEvents)
        {
            _db = db;
            _userManager = userManager;
            _media = media;
            _eventDeletion = eventDeletion;
            _recurringEvents = recurringEvents;
        }

        private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);
        private bool IsAdmin => User.IsInRole(GlobalConstants.Roles.Admin);

        // ── GET /api/events ──────────────────────────────────────────────────────
        [HttpGet]
        [EnableRateLimiting("public-read")]
        public async Task<IActionResult> List(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 12,
            [FromQuery] string? genre = null,
            [FromQuery] string? city = null,
            [FromQuery] string? keyword = null,
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo = null)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 50);
            var userId = CurrentUserId;

            var q = _db.Events
                .AsNoTracking()
                .Where(e => e.IsApproved && e.OrganizerProfileId != null)
                .Include(e => e.Organizer)
                .Include(e => e.OrganizerProfile)
                .Include(e => e.Likes)
                .Include(e => e.Saves)
                .Include(e => e.Attendances)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
                q = q.Where(e =>
                    e.Title.Contains(keyword) ||
                    (e.Description != null && e.Description.Contains(keyword)) ||
                    e.City.Contains(keyword) ||
                    e.Address.Contains(keyword) ||
                    (e.OrganizerProfile != null && e.OrganizerProfile.DisplayName.Contains(keyword)) ||
                    e.Tickets.Any(t => t.Name.Contains(keyword) || (t.Description != null && t.Description.Contains(keyword))));

            if (!string.IsNullOrWhiteSpace(city))
                q = q.Where(e => e.City.ToLower().Contains(city.ToLower()));

            if (!string.IsNullOrWhiteSpace(genre) && Enum.TryParse<EventGenre>(genre, true, out var genreEnum))
                q = q.Where(e => e.Genre == genreEnum);

            if (dateFrom.HasValue)
                q = q.Where(e => e.StartTime >= dateFrom.Value);

            if (dateTo.HasValue)
                q = q.Where(e => e.StartTime <= dateTo.Value);

            var total = await q.CountAsync();
            var events = await q
                .OrderByDescending(e => e.StartTime >= DateTime.UtcNow)
                .ThenBy(e => e.StartTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = events.Select(e => MapToCard(e, userId)).ToList();

            return Ok(new
            {
                items,
                totalCount = total,
                page,
                pageSize,
                hasMore = (page * pageSize) < total,
            });
        }

        // ── GET /api/events/{id} ─────────────────────────────────────────────────
        [HttpGet("{id:int}")]
        [EnableRateLimiting("public-read")]
        public async Task<IActionResult> Details(int id)
        {
            var userId = CurrentUserId;

            var ev = await _db.Events
                .AsNoTracking()
                .Where(e => e.Id == id)
                .Include(e => e.Organizer)
                .Include(e => e.OrganizerProfile)
                .Include(e => e.Images)
                .Include(e => e.Likes)
                .Include(e => e.Saves)
                .Include(e => e.Attendances)
                .Include(e => e.Tickets).ThenInclude(t => t.SectionPrices)
                .Include(e => e.EventSeries).ThenInclude(s => s!.Occurrences)
                .FirstOrDefaultAsync();

            if (ev == null) return NotFound(new { error = "Събитието не е намерено." });
            if (!ev.IsApproved && !IsAdmin && ev.OrganizerId != userId)
                return NotFound(new { error = "Събитието не е намерено." });

            var comments = await _db.EventComments
                .AsNoTracking()
                .Where(c => c.EventId == id && c.ParentCommentId == null)
                .Include(c => c.User)
                .Include(c => c.Likes)
                .Include(c => c.Replies).ThenInclude(r => r.User)
                .OrderByDescending(c => c.Likes.Count)
                .ThenByDescending(c => c.CreatedAt)
                .Take(20)
                .ToListAsync();

            var similar = await _db.Events
                .AsNoTracking()
                .Where(e => e.Id != id && e.IsApproved && e.Genre == ev.Genre && e.StartTime > DateTime.UtcNow)
                .Include(e => e.Organizer)
                .Include(e => e.Likes)
                .Include(e => e.Saves)
                .Include(e => e.Attendances)
                .OrderBy(e => e.StartTime)
                .Take(6)
                .ToListAsync();

            var canEdit = IsAdmin || ev.OrganizerId == userId;

            return Ok(new
            {
                id = ev.Id,
                title = ev.Title,
                description = ev.Description,
                startTime = ev.StartTime,
                endTime = ev.EndTime,
                genre = ev.Genre.ToString(),
                imageUrl = ev.ImageUrl,
                address = ev.Address,
                city = ev.City,
                latitude = ev.Latitude,
                longitude = ev.Longitude,
                organizerId = ev.OrganizerId,
                organizerProfileId = ev.OrganizerProfileId,
                businessWorkspaceId = ev.BusinessWorkspaceId,
                organizerName = OrganizerDisplayName(ev),
                imageUrls = ev.Images.Select(i => i.ImageUrl).ToArray(),
                likesCount = ev.Likes.Count,
                savesCount = ev.Saves.Count,
                goingCount = ev.Attendances.Count(a => a.Status == EventAttendanceStatus.Going),
                interestedCount = ev.Attendances.Count(a => a.Status == EventAttendanceStatus.Interested),
                commentsCount = await _db.EventComments.CountAsync(c => c.EventId == id),
                isLiked = userId != null && ev.Likes.Any(l => l.UserId == userId),
                isSaved = userId != null && ev.Saves.Any(s => s.UserId == userId),
                userAttendanceStatus = userId != null
                    ? ev.Attendances.FirstOrDefault(a => a.UserId == userId)?.Status.ToString()
                    : null,
                canEdit,
                canDelete = canEdit,
                canManageTickets = canEdit,
                isRecurring = ev.EventSeries != null,
                ticketingMode = ev.TicketingMode.ToString(),
                tickets = ev.Tickets.Select(t => new
                {
                    id = t.Id,
                    name = t.Name,
                    price = t.Price,
                    currency = "BGN",
                    description = t.Description,
                }).ToArray(),
                occurrences = ev.EventSeries?.Occurrences
                    .OrderBy(o => o.StartDateTime)
                    .Select(o => new
                    {
                        id = o.Id,
                        startDateTime = o.StartDateTime,
                        endDateTime = o.EndDateTime,
                        status = o.Status.ToString(),
                        isAvailable = o.Status == EventOccurrenceStatus.Scheduled && o.StartDateTime > DateTime.UtcNow,
                    })
                    .ToArray() ?? Array.Empty<object>(),
                comments = comments.Select(c => MapComment(c, userId)).ToArray(),
                similarEvents = similar.Select(e => MapToCard(e, userId)).ToArray(),
                isApproved = ev.IsApproved,
                hasPendingChanges = ev.ChangeRequests.Any(r => r.Status == EventChangeRequestStatus.Pending),
            });
        }

        // ── POST /api/events/{id}/like ───────────────────────────────────────────
        [HttpPost("{id:int}/like")]
        [Authorize(Policy = "ApiAuth")]
        [EnableRateLimiting("interactions")]
        public async Task<IActionResult> Like(int id)
        {
            var userId = CurrentUserId!;
            var ev = await _db.Events.FindAsync(id);
            if (ev == null) return NotFound();

            var exists = await _db.EventLikes.AnyAsync(l => l.EventId == id && l.UserId == userId);
            if (!exists)
            {
                _db.EventLikes.Add(new EventLike { EventId = id, UserId = userId, CreatedAt = DateTime.UtcNow });
                await _db.SaveChangesAsync();
            }

            var count = await _db.EventLikes.CountAsync(l => l.EventId == id);
            return Ok(new { likesCount = count, isLiked = true });
        }

        // ── POST /api/events/{id}/unlike ─────────────────────────────────────────
        [HttpPost("{id:int}/unlike")]
        [Authorize(Policy = "ApiAuth")]
        [EnableRateLimiting("interactions")]
        public async Task<IActionResult> Unlike(int id)
        {
            var userId = CurrentUserId!;
            var like = await _db.EventLikes.FirstOrDefaultAsync(l => l.EventId == id && l.UserId == userId);
            if (like != null)
            {
                _db.EventLikes.Remove(like);
                await _db.SaveChangesAsync();
            }
            var count = await _db.EventLikes.CountAsync(l => l.EventId == id);
            return Ok(new { likesCount = count, isLiked = false });
        }

        // ── POST /api/events/{id}/save ───────────────────────────────────────────
        [HttpPost("{id:int}/save")]
        [Authorize(Policy = "ApiAuth")]
        [EnableRateLimiting("interactions")]
        public async Task<IActionResult> Save(int id)
        {
            var userId = CurrentUserId!;
            if (!await _db.Events.AnyAsync(e => e.Id == id)) return NotFound();

            var exists = await _db.EventSaves.AnyAsync(s => s.EventId == id && s.UserId == userId);
            if (!exists)
            {
                _db.EventSaves.Add(new EventSave { EventId = id, UserId = userId, CreatedAt = DateTime.UtcNow });
                await _db.SaveChangesAsync();
            }
            var count = await _db.EventSaves.CountAsync(s => s.EventId == id);
            return Ok(new { savesCount = count, isSaved = true });
        }

        // ── POST /api/events/{id}/unsave ─────────────────────────────────────────
        [HttpPost("{id:int}/unsave")]
        [Authorize(Policy = "ApiAuth")]
        [EnableRateLimiting("interactions")]
        public async Task<IActionResult> Unsave(int id)
        {
            var userId = CurrentUserId!;
            var save = await _db.EventSaves.FirstOrDefaultAsync(s => s.EventId == id && s.UserId == userId);
            if (save != null) { _db.EventSaves.Remove(save); await _db.SaveChangesAsync(); }
            var count = await _db.EventSaves.CountAsync(s => s.EventId == id);
            return Ok(new { savesCount = count, isSaved = false });
        }

        // ── POST /api/events/{id}/attend ─────────────────────────────────────────
        [HttpPost("{id:int}/attend")]
        [Authorize(Policy = "ApiAuth")]
        [EnableRateLimiting("interactions")]
        public async Task<IActionResult> Attend(int id, [FromBody] AttendRequest request)
        {
            var userId = CurrentUserId!;
            if (!await _db.Events.AnyAsync(e => e.Id == id)) return NotFound();

            if (!Enum.TryParse<EventAttendanceStatus>(request.Status, true, out var status))
                return BadRequest(new { error = "Невалиден статус. Използвай Going или Interested." });

            var att = await _db.EventAttendances.FirstOrDefaultAsync(a => a.EventId == id && a.UserId == userId);
            if (att == null)
            {
                _db.EventAttendances.Add(new EventAttendance { EventId = id, UserId = userId, Status = status, CreatedAt = DateTime.UtcNow });
            }
            else
            {
                att.Status = status;
            }
            await _db.SaveChangesAsync();

            return Ok(new
            {
                userAttendanceStatus = status.ToString(),
                goingCount = await _db.EventAttendances.CountAsync(a => a.EventId == id && a.Status == EventAttendanceStatus.Going),
                interestedCount = await _db.EventAttendances.CountAsync(a => a.EventId == id && a.Status == EventAttendanceStatus.Interested),
            });
        }

        // ── DELETE /api/events/{id}/attend ───────────────────────────────────────
        [HttpDelete("{id:int}/attend")]
        [Authorize(Policy = "ApiAuth")]
        [EnableRateLimiting("interactions")]
        public async Task<IActionResult> Unattend(int id)
        {
            var userId = CurrentUserId!;
            var att = await _db.EventAttendances.FirstOrDefaultAsync(a => a.EventId == id && a.UserId == userId);
            if (att != null) { _db.EventAttendances.Remove(att); await _db.SaveChangesAsync(); }
            return Ok(new
            {
                userAttendanceStatus = (string?)null,
                goingCount = await _db.EventAttendances.CountAsync(a => a.EventId == id && a.Status == EventAttendanceStatus.Going),
                interestedCount = await _db.EventAttendances.CountAsync(a => a.EventId == id && a.Status == EventAttendanceStatus.Interested),
            });
        }

        // ── POST /api/events ─────────────────────────────────────────────────────
        [HttpPost]
        [Authorize(Policy = "ApiAuth")]
        [EnableRateLimiting("content-write")]
        public async Task<IActionResult> Create([FromBody] CreateEventRequest request)
        {
            var userId = CurrentUserId!;
            if (!User.IsInRole(GlobalConstants.Roles.Organizer) && !IsAdmin)
                return Forbid();

            if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Address) || string.IsNullOrWhiteSpace(request.City))
                return BadRequest(new { error = "Попълни всички задължителни полета." });
            if (!request.OrganizerProfileId.HasValue)
                return BadRequest(new { error = "Избери public page. Събитията се публикуват само през публична страница." });
            var profile = await _db.OrganizerProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.OrganizerProfileId.Value && p.OwnerId == userId && p.IsActive);
            if (profile == null) return BadRequest(new { error = "Невалидна public page." });

            if (!Enum.TryParse<EventGenre>(request.Genre, true, out var genre))
                return BadRequest(new { error = "Невалиден жанр." });

            if (request.StartTime >= request.EndTime)
                return BadRequest(new { error = "Началото трябва да е преди края." });

            var ev = new Event
            {
                OrganizerId = userId,
                OrganizerProfileId = request.OrganizerProfileId,
                BusinessWorkspaceId = request.BusinessWorkspaceId ?? profile.BusinessWorkspaceId,
                Title = request.Title.Trim(),
                Description = request.Description?.Trim(),
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                Genre = genre,
                Address = request.Address.Trim(),
                City = request.City.Trim(),
                ImageUrl = request.ImageUrl?.Trim(),
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                IsApproved = false,
            };

            _db.Events.Add(ev);
            await _db.SaveChangesAsync();
            await UpsertSeriesAsync(ev, request, userId);

            return Ok(new { id = ev.Id, title = ev.Title, isApproved = ev.IsApproved });
        }

        [HttpPut("{id:int}")]
        [Authorize(Policy = "ApiAuth")]
        [EnableRateLimiting("content-write")]
        public async Task<IActionResult> Update(int id, [FromBody] CreateEventRequest request)
        {
            var userId = CurrentUserId!;
            var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == id);
            if (ev == null) return NotFound();
            if (!IsAdmin && ev.OrganizerId != userId) return Forbid();

            if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Address) || string.IsNullOrWhiteSpace(request.City))
                return BadRequest(new { error = "Попълни всички задължителни полета." });
            if (!request.OrganizerProfileId.HasValue)
                return BadRequest(new { error = "Избери public page. Събитията се публикуват само през публична страница." });
            var profile = await _db.OrganizerProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.OrganizerProfileId.Value && (p.OwnerId == userId || IsAdmin) && p.IsActive);
            if (profile == null) return BadRequest(new { error = "Невалидна public page." });

            if (!Enum.TryParse<EventGenre>(request.Genre, true, out var genre))
                return BadRequest(new { error = "Невалиден жанр." });

            if (request.StartTime >= request.EndTime)
                return BadRequest(new { error = "Началото трябва да е преди края." });

            ev.Title = request.Title.Trim();
            ev.Description = request.Description?.Trim();
            ev.StartTime = request.StartTime;
            ev.EndTime = request.EndTime;
            ev.Genre = genre;
            ev.Address = request.Address.Trim();
            ev.City = request.City.Trim();
            ev.ImageUrl = request.ImageUrl?.Trim();
            ev.OrganizerProfileId = request.OrganizerProfileId;
            ev.BusinessWorkspaceId = request.BusinessWorkspaceId ?? profile.BusinessWorkspaceId;
            ev.Latitude = request.Latitude;
            ev.Longitude = request.Longitude;

            if (!IsAdmin)
            {
                ev.IsApproved = false;
            }

            await _db.SaveChangesAsync();
            await UpsertSeriesAsync(ev, request, userId);
            return Ok(new { id = ev.Id, title = ev.Title, isApproved = ev.IsApproved });
        }

        [HttpDelete("{id:int}")]
        [Authorize(Policy = "ApiAuth")]
        [EnableRateLimiting("content-write")]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        {
            var userId = CurrentUserId!;
            var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
            if (ev == null) return NotFound();
            if (!IsAdmin && ev.OrganizerId != userId) return Forbid();

            var result = await _eventDeletion.DeleteEventAsync(id, preservePaidTickets: true, cancellationToken);
            if (result.Deleted) return Ok(new { deleted = true });
            if (result.SkippedReason == "paid_tickets")
                return Conflict(new { error = "Събитието има платени билети и не може да бъде изтрито." });

            return NotFound();
        }

        // ── GET /api/events/{id}/comments ────────────────────────────────────────
        [HttpGet("{id:int}/comments")]
        [EnableRateLimiting("public-read")]
        public async Task<IActionResult> GetComments(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var userId = CurrentUserId;
            var comments = await _db.EventComments
                .AsNoTracking()
                .Where(c => c.EventId == id && c.ParentCommentId == null)
                .Include(c => c.User)
                .Include(c => c.Likes)
                .Include(c => c.Replies).ThenInclude(r => r.User)
                .Include(c => c.Replies).ThenInclude(r => r.Likes)
                .OrderByDescending(c => c.Likes.Count)
                .ThenByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(comments.Select(c => MapComment(c, userId)));
        }

        // ── POST /api/events/{id}/comments ───────────────────────────────────────
        [HttpPost("{id:int}/comments")]
        [Authorize(Policy = "ApiAuth")]
        [EnableRateLimiting("content-write")]
        public async Task<IActionResult> AddComment(int id, [FromBody] CommentRequest request)
        {
            var userId = CurrentUserId!;
            if (string.IsNullOrWhiteSpace(request.Content))
                return BadRequest(new { error = "Коментарът не може да е празен." });

            if (!await _db.Events.AnyAsync(e => e.Id == id)) return NotFound();

            var comment = new EventComment
            {
                EventId = id,
                UserId = userId,
                Content = request.Content.Trim(),
                ParentCommentId = request.ParentCommentId,
                CreatedAt = DateTime.UtcNow,
            };
            _db.EventComments.Add(comment);
            await _db.SaveChangesAsync();

            var user = await _userManager.FindByIdAsync(userId);
            return Ok(new
            {
                id = comment.Id,
                userId = comment.UserId,
                userName = user?.UserName ?? "",
                authorImageUrl = user?.ProfileImageUrl,
                content = comment.Content,
                createdAt = comment.CreatedAt,
                likesCount = 0,
                currentUserLiked = false,
                canDelete = true,
                replies = Array.Empty<object>(),
            });
        }

        // ── DELETE /api/events/{id}/comments/{commentId} ─────────────────────────
        [HttpDelete("{id:int}/comments/{commentId:int}")]
        [Authorize(Policy = "ApiAuth")]
        [EnableRateLimiting("content-write")]
        public async Task<IActionResult> DeleteComment(int id, int commentId)
        {
            var userId = CurrentUserId!;
            var comment = await _db.EventComments.FindAsync(commentId);
            if (comment == null || comment.EventId != id) return NotFound();
            if (comment.UserId != userId && !IsAdmin) return Forbid();

            _db.EventComments.Remove(comment);
            await _db.SaveChangesAsync();
            return Ok(new { deleted = true });
        }

        // ── POST /api/events/{id}/comments/{commentId}/like ──────────────────────
        [HttpPost("{id:int}/comments/{commentId:int}/like")]
        [Authorize(Policy = "ApiAuth")]
        [EnableRateLimiting("interactions")]
        public async Task<IActionResult> LikeComment(int id, int commentId)
        {
            var userId = CurrentUserId!;
            var comment = await _db.EventComments.Include(c => c.Likes).FirstOrDefaultAsync(c => c.Id == commentId && c.EventId == id);
            if (comment == null) return NotFound();

            if (!comment.Likes.Any(l => l.UserId == userId))
            {
                _db.EventCommentLikes.Add(new EventCommentLike { EventCommentId = commentId, UserId = userId, CreatedAt = DateTime.UtcNow });
                await _db.SaveChangesAsync();
            }
            return Ok(new { likesCount = comment.Likes.Count + 1, currentUserLiked = true });
        }

        // ── DELETE /api/events/{id}/comments/{commentId}/like ────────────────────
        [HttpDelete("{id:int}/comments/{commentId:int}/like")]
        [Authorize(Policy = "ApiAuth")]
        [EnableRateLimiting("interactions")]
        public async Task<IActionResult> UnlikeComment(int id, int commentId)
        {
            var userId = CurrentUserId!;
            var like = await _db.EventCommentLikes.FirstOrDefaultAsync(l => l.EventCommentId == commentId && l.UserId == userId);
            if (like != null) { _db.EventCommentLikes.Remove(like); await _db.SaveChangesAsync(); }
            var count = await _db.EventCommentLikes.CountAsync(l => l.EventCommentId == commentId);
            return Ok(new { likesCount = count, currentUserLiked = false });
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static object MapToCard(Event e, string? userId) => new
        {
            id = e.Id,
            title = e.Title,
            description = e.Description,
            startTime = e.StartTime,
            endTime = e.EndTime,
            genre = e.Genre.ToString(),
            imageUrl = e.ImageUrl,
            address = e.Address,
            city = e.City,
            latitude = e.Latitude,
            longitude = e.Longitude,
            organizerName = OrganizerDisplayName(e),
            organizerProfileId = e.OrganizerProfileId,
            likesCount = e.Likes.Count,
            savesCount = e.Saves.Count,
            goingCount = e.Attendances.Count(a => a.Status == EventAttendanceStatus.Going),
            interestedCount = e.Attendances.Count(a => a.Status == EventAttendanceStatus.Interested),
            isLiked = userId != null && e.Likes.Any(l => l.UserId == userId),
            isSaved = userId != null && e.Saves.Any(s => s.UserId == userId),
            userAttendanceStatus = userId != null
                ? e.Attendances.FirstOrDefault(a => a.UserId == userId)?.Status.ToString()
                : null,
        };

        private static object MapComment(EventComment c, string? userId) => new
        {
            id = c.Id,
            userId = c.UserId,
            userName = c.User?.UserName ?? "",
            authorImageUrl = c.User?.ProfileImageUrl,
            content = c.Content,
            createdAt = c.CreatedAt,
            likesCount = c.Likes.Count,
            currentUserLiked = userId != null && c.Likes.Any(l => l.UserId == userId),
            canDelete = userId == c.UserId,
            replies = (c.Replies ?? new List<EventComment>()).Select(r => new
            {
                id = r.Id,
                userId = r.UserId,
                userName = r.User?.UserName ?? "",
                authorImageUrl = r.User?.ProfileImageUrl,
                content = r.Content,
                createdAt = r.CreatedAt,
                likesCount = r.Likes?.Count ?? 0,
                currentUserLiked = userId != null && (r.Likes?.Any(l => l.UserId == userId) ?? false),
                canDelete = userId == r.UserId,
                replies = Array.Empty<object>(),
            }).ToArray(),
        };

        private static string OrganizerDisplayName(Event e)
        {
            return e.OrganizerProfile?.DisplayName ?? "";
        }

        private async Task UpsertSeriesAsync(Event ev, CreateEventRequest request, string userId)
        {
            if (!Enum.TryParse<EventRecurrenceType>(request.RecurrenceType, true, out var recurrenceType) ||
                recurrenceType == EventRecurrenceType.None)
            {
                return;
            }

            var series = await _db.EventSeries
                .Include(s => s.Occurrences)
                .FirstOrDefaultAsync(s => s.EventId == ev.Id);

            if (series == null)
            {
                series = new EventSeries
                {
                    EventId = ev.Id,
                    OrganizerId = userId,
                };
                _db.EventSeries.Add(series);
            }

            series.Title = ev.Title;
            series.Description = ev.Description;
            series.Category = ev.Genre;
            series.Location = ev.Address;
            series.City = ev.City;
            series.ImageUrl = ev.ImageUrl;
            series.RecurrenceType = recurrenceType;
            series.Interval = Math.Clamp(request.RecurrenceInterval ?? 1, 1, 365);
            series.DaysOfWeek = request.DaysOfWeek == null ? null : string.Join(",", request.DaysOfWeek.Distinct());
            series.OccurrenceDisplayMode = EventOccurrenceDisplayMode.ShowAllDates;
            series.StartDate = (request.RecurrenceStartDate ?? ev.StartTime).Date;
            series.EndDate = (request.RecurrenceEndDate ?? ev.EndTime).Date;
            series.StartTime = request.RecurrenceStartTime ?? ev.StartTime.TimeOfDay;
            series.EndTime = request.RecurrenceEndTime ?? ev.EndTime.TimeOfDay;
            series.TimeZone = string.IsNullOrWhiteSpace(request.TimeZone) ? "Europe/Sofia" : request.TimeZone;
            series.Status = EventSeriesStatus.Published;
            series.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            await _recurringEvents.RegenerateOccurrencesAsync(series, RecurringEditScope.EntireSeries);
        }

        // ── Request DTOs ─────────────────────────────────────────────────────────

        public class AttendRequest { public string Status { get; set; } = "Going"; }
        public class CommentRequest
        {
            public string Content { get; set; } = "";
            public int? ParentCommentId { get; set; }
        }
        public class CreateEventRequest
        {
            public string Title { get; set; } = "";
            public string? Description { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public string Genre { get; set; } = "Other";
            public string Address { get; set; } = "";
            public string City { get; set; } = "";
            public string? ImageUrl { get; set; }
            public int? OrganizerProfileId { get; set; }
            public int? BusinessWorkspaceId { get; set; }
            public double? Latitude { get; set; }
            public double? Longitude { get; set; }
            public string? RecurrenceType { get; set; }
            public int? RecurrenceInterval { get; set; }
            public string[]? DaysOfWeek { get; set; }
            public DateTime? RecurrenceStartDate { get; set; }
            public DateTime? RecurrenceEndDate { get; set; }
            public TimeSpan? RecurrenceStartTime { get; set; }
            public TimeSpan? RecurrenceEndTime { get; set; }
            public string? TimeZone { get; set; }
        }
    }
}
