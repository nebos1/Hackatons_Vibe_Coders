namespace EventsApp.ViewModels.Tickets
{
    public class EventTicketOptionViewModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int QuantityRemaining { get; set; }
        public int MaxPurchaseQuantity { get; set; }
        public bool IsActive { get; set; }
        public bool RequiresAttendeeNames { get; set; }
        public bool SoldOut => QuantityRemaining <= 0;
    }
}
