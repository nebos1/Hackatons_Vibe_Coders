using System.ComponentModel.DataAnnotations;
using EventsApp.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EventsApp.ViewModels.Posts
{
    public class PostCreateEditViewModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(GlobalConstants.Post.ContentMaxLength, MinimumLength = GlobalConstants.Post.ContentMinLength)]
        public string Content { get; set; } = null!;

        [Display(Name = "Event (optional)")]
        public int? EventId { get; set; }

        [Display(Name = "Photo or video")]
        public IFormFile? MediaFile { get; set; }

        public string? ImageUrl { get; set; }

        public string? CurrentMediaUrl { get; set; }

        public IEnumerable<SelectListItem> Events { get; set; } = Array.Empty<SelectListItem>();
    }
}
