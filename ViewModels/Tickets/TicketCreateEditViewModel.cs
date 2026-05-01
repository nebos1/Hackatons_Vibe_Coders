using System.ComponentModel.DataAnnotations;
using EventsApp.Common;
using Microsoft.AspNetCore.Http;

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

        [Display(Name = "Безплатен билет")]
        public bool IsFree { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        [Display(Name = "Total quantity")]
        public int QuantityTotal { get; set; }

        [Range(0, int.MaxValue)]
        [Display(Name = "Remaining (defaults to total)")]
        public int? QuantityRemaining { get; set; }

        public string? ImageUrl { get; set; }

        [Display(Name = "Ticket image")]
        public IFormFile? ImageFile { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;
    }
}
