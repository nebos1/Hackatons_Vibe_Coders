using EventsApp.Data;
using EventsApp.Models;
using EventsApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventsApp.Controllers
{
    [Authorize]
    [Route("Push")]
    public class PushController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPushNotificationService _pushNotifications;

        public PushController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IPushNotificationService pushNotifications)
        {
            _db = db;
            _userManager = userManager;
            _pushNotifications = pushNotifications;
        }

        [HttpGet("PublicKey")]
        public IActionResult PublicKey()
        {
            if (!_pushNotifications.IsConfigured || string.IsNullOrWhiteSpace(_pushNotifications.PublicKey))
            {
                return NotFound();
            }

            return Json(new { publicKey = _pushNotifications.PublicKey });
        }

        [HttpPost("Subscribe")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Subscribe([FromBody] PushSubscribeRequest request)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId)
                || string.IsNullOrWhiteSpace(request.Endpoint)
                || string.IsNullOrWhiteSpace(request.Keys?.P256DH)
                || string.IsNullOrWhiteSpace(request.Keys?.Auth))
            {
                return BadRequest();
            }

            var now = DateTime.UtcNow;
            var subscription = await _db.UserPushSubscriptions
                .FirstOrDefaultAsync(s => s.Endpoint == request.Endpoint);

            if (subscription == null)
            {
                subscription = new UserPushSubscription
                {
                    UserId = userId,
                    Endpoint = request.Endpoint,
                    CreatedAt = now,
                };
                _db.UserPushSubscriptions.Add(subscription);
            }
            else if (subscription.UserId != userId)
            {
                subscription.UserId = userId;
            }

            subscription.P256DH = request.Keys.P256DH;
            subscription.Auth = request.Keys.Auth;
            subscription.UserAgent = Request.Headers.UserAgent.ToString();
            subscription.LastSeenAt = now;

            await _db.SaveChangesAsync();

            return Ok(new { ok = true });
        }

        [HttpPost("Unsubscribe")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unsubscribe([FromBody] PushUnsubscribeRequest request)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(request.Endpoint))
            {
                return BadRequest();
            }

            await _db.UserPushSubscriptions
                .Where(s => s.UserId == userId && s.Endpoint == request.Endpoint)
                .ExecuteDeleteAsync();

            return Ok(new { ok = true });
        }

        public class PushSubscribeRequest
        {
            public string Endpoint { get; set; } = string.Empty;

            public PushSubscriptionKeys? Keys { get; set; }
        }

        public class PushSubscriptionKeys
        {
            public string P256DH { get; set; } = string.Empty;

            public string Auth { get; set; } = string.Empty;
        }

        public class PushUnsubscribeRequest
        {
            public string Endpoint { get; set; } = string.Empty;
        }
    }
}
