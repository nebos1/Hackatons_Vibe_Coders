namespace EventsApp.ViewModels.Tickets
{
    public class UserTicketDetailsViewModel
    {
        public Guid Id { get; set; }
        public Guid TicketId { get; set; }
        public Guid TransactionId { get; set; }
        public string TicketName { get; set; } = null!;
        public string EventTitle { get; set; } = null!;
        public int EventId { get; set; }
        public int? EventOccurrenceId { get; set; }
        public string Address { get; set; } = null!;
        public string City { get; set; } = null!;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? SeatLabel { get; set; }
        public string? AttendeeName { get; set; }
        public Guid PurchaseGroupId { get; set; }
        public bool IsPrimaryInPurchase { get; set; }
        public decimal Price { get; set; }
        public string TransactionStatus { get; set; } = null!;
        public string QrCode { get; set; } = null!;
        public bool IsUsed { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UsedAt { get; set; }
        public string? UsedByOrganizerName { get; set; }
        public string OwnerUserName { get; set; } = null!;
        public string OwnerEmail { get; set; } = null!;
    }
}
