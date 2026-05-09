namespace EventsApp.ViewModels.Admin
{
    public class AdminOrganizerRowViewModel
    {
        public string OrganizerId { get; set; } = null!;
        public string UserName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string OrganizationName { get; set; } = null!;
        public string? PhoneNumber { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        public string? ReferralSource { get; set; }
        public string? Website { get; set; }
        public string? CompanyNumber { get; set; }
        public bool Approved { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
