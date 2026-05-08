namespace EventsApp.Services
{
    public interface IAppLinkService
    {
        string ToAbsoluteUrl(HttpRequest request, string? pathAndQuery);
    }

    public class AppLinkService : IAppLinkService
    {
        private readonly IConfiguration _configuration;

        public AppLinkService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string ToAbsoluteUrl(HttpRequest request, string? pathAndQuery)
        {
            var configuredUrl = GetSetting("APP_PUBLIC_URL", "PUBLIC_URL", "App:PublicUrl");

            var baseUrl = BuildBaseUrl(configuredUrl, request).TrimEnd('/') + "/";
            if (string.IsNullOrWhiteSpace(pathAndQuery))
            {
                return baseUrl.TrimEnd('/');
            }

            if (Uri.TryCreate(pathAndQuery, UriKind.Absolute, out var absolute))
            {
                if (string.Equals(absolute.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(absolute.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                {
                    return absolute.ToString();
                }

                pathAndQuery = string.IsNullOrWhiteSpace(absolute.PathAndQuery)
                    ? "/"
                    : absolute.PathAndQuery;
            }

            return new Uri(new Uri(baseUrl), pathAndQuery.TrimStart('/')).ToString();
        }

        private string? GetSetting(params string[] keys)
        {
            foreach (var key in keys)
            {
                var value = _configuration[key];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }

        private string BuildBaseUrl(string? configuredUrl, HttpRequest request)
        {
            var normalizedConfiguredUrl = NormalizePublicUrl(configuredUrl);
            if (!string.IsNullOrWhiteSpace(normalizedConfiguredUrl))
            {
                return normalizedConfiguredUrl;
            }

            var railwayDomain = NormalizePublicUrl(GetSetting("RAILWAY_PUBLIC_DOMAIN", "RAILWAY_STATIC_URL"));
            if (!string.IsNullOrWhiteSpace(railwayDomain))
            {
                return railwayDomain;
            }

            var scheme = request.Scheme;
            if (!string.Equals(scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                scheme = Uri.UriSchemeHttps;
            }

            if (request.Host.HasValue)
            {
                return $"{scheme}://{request.Host.Value}";
            }

            return "https://evento.business";
        }

        private static string? NormalizePublicUrl(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim().TrimEnd('/');
            if (!trimmed.Contains("://", StringComparison.Ordinal))
            {
                trimmed = "https://" + trimmed;
            }

            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) &&
                !string.IsNullOrWhiteSpace(uri.Host) &&
                (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
            {
                return uri.ToString().TrimEnd('/');
            }

            return null;
        }
    }
}
