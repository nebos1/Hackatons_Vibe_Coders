using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;

namespace EventsApp.Services.Geocoding
{
    public class NominatimGeocodingService : IGeocodingService
    {
        private readonly HttpClient _http;
        private readonly ILogger<NominatimGeocodingService> _logger;

        public NominatimGeocodingService(HttpClient http, ILogger<NominatimGeocodingService> logger)
        {
            _http = http;
            _logger = logger;
            if (_http.BaseAddress == null)
            {
                _http.BaseAddress = new Uri("https://nominatim.openstreetmap.org/");
            }
            _http.DefaultRequestHeaders.UserAgent.Clear();
            _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GrooveOn", "1.0"));
            _http.DefaultRequestHeaders.AcceptLanguage.Clear();
            _http.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("bg"));
            _http.Timeout = TimeSpan.FromSeconds(8);
        }

        public async Task<GeocodeResult?> GeocodeAsync(string address, string? city, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(address) && string.IsNullOrWhiteSpace(city)) return null;

            var query = string.IsNullOrWhiteSpace(city)
                ? address.Trim()
                : (string.IsNullOrWhiteSpace(address) ? city!.Trim() : $"{address.Trim()}, {city.Trim()}");

            var url = "search?format=jsonv2" +
                      $"&q={Uri.EscapeDataString(query)}" +
                      "&limit=1&addressdetails=1&countrycodes=bg";

            try
            {
                using var resp = await _http.GetAsync(url, ct);
                if (!resp.IsSuccessStatusCode) return null;

                using var stream = await resp.Content.ReadAsStreamAsync(ct);
                using var doc = await JsonDocument.ParseAsync(stream, default, ct);

                if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                {
                    return null;
                }

                var first = doc.RootElement[0];
                if (!first.TryGetProperty("lat", out var latEl) || !first.TryGetProperty("lon", out var lonEl)) return null;

                if (!double.TryParse(latEl.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)) return null;
                if (!double.TryParse(lonEl.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lng)) return null;

                var display = first.TryGetProperty("display_name", out var dn) ? dn.GetString() : null;
                return new GeocodeResult(lat, lng, display);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Nominatim geocode failed for query {Query}", query);
                return null;
            }
        }
    }
}
