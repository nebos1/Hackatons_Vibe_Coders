namespace EventsApp.ViewModels.Tickets
{
    public class EventTicketOptionViewModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public bool UsesSeatPricing { get; set; }
        public IReadOnlyList<EventTicketSectionPriceViewModel> SectionPrices { get; set; } = Array.Empty<EventTicketSectionPriceViewModel>();
        public int QuantityRemaining { get; set; }
        public int MaxPurchaseQuantity { get; set; }
        public bool IsActive { get; set; }
        public bool RequiresAttendeeNames { get; set; }
        public bool SoldOut => QuantityRemaining <= 0;
    }

    public class EventTicketSectionPriceViewModel
    {
        public int SectionId { get; set; }

        public decimal Price { get; set; }
    }
}
