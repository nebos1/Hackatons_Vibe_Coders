using System.ComponentModel.DataAnnotations;
using EventsApp.Common;

namespace EventsApp.ViewModels.Tickets
{
    public class TicketCreateEditViewModel
    {
        public Guid Id { get; set; }

        [Required]
        public int EventId { get; set; }

        public string? EventTitle { get; set; }

        [Required]
        [StringLength(GlobalConstants.Ticket.NameMaxLength, MinimumLength = GlobalConstants.Ticket.NameMinLength)]
        public string Name { get; set; } = null!;

        [StringLength(GlobalConstants.Ticket.DescriptionMaxLength)]
        public string? Description { get; set; }

        [Required]
        [Range(0, 1_000_000)]
        public decimal Price { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        [Display(Name = "Total quantity")]
        public int QuantityTotal { get; set; }

        [Range(0, int.MaxValue)]
        [Display(Name = "Remaining (defaults to total)")]
        public int? QuantityRemaining { get; set; }

        [StringLength(GlobalConstants.Ticket.ImageUrlMaxLength)]
        [Display(Name = "Image URL")]
        public string? ImageUrl { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;
    }
}
