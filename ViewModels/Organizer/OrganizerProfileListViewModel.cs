namespace EventsApp.ViewModels.Organizer
{
    public class OrganizerProfileListViewModel
    {
        public IReadOnlyList<OrganizerProfileRowViewModel> Profiles { get; set; } = Array.Empty<OrganizerProfileRowViewModel>();
    }

    public class OrganizerProfileRowViewModel
    {
        public int Id { get; set; }
        public string DisplayName { get; set; } = null!;
        public string? Tagline { get; set; }
        public string? City { get; set; }
        public string? AvatarImageUrl { get; set; }
        public string? CoverImageUrl { get; set; }
        public bool IsDefault { get; set; }
        public bool IsActive { get; set; }
        public int EventsCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
