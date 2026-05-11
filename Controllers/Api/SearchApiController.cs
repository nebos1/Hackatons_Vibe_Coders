using EventsApp.Common;
using EventsApp.Models.AI;
using EventsApp.Services.AI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EventsApp.Controllers.Api
{
    [ApiController]
    [Route("api/search")]
    public class SearchApiController : ControllerBase
    {
        private const int MaxSmartQueryLength = 180;

        private readonly IAiSearchService _ai;
        private readonly ILogger<SearchApiController> _logger;

        public SearchApiController(IAiSearchService ai, ILogger<SearchApiController> logger)
        {
            _ai = ai;
            _logger = logger;
        }

        public class SmartSearchRequest
        {
            public string? Query { get; set; }
        }

        [HttpPost("smart")]
        [EnableRateLimiting("ai-light")]
        public async Task<IActionResult> Smart([FromBody] SmartSearchRequest? request, CancellationToken ct)
        {
            var query = (request?.Query ?? string.Empty).Trim();

            var result = new AiSearchResult
            {
                RawQuery = query,
            };

            if (string.IsNullOrWhiteSpace(query))
            {
                result.AiUsed = false;
                result.AiStatus = "Empty";
                return Ok(result);
            }

            if (query.Length > MaxSmartQueryLength)
            {
                result.AiUsed = false;
                result.AiStatus = "Rejected";
                result.AiStatusDetail = $"Query is too long. Maximum length is {MaxSmartQueryLength} characters.";
                ApplyKeywordFallback(result, query[..MaxSmartQueryLength]);
                return Ok(result);
            }

            var local = LocalEventSearchInterpreter.Parse(query, DateTime.UtcNow);
            AiSearchIntent? intent = local.Intent;
            var usedAi = false;

            if (!_ai.IsEnabled || local.HasStrongIntent || !local.ShouldAskAi)
            {
                ApplyIntent(result, intent);
                ApplyKeywordFallback(result, query);
                result.AiUsed = false;
                result.AiStatus = _ai.IsEnabled ? "Local" : "Disabled";
                result.AiStatusDetail = _ai.IsEnabled
                    ? "Parsed locally without spending AI tokens."
                    : "AI search is not configured. Local smart search was used.";
                return Ok(result);
            }

            try
            {
                var aiIntent = await _ai.InterpretAsync(query, ct);
                if (aiIntent != null)
                {
                    intent = LocalEventSearchInterpreter.Merge(intent, aiIntent);
                    usedAi = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Smart search AI call threw");
            }

            ApplyIntent(result, intent);
            result.AiUsed = usedAi;

            result.AiStatus = _ai.LastStatus.ToString();
            result.AiStatusDetail = usedAi
                ? _ai.LastStatusDetail
                : (_ai.LastStatusDetail ?? "AI did not improve the local parse; local smart search was used.");

            ApplyCityFallback(result, query);
            ApplyKeywordFallback(result, query);

            return Ok(result);
        }

        private static void ApplyCityFallback(AiSearchResult result, string query)
        {
            if (result.Latitude.HasValue && result.Longitude.HasValue) return;

            string? cityName = result.Cities.FirstOrDefault() ?? result.City;
            if (string.IsNullOrWhiteSpace(cityName))
            {
                foreach (var token in query.Split(new[] { ' ', ',', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (CityCoordinates.TryGetCoordinates(token, out _, out _))
                    {
                        cityName = token;
                        break;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(cityName) &&
                CityCoordinates.TryGetCoordinates(cityName, out var lat, out var lng))
            {
                result.Latitude ??= lat;
                result.Longitude ??= lng;
                result.City ??= cityName;
                if (result.Cities.Length == 0) result.Cities = new[] { cityName };
            }
        }

        private static void ApplyIntent(AiSearchResult result, AiSearchIntent? intent)
        {
            if (intent == null) return;

            result.City = intent.City;
            result.Cities = intent.Cities.Length > 0
                ? intent.Cities
                : (string.IsNullOrWhiteSpace(intent.City) ? Array.Empty<string>() : new[] { intent.City });
            result.Genre = intent.Genre?.ToString();
            result.Genres = intent.Genres.Length > 0
                ? intent.Genres.Select(g => g.ToString()).ToArray()
                : (intent.Genre.HasValue ? new[] { intent.Genre.Value.ToString() } : Array.Empty<string>());
            result.Keyword = intent.Keyword ?? result.Keyword;
            result.DateIntent = intent.DateIntent;
            result.NearMe = intent.NearMe;
            result.Latitude = intent.Latitude;
            result.Longitude = intent.Longitude;
            result.Keywords = intent.Keywords ?? Array.Empty<string>();
        }

        private static void ApplyKeywordFallback(AiSearchResult result, string query)
        {
            var stop = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "искам", "търся", "събития", "събитие", "events", "event", "near", "around", "около", "на"
            };
            var compact = string.Join(' ', query
                .Trim()
                .ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(part => !stop.Contains(part)));

            if (!string.IsNullOrWhiteSpace(result.Keyword))
            {
                result.Keyword = result.Keyword.Trim();
                return;
            }

            var hasStructuredFilters =
                !string.IsNullOrWhiteSpace(result.City) ||
                result.Cities.Length > 0 ||
                !string.IsNullOrWhiteSpace(result.Genre) ||
                result.Genres.Length > 0 ||
                !string.IsNullOrWhiteSpace(result.DateIntent) ||
                result.NearMe ||
                result.Latitude.HasValue ||
                result.Longitude.HasValue;

            if (result.AiUsed && hasStructuredFilters)
            {
                result.Keyword = null;
                result.Keywords = Array.Empty<string>();
                return;
            }

            result.Keyword = string.IsNullOrWhiteSpace(compact) ? (string.IsNullOrWhiteSpace(query) ? null : query) : compact;
        }
    }
}
