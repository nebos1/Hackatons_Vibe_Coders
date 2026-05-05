using EventsApp.ViewModels.Layouts;
using Microsoft.AspNetCore.Http;

namespace EventsApp.Services.AI
{
    public interface ILayoutAiService
    {
        bool IsEnabled { get; }

        string? LastError { get; }

        Task<VenueLayoutJsonModel?> GenerateLayoutAsync(
            string? description,
            IFormFile? image,
            CancellationToken cancellationToken = default);
    }
}
