using EventsApp.Common;
using EventsApp.Data;
using EventsApp.Models;
using EventsApp.Services;
using EventsApp.Services.Email;
using EventsApp.ViewModels.Account;
using EventsApp.ViewModels.Events;
using EventsApp.ViewModels.Posts;
using EventsApp.ViewModels.Tickets;
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
        private readonly IMediaUploadService _mediaUpload;
        private readonly IPlatformPermissionService _permissions;
        private readonly IEmailConfirmationSender _emailConfirmationSender;

        public AccountController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IMediaUploadService mediaUpload,
            IPlatformPermissionService permissions,
            IEmailConfirmationSender emailConfirmationSender)
        {
            _db = db;
            _userManager = userManager;
            _mediaUpload = mediaUpload;
            _permissions = permissions;
            _emailConfirmationSender = emailConfirmationSender;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault() ?? GlobalConstants.Roles.User;

            var orgData = await _db.OrganizerData
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrganizerId == user.Id);

            var preferences = await _db.UserPreferences
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == user.Id);

            var vm = new AccountOverviewViewModel
            {
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                EmailConfirmed = user.EmailConfirmed,
                PhoneNumber = user.PhoneNumber,
                Bio = user.Bio,
                ProfileImageUrl = user.ProfileImageUrl,
                CreatedAt = user.CreatedAt,
                Role = role,
                HasApplied = orgData != null,
                IsApproved = orgData?.Approved ?? false,
                CanCreatePosts = await _permissions.CanCreatePostAsync(User),
                OrganizationName = orgData?.OrganizationName,
                ApplicationDate = orgData?.CreatedAt,
                HasPreferences = preferences != null,
                PreferredGenre = preferences?.PreferredGenre,
                PreferredCity = preferences?.PreferredCity,
                MinAge = preferences?.MinAge,
                MaxDistanceKm = preferences?.MaxDistanceKm,
            };

            vm.EventsCount = await _db.Events.CountAsync(e => e.OrganizerId == user.Id);
            vm.PostsCount = await _db.Posts.CountAsync(p => p.OrganizerId == user.Id);
            vm.FollowersCount = await _db.Follows.CountAsync(f => f.FollowingId == user.Id);
            vm.FollowingCount = await _db.Follows.CountAsync(f => f.FollowerId == user.Id);
            vm.SavedPostsCount = await _db.PostSaves.CountAsync(s => s.UserId == user.Id);
            vm.SavedEventsCount = await _db.EventSaves.CountAsync(s => s.UserId == user.Id);
            vm.GoingEventsCount = await _db.EventAttendances.CountAsync(a => a.UserId == user.Id && a.Status == EventAttendanceStatus.Going);

            vm.MyPosts = await _db.Posts
                .AsNoTracking()
                .Where(p => p.OrganizerId == user.Id)
                .OrderByDescending(p => p.CreatedAt)
                .Take(4)
                .Select(p => new PostCardViewModel
                {
                    Id = p.Id,
                    OrganizerId = p.OrganizerId,
                    OrganizerProfileId = p.OrganizerProfileId,
                    OrganizerName = p.Organizer.OrganizerData != null && p.Organizer.OrganizerData.Approved
                        ? p.Organizer.OrganizerData.OrganizationName
                        : p.Organizer.UserName ?? string.Empty,
                    Content = p.Content,
                    CreatedAt = p.CreatedAt,
                    EventId = p.EventId,
                    EventTitle = p.Event != null ? p.Event.Title : null,
                    FirstMediaUrl = p.Images.Select(i => i.ImageUrl).FirstOrDefault(),
                    FirstMediaType = p.Images.Select(i => i.MediaType).FirstOrDefault(),
                    LikesCount = p.Likes.Count,
                    CommentsCount = p.Comments.Count,
                    SavesCount = p.Saves.Count,
                    CurrentUserLiked = p.Likes.Any(l => l.UserId == user.Id),
                    CurrentUserSaved = p.Saves.Any(s => s.UserId == user.Id),
                    AuthorImageUrl = p.Organizer.ProfileImageUrl,
                    AuthorIsOrganizer = p.Organizer.OrganizerData != null && p.Organizer.OrganizerData.Approved,
                })
                .ToListAsync();

            vm.LikedEvents = await _db.EventLikes
                .AsNoTracking()
                .Where(l => l.UserId == user.Id &&
                    (l.Event.IsApproved || role == GlobalConstants.Roles.Admin || l.Event.OrganizerId == user.Id))
                .OrderByDescending(l => l.CreatedAt)
                .Take(4)
                .Select(l => new EventCardViewModel
                {
                    Id = l.EventId,
                    Title = l.Event.Title,
                    ImageUrl = l.Event.ImageUrl,
                    Address = l.Event.Address,
                    City = l.Event.City,
                    StartTime = l.Event.StartTime,
                    Genre = l.Event.Genre,
                    IsApproved = l.Event.IsApproved,
                    OrganizerId = l.Event.OrganizerId,
                    OrganizerProfileId = l.Event.OrganizerProfileId,
                    OrganizerName = l.Event.OrganizerProfile != null ? l.Event.OrganizerProfile.DisplayName : l.Event.Organizer.UserName ?? string.Empty,
                    LikesCount = l.Event.Likes.Count,
                    CommentsCount = l.Event.Comments.Count,
                    SavesCount = l.Event.Saves.Count,
                    GoingCount = l.Event.Attendances.Count(a => a.Status == EventAttendanceStatus.Going),
                    InterestedCount = l.Event.Attendances.Count(a => a.Status == EventAttendanceStatus.Interested),
                    CurrentUserLiked = true,
                    CurrentUserSaved = l.Event.Saves.Any(s => s.UserId == user.Id),
                    CurrentUserAttendanceStatus = l.Event.Attendances
                        .Where(a => a.UserId == user.Id)
                        .Select(a => (EventAttendanceStatus?)a.Status)
                        .FirstOrDefault(),
                    HasActiveTickets = l.Event.Tickets.Any(t => t.IsActive),
                    HasPaidTickets = l.Event.Tickets.Any(t => t.IsActive && t.Price > 0m),
                    LowestPaidTicketPrice = l.Event.Tickets
                        .Where(t => t.IsActive && t.Price > 0m)
                        .Min(t => (decimal?)t.Price),
                })
                .ToListAsync();

            vm.LikedPosts = await _db.PostLikes
                .AsNoTracking()
                .Where(l => l.UserId == user.Id)
                .OrderByDescending(l => l.CreatedAt)
                .Take(4)
                .Select(l => new PostCardViewModel
                {
                    Id = l.PostId,
                    OrganizerId = l.Post.OrganizerId,
                    OrganizerProfileId = l.Post.OrganizerProfileId,
                    OrganizerName = l.Post.OrganizerProfile != null ? l.Post.OrganizerProfile.DisplayName : l.Post.Organizer.UserName ?? string.Empty,
                    Content = l.Post.Content,
                    CreatedAt = l.Post.CreatedAt,
                    EventId = l.Post.EventId,
                    EventTitle = l.Post.Event != null ? l.Post.Event.Title : null,
                    FirstMediaUrl = l.Post.Images.Select(i => i.ImageUrl).FirstOrDefault(),
                    FirstMediaType = l.Post.Images.Select(i => i.MediaType).FirstOrDefault(),
                    LikesCount = l.Post.Likes.Count,
                    CommentsCount = l.Post.Comments.Count,
                    SavesCount = l.Post.Saves.Count,
                    CurrentUserLiked = true,
                    CurrentUserSaved = l.Post.Saves.Any(s => s.UserId == user.Id),
                    AuthorImageUrl = l.Post.Organizer.ProfileImageUrl,
                    AuthorIsOrganizer = l.Post.Organizer.OrganizerData != null && l.Post.Organizer.OrganizerData.Approved,
                })
                .ToListAsync();

            vm.SavedPosts = await _db.PostSaves
                .AsNoTracking()
                .Where(s => s.UserId == user.Id)
                .OrderByDescending(s => s.CreatedAt)
                .Take(4)
                .Select(s => new PostCardViewModel
                {
                    Id = s.PostId,
                    OrganizerId = s.Post.OrganizerId,
                    OrganizerProfileId = s.Post.OrganizerProfileId,
                    OrganizerName = s.Post.Organizer.OrganizerData != null && s.Post.Organizer.OrganizerData.Approved
                        ? s.Post.Organizer.OrganizerData.OrganizationName
                        : s.Post.Organizer.UserName ?? string.Empty,
                    Content = s.Post.Content,
                    CreatedAt = s.Post.CreatedAt,
                    EventId = s.Post.EventId,
                    EventTitle = s.Post.Event != null ? s.Post.Event.Title : null,
                    FirstMediaUrl = s.Post.Images.Select(i => i.ImageUrl).FirstOrDefault(),
                    FirstMediaType = s.Post.Images.Select(i => i.MediaType).FirstOrDefault(),
                    LikesCount = s.Post.Likes.Count,
                    CommentsCount = s.Post.Comments.Count,
                    SavesCount = s.Post.Saves.Count,
                    CurrentUserLiked = s.Post.Likes.Any(l => l.UserId == user.Id),
                    CurrentUserSaved = true,
                    AuthorImageUrl = s.Post.Organizer.ProfileImageUrl,
                    AuthorIsOrganizer = s.Post.Organizer.OrganizerData != null && s.Post.Organizer.OrganizerData.Approved,
                })
                .ToListAsync();

            vm.SavedEvents = await _db.EventSaves
                .AsNoTracking()
                .Where(s => s.UserId == user.Id &&
                    (s.Event.IsApproved || role == GlobalConstants.Roles.Admin || s.Event.OrganizerId == user.Id))
                .OrderByDescending(s => s.CreatedAt)
                .Take(4)
                .Select(s => new EventCardViewModel
                {
                    Id = s.EventId,
                    Title = s.Event.Title,
                    ImageUrl = s.Event.ImageUrl,
                    Address = s.Event.Address,
                    City = s.Event.City,
                    StartTime = s.Event.StartTime,
                    Genre = s.Event.Genre,
                    IsApproved = s.Event.IsApproved,
                    OrganizerId = s.Event.OrganizerId,
                    OrganizerProfileId = s.Event.OrganizerProfileId,
                    OrganizerName = s.Event.OrganizerProfile != null ? s.Event.OrganizerProfile.DisplayName : "Public page",
                    LikesCount = s.Event.Likes.Count,
                    CommentsCount = s.Event.Comments.Count,
                    SavesCount = s.Event.Saves.Count,
                    GoingCount = s.Event.Attendances.Count(a => a.Status == EventAttendanceStatus.Going),
                    InterestedCount = s.Event.Attendances.Count(a => a.Status == EventAttendanceStatus.Interested),
                    CurrentUserLiked = s.Event.Likes.Any(l => l.UserId == user.Id),
                    CurrentUserSaved = true,
                    CurrentUserAttendanceStatus = s.Event.Attendances
                        .Where(a => a.UserId == user.Id)
                        .Select(a => (EventAttendanceStatus?)a.Status)
                        .FirstOrDefault(),
                    HasActiveTickets = s.Event.Tickets.Any(t => t.IsActive),
                    HasPaidTickets = s.Event.Tickets.Any(t => t.IsActive && t.Price > 0m),
                    LowestPaidTicketPrice = s.Event.Tickets
                        .Where(t => t.IsActive && t.Price > 0m)
                        .Min(t => (decimal?)t.Price),
                })
                .ToListAsync();

            vm.PurchasedTicketsCount = await _db.UserTickets
                .CountAsync(ut => ut.Transaction.UserId == user.Id);

            vm.RecentTickets = await _db.UserTickets
                .AsNoTracking()
                .Where(ut => ut.Transaction.UserId == user.Id)
                .OrderByDescending(ut => ut.CreatedAt)
                .Take(5)
                .Select(ut => new MyTicketRowViewModel
                {
                    Id = ut.Id,
                    EventId = ut.Ticket.EventId,
                    EventTitle = ut.Ticket.Event.Title,
                    TicketName = ut.Ticket.Name,
                    Address = ut.Ticket.Event.Address,
                    City = ut.Ticket.Event.City,
                    StartTime = ut.Ticket.Event.StartTime,
                    Price = ut.Ticket.Price,
                    IsUsed = ut.IsUsed,
                    CreatedAt = ut.CreatedAt,
                })
                .ToListAsync();

            var hasViewedAny = await _db.UserActivities
                .AnyAsync(a => a.UserId == user.Id && a.ActivityType == UserActivityType.EventViewed);
            var hasAttendedAny = await _db.EventAttendances
                .AnyAsync(a => a.UserId == user.Id);
            vm.OnboardingChecklist = new UserOnboardingChecklist
            {
                HasSavedEvent = vm.SavedEventsCount > 0,
                HasAttended = hasAttendedAny,
                HasFollowed = vm.FollowingCount > 0,
                HasViewedEvent = hasViewedAny,
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendEmailConfirmation()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (await _userManager.IsEmailConfirmedAsync(user))
            {
                TempData["StatusMessage"] = "Имейлът вече е потвърден.";
                return RedirectToAction(nameof(Index));
            }

            var orgData = await _db.OrganizerData
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrganizerId == user.Id);
            var nextUrl = orgData != null
                ? Url.Action(nameof(EditApplication), "Account", new { welcome = "organizer" }) ?? "/Account/EditApplication?welcome=organizer"
                : Url.Content("~/Preferences/Edit?welcome=1");

            await _emailConfirmationSender.SendAsync(user, Request, nextUrl, orgData != null);
            TempData["StatusMessage"] = "Изпратихме нов линк за потвърждение на имейла.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> EditProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            return View(CreateEditProfileViewModel(user));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(EditProfileViewModel input)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            input.UserName = input.UserName?.Trim() ?? string.Empty;
            input.PhoneNumber = string.IsNullOrWhiteSpace(input.PhoneNumber) ? null : input.PhoneNumber.Trim();
            input.Bio = string.IsNullOrWhiteSpace(input.Bio) ? null : input.Bio.Trim();
            input.ProfileImageUrl = string.IsNullOrWhiteSpace(input.ProfileImageUrl) ? null : input.ProfileImageUrl.Trim();
            input.Email = user.Email;
            input.CreatedAt = user.CreatedAt;

            ModelState.Clear();
            if (!TryValidateModel(input))
            {
                return View(input);
            }

            user.UserName = input.UserName;
            user.PhoneNumber = input.PhoneNumber;
            user.Bio = input.Bio;
            if (input.ProfileImageFile != null && input.ProfileImageFile.Length > 0)
            {
                try
                {
                    var media = await _mediaUpload.SaveAsync(input.ProfileImageFile, "profiles");
                    user.ProfileImageUrl = media?.Url;
                }
                catch (InvalidOperationException ex)
                {
                    ModelState.AddModelError(nameof(input.ProfileImageFile), ex.Message);
                    return View(input);
                }
            }
            else
            {
                user.ProfileImageUrl = input.ProfileImageUrl;
            }

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                AddIdentityErrors(result);
                return View(input);
            }

            TempData["StatusMessage"] = "Profile updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Apply()
        {
            var userId = _userManager.GetUserId(User)!;

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
                City = input.City,
                Country = input.Country,
                ReferralSource = input.ReferralSource,
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

            if (orgData.Approved && User.IsInRole(GlobalConstants.Roles.Organizer))
            {
                return RedirectToAction("Profile", "Organizer");
            }

            return View(new ApplyOrganizerViewModel
            {
                OrganizationName = orgData.OrganizationName,
                Description = orgData.Description,
                PhoneNumber = orgData.PhoneNumber,
                City = orgData.City,
                Country = orgData.Country,
                ReferralSource = orgData.ReferralSource,
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
            orgData.City = input.City;
            orgData.Country = input.Country;
            orgData.ReferralSource = input.ReferralSource;
            orgData.Website = input.Website;
            orgData.CompanyNumber = input.CompanyNumber;

            await _db.SaveChangesAsync();
            TempData["StatusMessage"] = "Application updated.";
            return RedirectToAction(nameof(Index));
        }

        private static EditProfileViewModel CreateEditProfileViewModel(ApplicationUser user)
        {
            return new EditProfileViewModel
            {
                UserName = user.UserName ?? string.Empty,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Bio = user.Bio,
                ProfileImageUrl = user.ProfileImageUrl,
                CreatedAt = user.CreatedAt,
            };
        }

        private void AddIdentityErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }
    }
}
