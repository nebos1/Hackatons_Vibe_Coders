namespace EventsApp.ViewModels.Tickets
{
    public class TicketManageViewModel
    {
        public int EventId { get; set; }
        public string EventTitle { get; set; } = null!;
        public IReadOnlyList<TicketRowViewModel> Tickets { get; set; } = Array.Empty<TicketRowViewModel>();
    }

    public class TicketRowViewModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public decimal Price { get; set; }
        public int QuantityTotal { get; set; }
        public int QuantityRemaining { get; set; }
        public bool IsActive { get; set; }
        public bool RequiresAttendeeNames { get; set; }
        public int Sold { get; set; }
        public int Used { get; set; }
    }
}
