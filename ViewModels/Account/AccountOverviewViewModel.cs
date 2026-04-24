namespace EventsApp.ViewModels.Account
{
    public class AccountOverviewViewModel
    {
        public string UserName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public string? ProfileImageUrl { get; set; }
        public string? Bio { get; set; }

        // Роля
        public string Role { get; set; } = null!;

        // Статус на кандидатура за организатор
        public bool HasApplied { get; set; }
        public bool IsApproved { get; set; }
        public string? OrganizationName { get; set; }
        public DateTime? ApplicationDate { get; set; }

        // Статистики (за organizers)
        public int VenuesCount { get; set; }
        public int EventsCount { get; set; }
        public int PostsCount { get; set; }
    }
}
