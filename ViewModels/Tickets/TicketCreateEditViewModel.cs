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
        [Display(Name = "Име на билета")]
        public string Name { get; set; } = null!;

        [StringLength(GlobalConstants.Ticket.DescriptionMaxLength)]
        [Display(Name = "Описание")]
        public string? Description { get; set; }

        [Required]
        [Range(0, 1_000_000)]
        [Display(Name = "Цена")]
        public decimal Price { get; set; }

        [Display(Name = "Безплатен билет")]
        public bool IsFree { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        [Display(Name = "Общо количество")]
        public int QuantityTotal { get; set; }

        [Range(0, int.MaxValue)]
        [Display(Name = "Оставащи билети")]
        public int? QuantityRemaining { get; set; }

        public string? ImageUrl { get; set; }

        [Display(Name = "Снимка на билет")]
        public IFormFile? ImageFile { get; set; }

        [Display(Name = "Активен")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Изисквай имена на посетители")]
        public bool RequiresAttendeeNames { get; set; }

        public bool HasSeatLayout { get; set; }

        public int SuggestedSeatCapacity { get; set; }

        public List<TicketLayoutSectionPriceViewModel> LayoutSections { get; set; } = new();
    }

    public class TicketLayoutSectionPriceViewModel
    {
        public int SectionId { get; set; }

        public string Name { get; set; } = null!;

        public string ColorHex { get; set; } = "#2456ff";

        public int SeatsCount { get; set; }

        [Range(0, 1_000_000)]
        public decimal SectionPrice { get; set; }
    }
}
