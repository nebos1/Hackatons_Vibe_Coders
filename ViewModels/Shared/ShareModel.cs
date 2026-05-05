namespace EventsApp.ViewModels.Shared
{
    public class ShareModel
    {
        public string Url { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? ChatShareAction { get; set; }
        public int? ChatShareId { get; set; }
    }
}
