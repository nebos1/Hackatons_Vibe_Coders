using EventsApp.Data;
using EventsApp.Models;
using EventsApp.ViewModels.Events;
using EventsApp.ViewModels.Posts;
using EventsApp.ViewModels.Social;
using Microsoft.EntityFrameworkCore;

namespace EventsApp.Services
{
    public interface ISocialFeedService
    {
        Task<FeedViewModel> BuildFeedAsync(string? userId, string? searchTerm = null, CancellationToken cancellationToken = default);
        Task TrackActivityAsync(string userId, UserActivityType activityType, int? eventId = null, int? postId = null, string? targetUserId = null, string? value = null, CancellationToken cancellationToken = default);
    }

    public class SocialFeedService : ISocialFeedService
    {
        private readonly ApplicationDbContext _db;

        public SocialFeedService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<FeedViewModel> BuildFeedAsync(string? userId, string? searchTerm = null, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var normalizedSearch = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim();
            var followedIds = userId == null
                ? new List<string>()
                : await _db.Follows
                    .AsNoTracking()
                    .Where(f => f.FollowerId == userId)
                    .Select(f => f.FollowingId)
                    .ToListAsync(cancellationToken);

            var prefs = userId == null
                ? null
                : await _db.UserPreferences
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

            var activitySignals = userId == null
                ? new List<UserEventSignal>()
                : await _db.UserActivities
                    .AsNoTracking()
                    .Where(a => a.UserId == userId && a.EventId != null)
                    .OrderByDescending(a => a.CreatedAt)
                    .Take(120)
                    .Select(a => new UserEventSignal
                    {
                        Genre = a.Event!.Genre,
                        City = a.Event.City,
                        OrganizerId = a.Event.OrganizerId,
                        ActivityType = a.ActivityType,
                    })
                    .ToListAsync(cancellationToken);

            var genreScores = activitySignals
                .GroupBy(s => s.Genre)
                .ToDictionary(g => g.Key, g => g.Sum(s => ActivityWeight(s.ActivityType)));

            var cityScores = activitySignals
                .GroupBy(s => s.City, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Sum(s => ActivityWeight(s.ActivityType)), StringComparer.OrdinalIgnoreCase);

            var organizerScores = activitySignals
                .GroupBy(s => s.OrganizerId)
                .ToDictionary(g => g.Key, g => g.Sum(s => ActivityWeight(s.ActivityType)));

            var hasPersonalSignals = (prefs?.PreferredGenre != null)
                || !string.IsNullOrWhiteSpace(prefs?.PreferredCity)
                || followedIds.Count > 0
                || activitySignals.Count > 0;

            var currentUser = userId == null
                ? null
                : await _db.Users
                    .AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => new
                    {
                        DisplayName = u.OrganizerData != null && u.OrganizerData.Approved
                            ? u.OrganizerData.OrganizationName
                            : u.UserName ?? u.Email ?? string.Empty,
                        u.ProfileImageUrl,
                    })
                    .FirstOrDefaultAsync(cancellationToken);

            var candidateEvents = await QueryEventCards(_db.Events
                    .AsNoTracking()
                    .Where(e => e.IsApproved && e.StartTime >= now)
                    .OrderBy(e => e.StartTime)
                    .Take(80),
                userId,
                cancellationToken);

            var recommended = candidateEvents
                .Select(ev => new
                {
                    Event = ev,
                    Score = ScoreEvent(ev, prefs?.PreferredGenre, prefs?.PreferredCity, followedIds, genreScores, cityScores, organizerScores, now),
                })
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Event.StartTime)
                .Select(x => x.Event)
                .Take(8)
                .ToList();

            if (!hasPersonalSignals)
            {
                recommended = candidateEvents
                    .Where(e => string.IsNullOrWhiteSpace(prefs?.PreferredCity) || string.Equals(e.City, prefs.PreferredCity, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(e => e.StartTime)
                    .Take(8)
                    .ToList();
            }

            var trending = await QueryEventCards(_db.Events
                    .AsNoTracking()
                    .Where(e => e.IsApproved && e.StartTime >= now.AddDays(-2))
                    .OrderByDescending(e => e.Likes.Count + e.Comments.Count + e.Saves.Count + (e.Attendances.Count * 2))
                    .ThenBy(e => e.StartTime)
                    .Take(8),
                userId,
                cancellationToken);

            var friendPostsQuery = _db.Posts
                .AsNoTracking()
                .Where(p => followedIds.Contains(p.OrganizerId))
                .Where(p => p.EventId != null
                    || (p.OrganizerProfile != null && p.OrganizerProfile.IsActive && p.OrganizerProfile.IsApproved)
                    || (p.Organizer.OrganizerData != null && p.Organizer.OrganizerData.Approved))
                .OrderByDescending(p => p.CreatedAt)
                .Take(8);

            var friendsActivity = followedIds.Count == 0
                ? new List<PostCardViewModel>()
                : await QueryPostCards(friendPostsQuery, userId, cancellationToken);

            var organizerPosts = await QueryPostCards(_db.Posts
                    .AsNoTracking()
                    .Where(p => p.EventId != null
                        || (p.OrganizerProfile != null && p.OrganizerProfile.IsActive && p.OrganizerProfile.IsApproved)
                        || (p.Organizer.OrganizerData != null && p.Organizer.OrganizerData.Approved))
                    .OrderByDescending(p => p.CreatedAt)
                    .Take(12),
                userId,
                cancellationToken);

            var stories = await _db.Stories
                .AsNoTracking()
                .Where(s => s.ExpiresAt > now)
                .Where(s => (s.OrganizerProfile != null && s.OrganizerProfile.IsActive && s.OrganizerProfile.IsApproved)
                    || (s.Author.OrganizerData != null && s.Author.OrganizerData.Approved))
                .OrderByDescending(s => followedIds.Contains(s.AuthorId))
                .ThenByDescending(s => s.CreatedAt)
                .Take(18)
                .Select(s => new StoryCardViewModel
                {
                    Id = s.Id,
                    AuthorId = s.AuthorId,
                    AuthorName = s.OrganizerProfile != null
                        ? s.OrganizerProfile.DisplayName
                        : s.Author.OrganizerData != null && s.Author.OrganizerData.Approved
                        ? s.Author.OrganizerData.OrganizationName
                        : s.Author.UserName ?? string.Empty,
                    AuthorImageUrl = s.OrganizerProfile != null && !string.IsNullOrWhiteSpace(s.OrganizerProfile.AvatarImageUrl)
                        ? s.OrganizerProfile.AvatarImageUrl
                        : s.Author.ProfileImageUrl,
                    MediaUrl = s.MediaUrl,
                    MediaType = s.MediaType,
                    Caption = s.Caption,
                    CreatedAt = s.CreatedAt,
                    ExpiresAt = s.ExpiresAt,
                })
                .ToListAsync(cancellationToken);

            var suggestedProfiles = await _db.Users
                .AsNoTracking()
                .Where(u => userId == null || u.Id != userId)
                .Where(u => userId == null || !followedIds.Contains(u.Id))
                .OrderByDescending(u => u.Followers.Count + u.Events.Count + u.Posts.Count)
                .Take(6)
                .Select(u => new ProfileSummaryViewModel
                {
                    Id = u.Id,
                    DisplayName = u.OrganizerData != null && u.OrganizerData.Approved
                        ? u.OrganizerData.OrganizationName
                        : u.UserName ?? string.Empty,
                    Bio = u.Bio ?? (u.OrganizerData != null ? u.OrganizerData.Description : null),
                    ProfileImageUrl = u.OrganizerProfiles
                        .Where(p => p.IsActive && p.IsApproved && p.AvatarImageUrl != null && p.AvatarImageUrl != string.Empty)
                        .OrderByDescending(p => p.IsDefault)
                        .ThenByDescending(p => p.CreatedAt)
                        .Select(p => p.AvatarImageUrl)
                        .FirstOrDefault()
                        ?? u.ProfileImageUrl
                        ?? u.OrganizerProfiles
                            .Where(p => p.IsActive && p.IsApproved && p.CoverImageUrl != null && p.CoverImageUrl != string.Empty)
                            .OrderByDescending(p => p.IsDefault)
                            .ThenByDescending(p => p.CreatedAt)
                            .Select(p => p.CoverImageUrl)
                            .FirstOrDefault(),
                    IsOrganizer = u.OrganizerData != null && u.OrganizerData.Approved,
                    FollowersCount = u.Followers.Count,
                    FollowingCount = u.Following.Count,
                    PostsCount = u.Posts.Count,
                    EventsCount = u.Events.Count(e => e.IsApproved),
                    CurrentUserFollows = false,
                })
                .ToListAsync(cancellationToken);

            var searchResults = string.IsNullOrWhiteSpace(normalizedSearch)
                ? new List<FeedSearchResultViewModel>()
                : await SearchProfilesAsync(normalizedSearch, userId, cancellationToken);

            return new FeedViewModel
            {
                Stories = stories,
                RecommendedEvents = recommended,
                TrendingEvents = trending,
                FriendsActivity = friendsActivity,
                OrganizerPosts = organizerPosts,
                SuggestedProfiles = suggestedProfiles,
                SearchQuery = normalizedSearch,
                SearchResults = searchResults,
                HasPersonalSignals = hasPersonalSignals,
                PreferredCity = prefs?.PreferredCity,
                CurrentUserDisplayName = currentUser?.DisplayName,
                CurrentUserProfileImageUrl = currentUser?.ProfileImageUrl,
            };
        }

        public async Task TrackActivityAsync(string userId, UserActivityType activityType, int? eventId = null, int? postId = null, string? targetUserId = null, string? value = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            _db.UserActivities.Add(new UserActivity
            {
                UserId = userId,
                ActivityType = activityType,
                EventId = eventId,
                PostId = postId,
                TargetUserId = targetUserId,
                Value = value,
            });

            await _db.SaveChangesAsync(cancellationToken);
        }

        private async Task<List<FeedSearchResultViewModel>> SearchProfilesAsync(string searchTerm, string? currentUserId, CancellationToken cancellationToken)
        {
            var term = searchTerm.ToLowerInvariant();

            var users = await _db.Users
                .AsNoTracking()
                .Where(u =>
                    (u.UserName ?? string.Empty).ToLower().Contains(term)
                    || (u.FirstName ?? string.Empty).ToLower().Contains(term)
                    || (u.LastName ?? string.Empty).ToLower().Contains(term)
                    || (u.Bio ?? string.Empty).ToLower().Contains(term)
                    || (u.OrganizerData != null && (
                        (u.OrganizerData.OrganizationName ?? string.Empty).ToLower().Contains(term)
                        || (u.OrganizerData.Description ?? string.Empty).ToLower().Contains(term))))
                .OrderByDescending(u => u.Followers.Count + u.Events.Count + u.Posts.Count)
                .Take(8)
                .Select(u => new FeedSearchResultViewModel
                {
                    Id = "user:" + u.Id,
                    UserId = u.Id,
                    DisplayName = u.OrganizerData != null && u.OrganizerData.Approved
                        ? u.OrganizerData.OrganizationName
                        : u.UserName ?? ((u.FirstName ?? string.Empty) + " " + (u.LastName ?? string.Empty)).Trim(),
                    UserName = u.UserName,
                    Bio = u.Bio ?? (u.OrganizerData != null ? u.OrganizerData.Description : null),
                    ProfileImageUrl = u.ProfileImageUrl,
                    TypeKey = u.OrganizerData != null && u.OrganizerData.Approved ? "profile.type.organizer" : "profile.type.profile",
                    TypeText = u.OrganizerData != null && u.OrganizerData.Approved ? "Organizer" : "Profile",
                    FollowersCount = u.Followers.Count,
                    PostsCount = u.Posts.Count,
                    EventsCount = u.Events.Count(e => e.IsApproved),
                    CurrentUserFollows = currentUserId != null && u.Followers.Any(f => f.FollowerId == currentUserId),
                })
                .ToListAsync(cancellationToken);

            var pages = await _db.OrganizerProfiles
                .AsNoTracking()
                .Where(p => p.IsActive && p.IsApproved)
                .Where(p =>
                    p.DisplayName.ToLower().Contains(term)
                    || (p.Tagline ?? string.Empty).ToLower().Contains(term)
                    || (p.Description ?? string.Empty).ToLower().Contains(term)
                    || (p.City ?? string.Empty).ToLower().Contains(term)
                    || (p.Owner.UserName ?? string.Empty).ToLower().Contains(term))
                .OrderByDescending(p => p.Events.Count + p.Posts.Count)
                .Take(8)
                .Select(p => new FeedSearchResultViewModel
                {
                    Id = "page:" + p.Id,
                    UserId = p.OwnerId,
                    OrganizerProfileId = p.Id,
                    DisplayName = p.DisplayName,
                    UserName = p.Owner.UserName,
                    Bio = p.Tagline ?? p.Description,
                    ProfileImageUrl = p.AvatarImageUrl != null && p.AvatarImageUrl != string.Empty ? p.AvatarImageUrl : p.Owner.ProfileImageUrl,
                    TypeKey = "organizer.pages.public",
                    TypeText = "Public page",
                    FollowersCount = p.Owner.Followers.Count,
                    PostsCount = p.Posts.Count,
                    EventsCount = p.Events.Count(e => e.IsApproved),
                    CurrentUserFollows = currentUserId != null && p.Owner.Followers.Any(f => f.FollowerId == currentUserId),
                })
                .ToListAsync(cancellationToken);

            return pages
                .Concat(users)
                .GroupBy(r => r.Id)
                .Select(g => g.First())
                .Take(12)
                .ToList();
        }

        private static async Task<List<EventCardViewModel>> QueryEventCards(IQueryable<Event> query, string? userId, CancellationToken cancellationToken)
        {
            return await query
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
                    OrganizerProfileId = e.OrganizerProfileId,
                    OrganizerName = e.OrganizerProfile != null
                        ? e.OrganizerProfile.DisplayName
                        : e.Organizer.OrganizerData != null && e.Organizer.OrganizerData.Approved
                        ? e.Organizer.OrganizerData.OrganizationName
                        : e.Organizer.UserName ?? string.Empty,
                    LikesCount = e.Likes.Count,
                    CommentsCount = e.Comments.Count,
                    SavesCount = e.Saves.Count,
                    GoingCount = e.Attendances.Count(a => a.Status == EventAttendanceStatus.Going),
                    InterestedCount = e.Attendances.Count(a => a.Status == EventAttendanceStatus.Interested),
                    CurrentUserLiked = userId != null && e.Likes.Any(l => l.UserId == userId),
                    CurrentUserSaved = userId != null && e.Saves.Any(s => s.UserId == userId),
                    CurrentUserAttendanceStatus = userId == null
                        ? null
                        : e.Attendances
                            .Where(a => a.UserId == userId)
                            .Select(a => (EventAttendanceStatus?)a.Status)
                            .FirstOrDefault(),
                    Latitude = e.Latitude,
                    Longitude = e.Longitude,
                    HasActiveTickets = e.Tickets.Any(t => t.IsActive),
                    HasPaidTickets = e.Tickets.Any(t => t.IsActive && t.Price > 0m),
                    LowestPaidTicketPrice = e.Tickets
                        .Where(t => t.IsActive && t.Price > 0m)
                        .Min(t => (decimal?)t.Price),
                })
                .ToListAsync(cancellationToken);
        }

        private static async Task<List<PostCardViewModel>> QueryPostCards(IQueryable<Post> query, string? userId, CancellationToken cancellationToken)
        {
            return await query
                .Select(p => new PostCardViewModel
                {
                    Id = p.Id,
                    OrganizerId = p.OrganizerId,
                    OrganizerProfileId = p.OrganizerProfileId,
                    OrganizerName = p.OrganizerProfile != null
                        ? p.OrganizerProfile.DisplayName
                        : p.Organizer.OrganizerData != null && p.Organizer.OrganizerData.Approved
                        ? p.Organizer.OrganizerData.OrganizationName
                        : p.Organizer.UserName ?? string.Empty,
                    AuthorImageUrl = p.OrganizerProfile != null && !string.IsNullOrWhiteSpace(p.OrganizerProfile.AvatarImageUrl)
                        ? p.OrganizerProfile.AvatarImageUrl
                        : p.Organizer.ProfileImageUrl,
                    AuthorIsOrganizer = (p.OrganizerProfile != null && p.OrganizerProfile.IsActive && p.OrganizerProfile.IsApproved)
                        || (p.Organizer.OrganizerData != null && p.Organizer.OrganizerData.Approved),
                    Content = p.Content,
                    CreatedAt = p.CreatedAt,
                    EventId = p.EventId,
                    EventTitle = p.Event != null ? p.Event.Title : null,
                    FirstMediaUrl = p.Images.Select(i => i.ImageUrl).FirstOrDefault(),
                    FirstMediaType = p.Images.Select(i => i.MediaType).FirstOrDefault(),
                    LikesCount = p.Likes.Count,
                    CommentsCount = p.Comments.Count,
                    SavesCount = p.Saves.Count,
                    CurrentUserLiked = userId != null && p.Likes.Any(l => l.UserId == userId),
                    CurrentUserSaved = userId != null && p.Saves.Any(s => s.UserId == userId),
                })
                .ToListAsync(cancellationToken);
        }

        private static int ScoreEvent(
            EventCardViewModel ev,
            EventGenre? preferredGenre,
            string? preferredCity,
            IReadOnlyCollection<string> followedIds,
            IReadOnlyDictionary<EventGenre, int> genreScores,
            IReadOnlyDictionary<string, int> cityScores,
            IReadOnlyDictionary<string, int> organizerScores,
            DateTime now)
        {
            var score = 0;

            if (preferredGenre == ev.Genre) score += 60;
            if (!string.IsNullOrWhiteSpace(preferredCity) && string.Equals(preferredCity, ev.City, StringComparison.OrdinalIgnoreCase)) score += 35;
            if (followedIds.Contains(ev.OrganizerId)) score += 45;
            if (genreScores.TryGetValue(ev.Genre, out var genreScore)) score += genreScore;
            if (cityScores.TryGetValue(ev.City, out var cityScore)) score += cityScore;
            if (organizerScores.TryGetValue(ev.OrganizerId, out var organizerScore)) score += organizerScore;

            score += Math.Min(40, ev.LikesCount * 3 + ev.CommentsCount * 4 + ev.SavesCount * 4 + ev.GoingCount * 5 + ev.InterestedCount * 2);

            var daysAway = Math.Max(0, (ev.StartTime - now).TotalDays);
            score += Math.Max(0, 24 - (int)Math.Min(24, daysAway));

            return score;
        }

        private static int ActivityWeight(UserActivityType activityType)
        {
            return activityType switch
            {
                UserActivityType.EventGoing => 18,
                UserActivityType.EventInterested => 14,
                UserActivityType.EventSaved => 12,
                UserActivityType.EventLiked => 9,
                UserActivityType.EventViewed => 4,
                _ => 1,
            };
        }

        private class UserEventSignal
        {
            public EventGenre Genre { get; set; }
            public string City { get; set; } = null!;
            public string OrganizerId { get; set; } = null!;
            public UserActivityType ActivityType { get; set; }
        }
    }
}
