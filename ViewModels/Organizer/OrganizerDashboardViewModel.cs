using EventsApp.Models;
using EventsApp.ViewModels.Events;
using EventsApp.ViewModels.Posts;

namespace EventsApp.ViewModels.Organizer
{
    public class OrganizerDashboardViewModel
    {
        public bool HasProfile { get; set; }
        public string? OrganizationName { get; set; }
        public string? Description { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Website { get; set; }
        public string? CompanyNumber { get; set; }
        public bool Approved { get; set; }

        public int EventsCount { get; set; }
        public int PostsCount { get; set; }
        public int TicketTypesCount { get; set; }
        public int TicketsSoldCount { get; set; }
        public int EventsWithTicketsCount { get; set; }
        public int LayoutsCount { get; set; }

        public int UpcomingEventsCount { get; set; }
        public int PastEventsCount { get; set; }
        public int TicketsUsedCount { get; set; }
        public int TotalLikes { get; set; }
        public int TotalComments { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AverageTicketPrice { get; set; }
        public int Last30DaysSold { get; set; }
        public decimal Last30DaysRevenue { get; set; }

        public IReadOnlyList<TopEventStat> TopByTicketsSold { get; set; } = Array.Empty<TopEventStat>();
        public IReadOnlyList<TopEventStat> TopByRevenue { get; set; } = Array.Empty<TopEventStat>();
        public IReadOnlyList<GenreCountStat> GenreBreakdown { get; set; } = Array.Empty<GenreCountStat>();
        public IReadOnlyList<CityCountStat> CityBreakdown { get; set; } = Array.Empty<CityCountStat>();
        public IReadOnlyList<DailySalesPoint> SalesLast30Days { get; set; } = Array.Empty<DailySalesPoint>();

        public IReadOnlyList<EventCardViewModel> RecentEvents { get; set; } = Array.Empty<EventCardViewModel>();
        public IReadOnlyList<PostCardViewModel> RecentPosts { get; set; } = Array.Empty<PostCardViewModel>();
        public IReadOnlyList<OrganizerEventTicketRowViewModel> EventTicketRows { get; set; } = Array.Empty<OrganizerEventTicketRowViewModel>();
    }

    public class OrganizerEventTicketRowViewModel
    {
        public int EventId { get; set; }
        public string EventTitle { get; set; } = null!;
        public DateTime StartTime { get; set; }
        public bool HasActiveTickets { get; set; }
        public int Sold { get; set; }
    }

    public class TopEventStat
    {
        public int EventId { get; set; }
        public string Title { get; set; } = null!;
        public int Sold { get; set; }
        public decimal Revenue { get; set; }
    }

    public class GenreCountStat
    {
        public EventGenre Genre { get; set; }
        public int Count { get; set; }
    }

    public class CityCountStat
    {
        public string City { get; set; } = null!;
        public int Count { get; set; }
    }

    public class DailySalesPoint
    {
        public DateTime Date { get; set; }
        public int Sold { get; set; }
        public decimal Revenue { get; set; }
    }
}
