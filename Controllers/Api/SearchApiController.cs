using EventsApp.Common;
using EventsApp.Models.AI;
using EventsApp.Services.AI;
using Microsoft.AspNetCore.Mvc;

namespace EventsApp.Controllers.Api
{
    [ApiController]
    [Route("api/search")]
    public class SearchApiController : ControllerBase
    {
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
        public async Task<IActionResult> Smart([FromBody] SmartSearchRequest? request, CancellationToken ct)
        {
            var query = (request?.Query ?? string.Empty).Trim();

            var result = new AiSearchResult
            {
                RawQuery = query,
                Keyword = string.IsNullOrWhiteSpace(query) ? null : query,
            };

            if (string.IsNullOrWhiteSpace(query))
            {
                result.AiUsed = false;
                result.AiStatus = "Empty";
                return Ok(result);
            }

            if (!_ai.IsEnabled)
            {
                ApplyCityFallback(result, query);
                result.AiUsed = false;
                result.AiStatus = "Disabled";
                result.AiStatusDetail = "Sirma AI not configured. Add Sirma_key + Sirma_agent_id to .env or user-secrets.";
                return Ok(result);
            }

            AiSearchIntent? intent = null;
            try
            {
                intent = await _ai.InterpretAsync(query, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Smart search call to Sirma AI threw");
            }

            if (intent != null)
            {
                result.City = intent.City;
                result.Genre = intent.Genre?.ToString();
                result.Keyword = intent.Keyword ?? result.Keyword;
                result.DateIntent = intent.DateIntent;
                result.NearMe = intent.NearMe;
                result.Latitude = intent.Latitude;
                result.Longitude = intent.Longitude;
                result.Keywords = intent.Keywords ?? Array.Empty<string>();
                result.AiUsed = true;
            }
            else
            {
                result.AiUsed = false;
            }

            result.AiStatus = _ai.LastStatus.ToString();
            result.AiStatusDetail = _ai.LastStatusDetail;

            ApplyCityFallback(result, query);

            return Ok(result);
        }

        private static void ApplyCityFallback(AiSearchResult result, string query)
        {
            if (result.Latitude.HasValue && result.Longitude.HasValue) return;

            string? cityName = result.City;
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
            }
        }
    }
}
