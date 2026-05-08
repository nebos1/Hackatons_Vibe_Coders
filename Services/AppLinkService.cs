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
                return absolute.ToString();
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

        private static string BuildBaseUrl(string? configuredUrl, HttpRequest request)
        {
            if (!string.IsNullOrWhiteSpace(configuredUrl)
                && Uri.TryCreate(configuredUrl.Trim(), UriKind.Absolute, out var configuredUri))
            {
                return configuredUri.ToString();
            }

            return $"{request.Scheme}://{request.Host}";
        }
    }
}
