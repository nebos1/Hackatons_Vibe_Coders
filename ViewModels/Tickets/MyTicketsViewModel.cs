namespace EventsApp.ViewModels.Tickets
{
    public class MyTicketsViewModel
    {
        public IReadOnlyList<MyTicketRowViewModel> Tickets { get; set; } = Array.Empty<MyTicketRowViewModel>();
    }

    public class MyTicketRowViewModel
    {
        public Guid Id { get; set; }
        public int EventId { get; set; }
        public int? EventOccurrenceId { get; set; }
        public string EventTitle { get; set; } = null!;
        public string TicketName { get; set; } = null!;
        public string Address { get; set; } = null!;
        public string City { get; set; } = null!;
        public DateTime StartTime { get; set; }
        public string? SeatLabel { get; set; }
        public string? AttendeeName { get; set; }
        public Guid PurchaseGroupId { get; set; }
        public bool IsPrimaryInPurchase { get; set; }
        public decimal Price { get; set; }
        public bool IsUsed { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
