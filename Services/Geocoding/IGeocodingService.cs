namespace EventsApp.Services.Geocoding
{
    public record GeocodeResult(double Latitude, double Longitude, string? DisplayName);

    public interface IGeocodingService
    {
        Task<GeocodeResult?> GeocodeAsync(string address, string? city, CancellationToken ct = default);
    }
}
