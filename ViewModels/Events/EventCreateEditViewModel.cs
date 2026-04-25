using System.ComponentModel.DataAnnotations;
using EventsApp.Common;
using EventsApp.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EventsApp.ViewModels.Events
{
    public class EventCreateEditViewModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(GlobalConstants.Event.TitleMaxLength, MinimumLength = GlobalConstants.Event.TitleMinLength)]
        public string Title { get; set; } = null!;

        [StringLength(GlobalConstants.Event.DescriptionMaxLength)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Required]
        [StringLength(GlobalConstants.Event.CityMaxLength)]
        [Display(Name = "City")]
        public string City { get; set; } = null!;

        [Required]
        [StringLength(GlobalConstants.Event.AddressMaxLength)]
        [Display(Name = "Address")]
        public string Address { get; set; } = null!;

        [Required]
        [Display(Name = "Start time")]
        [DataType(DataType.DateTime)]
        public DateTime StartTime { get; set; } = DateTime.UtcNow.AddDays(1);

        [Required]
        [Display(Name = "End time")]
        [DataType(DataType.DateTime)]
        public DateTime EndTime { get; set; } = DateTime.UtcNow.AddDays(1).AddHours(2);

        [Required]
        public EventGenre Genre { get; set; }


        [Url]
        [StringLength(GlobalConstants.Event.ImageUrlMaxLength)]
        [Display(Name = "Image URL")]
        public string? ImageUrl { get; set; }

        [Display(Name = "Upload Photo")]
        public IFormFile? Photo { get; set; }

        [Range(-90, 90)]
        [Display(Name = "Latitude")]
        public double? Latitude { get; set; }

        [Range(-180, 180)]
        [Display(Name = "Longitude")]
        public double? Longitude { get; set; }

        [Display(Name = "Approved")]
        public bool IsApproved { get; set; }

        public bool CanEditApproval { get; set; }
    }
}
