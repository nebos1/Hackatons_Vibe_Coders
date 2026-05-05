using EventsApp.Common;
using EventsApp.Data;
using EventsApp.Models;
using EventsApp.ViewModels.Social;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EventsApp.Services
{
    public class ActingIdentity
    {
        public AuthorIdentityType Type { get; set; }

        public int? OrganizerProfileId { get; set; }

        public int? BusinessWorkspaceId { get; set; }

        public string DisplayName { get; set; } = null!;

        public string? ImageUrl { get; set; }
    }

    public interface IActingIdentityService
    {
        Task<IReadOnlyList<ActingIdentityOptionViewModel>> GetOptionsAsync(HttpContext httpContext, int? preferredOrganizerProfileId = null, bool includePersonal = true, CancellationToken cancellationToken = default);

        Task<ActingIdentity?> ResolveAsync(HttpContext httpContext, string? key, int? preferredOrganizerProfileId = null, bool includePersonal = true, CancellationToken cancellationToken = default);
    }

    public class ActingIdentityService : IActingIdentityService
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IBusinessContextService _businessContext;

        public ActingIdentityService(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IBusinessContextService businessContext)
        {
            _db = db;
            _userManager = userManager;
            _businessContext = businessContext;
        }

        public async Task<IReadOnlyList<ActingIdentityOptionViewModel>> GetOptionsAsync(HttpContext httpContext, int? preferredOrganizerProfileId = null, bool includePersonal = true, CancellationToken cancellationToken = default)
        {
            var userId = _userManager.GetUserId(httpContext.User);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Array.Empty<ActingIdentityOptionViewModel>();
            }

            var user = await _db.Users
                .AsNoTracking()
                .Include(u => u.OrganizerData)
                .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

            if (user == null)
            {
                return Array.Empty<ActingIdentityOptionViewModel>();
            }

            var canUseOrganizerPages = httpContext.User.IsInRole(GlobalConstants.Roles.Organizer)
                && user.OrganizerData?.Approved == true;
            var context = await _businessContext.GetContextAsync(httpContext, cancellationToken);
            var pages = canUseOrganizerPages
                ? await _db.OrganizerProfiles
                    .AsNoTracking()
                    .Include(p => p.BusinessWorkspace)
                    .Where(p => p.OwnerId == userId && p.IsActive && p.IsApproved)
                    .Where(p => p.BusinessWorkspace == null || p.BusinessWorkspace.Status == BusinessWorkspaceStatus.Active)
                    .OrderByDescending(p => preferredOrganizerProfileId.HasValue && p.Id == preferredOrganizerProfileId.Value)
                    .ThenByDescending(p => context.Page != null && p.Id == context.Page.Id)
                    .ThenByDescending(p => p.IsDefaultForWorkspace)
                    .ThenByDescending(p => p.IsDefault)
                    .ThenBy(p => p.DisplayName)
                    .ToListAsync(cancellationToken)
                : new List<OrganizerProfile>();

            var defaultPageId = preferredOrganizerProfileId
                ?? context.Page?.Id
                ?? pages.FirstOrDefault()?.Id;

            var options = new List<ActingIdentityOptionViewModel>();
            if (includePersonal || pages.Count == 0)
            {
                options.Add(new ActingIdentityOptionViewModel
                {
                    Key = "user",
                    Label = "Personal profile: " + GetUserDisplayName(user),
                    DisplayName = GetUserDisplayName(user),
                    ImageUrl = user.ProfileImageUrl,
                    BadgeKey = httpContext.User.IsInRole(GlobalConstants.Roles.Admin) ? "identity.admin" : "identity.user",
                    IsDefault = defaultPageId == null,
                });
            }

            options.AddRange(pages.Select(p => new ActingIdentityOptionViewModel
            {
                Key = $"page:{p.Id}",
                Label = "Page: " + p.DisplayName,
                DisplayName = p.DisplayName,
                ImageUrl = p.AvatarImageUrl,
                BadgeKey = "identity.page",
                IsDefault = p.Id == defaultPageId,
            }));

            if (!options.Any(o => o.IsDefault) && options.Count > 0)
            {
                options[0].IsDefault = true;
            }

            return options;
        }

        public async Task<ActingIdentity?> ResolveAsync(HttpContext httpContext, string? key, int? preferredOrganizerProfileId = null, bool includePersonal = true, CancellationToken cancellationToken = default)
        {
            var userId = _userManager.GetUserId(httpContext.User);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            var options = await GetOptionsAsync(httpContext, preferredOrganizerProfileId, includePersonal, cancellationToken);
            var selectedKey = string.IsNullOrWhiteSpace(key)
                ? options.FirstOrDefault(o => o.IsDefault)?.Key
                : key;

            if (selectedKey?.StartsWith("page:", StringComparison.OrdinalIgnoreCase) == true
                && int.TryParse(selectedKey["page:".Length..], out var pageId))
            {
                var page = await _db.OrganizerProfiles
                    .AsNoTracking()
                    .Include(p => p.BusinessWorkspace)
                    .FirstOrDefaultAsync(p =>
                        p.Id == pageId &&
                        p.OwnerId == userId &&
                        p.IsActive &&
                        p.IsApproved &&
                        (p.BusinessWorkspace == null || p.BusinessWorkspace.Status == BusinessWorkspaceStatus.Active),
                        cancellationToken);

                if (page == null)
                {
                    return null;
                }

                return new ActingIdentity
                {
                    Type = AuthorIdentityType.OrganizerPage,
                    OrganizerProfileId = page.Id,
                    BusinessWorkspaceId = page.BusinessWorkspaceId,
                    DisplayName = page.DisplayName,
                    ImageUrl = page.AvatarImageUrl,
                };
            }

            if (includePersonal && options.Any(o => o.Key == "user"))
            {
                var user = await _db.Users.AsNoTracking().Include(u => u.OrganizerData).FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
                if (user == null)
                {
                    return null;
                }

                return new ActingIdentity
                {
                    Type = httpContext.User.IsInRole(GlobalConstants.Roles.Admin) ? AuthorIdentityType.Admin : AuthorIdentityType.User,
                    DisplayName = GetUserDisplayName(user),
                    ImageUrl = user.ProfileImageUrl,
                };
            }

            return null;
        }

        private static string GetUserDisplayName(ApplicationUser user)
        {
            var name = string.Join(" ", new[] { user.FirstName, user.LastName }.Where(v => !string.IsNullOrWhiteSpace(v)));
            return string.IsNullOrWhiteSpace(name) ? user.UserName ?? user.Email ?? string.Empty : name;
        }
    }
}
