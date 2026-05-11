using EventsApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace EventsApp.Controllers
{
    [Route("media/{**key}")]
    public class MediaController : Controller
    {
        private readonly IRemoteMediaService _remoteMediaService;

        public MediaController(IRemoteMediaService remoteMediaService)
        {
            _remoteMediaService = remoteMediaService;
        }

        [HttpGet]
        public async Task<IActionResult> Get(string key, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(key) ||
                key.Contains("..", StringComparison.Ordinal) ||
                key.Contains("\\", StringComparison.Ordinal) ||
                key.StartsWith("/", StringComparison.Ordinal))
            {
                return NotFound();
            }

            var signedUrl = await _remoteMediaService.CreateReadUrlAsync(key, cancellationToken);
            if (string.IsNullOrWhiteSpace(signedUrl))
            {
                return NotFound();
            }

            Response.Headers.CacheControl = "public, max-age=3600, stale-while-revalidate=86400";
            return Redirect(signedUrl);
        }
    }
}
