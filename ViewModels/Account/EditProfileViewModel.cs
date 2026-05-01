using System.ComponentModel.DataAnnotations;
using EventsApp.Common;
using Microsoft.AspNetCore.Http;

namespace EventsApp.ViewModels.Account
{
    public class EditProfileViewModel
    {
        [Required]
        [StringLength(GlobalConstants.User.UserNameMaxLength, MinimumLength = GlobalConstants.User.UserNameMinLength)]
        [Display(Name = "User name")]
        public string UserName { get; set; } = string.Empty;

        [Display(Name = "Email")]
        public string? Email { get; set; }

        [StringLength(GlobalConstants.Organizer.PhoneNumberMaxLength)]
        [Display(Name = "Phone number")]
        public string? PhoneNumber { get; set; }

        [StringLength(GlobalConstants.User.BioMaxLength)]
        [Display(Name = "Bio")]
        public string? Bio { get; set; }

        public string? ProfileImageUrl { get; set; }

        [Display(Name = "Profile image")]
        public IFormFile? ProfileImageFile { get; set; }

        [Display(Name = "Created at")]
        public DateTime CreatedAt { get; set; }
    }
}
