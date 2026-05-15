using EventsApp.Common;
using EventsApp.Data;
using EventsApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EventsApp.Infrastructure
{
    public static class EventsSeeder
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            var db = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var logger = serviceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger(nameof(EventsSeeder));

            var admins = await userManager.GetUsersInRoleAsync(GlobalConstants.Roles.Admin);
            var admin = admins.FirstOrDefault();
            if (admin == null)
            {
                logger.LogWarning("EventsSeeder skipped: no admin user found.");
                return;
            }

            var today = DateTime.UtcNow.Date;
            var seedEvents = BuildSeedEvents(admin.Id, today);

            var seedTitles = seedEvents.Select(s => s.Title).ToList();
            var existingEvents = await db.Events
                .Where(e => seedTitles.Contains(e.Title))
                .ToListAsync();

            foreach (var existing in existingEvents)
            {
                var seed = seedEvents.First(e => e.Title == existing.Title);
                if (string.IsNullOrWhiteSpace(existing.ImageUrl))
                {
                    existing.ImageUrl = seed.ImageUrl;
                }
            }

            var toInsert = seedEvents
                .Where(e => existingEvents.All(existing => existing.Title != e.Title))
                .ToList();

            if (toInsert.Count == 0 && !db.ChangeTracker.HasChanges())
            {
                return;
            }

            db.Events.AddRange(toInsert);
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded {Count} events.", toInsert.Count);
        }

        private static List<Event> BuildSeedEvents(string organizerId, DateTime today)
        {
            return new List<Event>
            {
                new Event
                {
                    OrganizerId = organizerId,
                    Title = "Рок вечер в Каракул — Русе",
                    Description = "Жива рок музика на високо ниво в сърцето на Русе. Местни и гостуващи групи.",
                    StartTime = today.AddDays(2).AddHours(20),
                    EndTime = today.AddDays(2).AddHours(23).AddMinutes(30),
                    Genre = EventGenre.Rock,
                    Address = "ул. Александровска 87, Русе",
                    City = "Русе",
                    Latitude = 43.8564,
                    Longitude = 25.9658,
                    ImageUrl = SeedImage("rock-ruse"),
                    IsApproved = true,
                    TicketingMode = EventTicketingMode.GeneralAdmission,
                    CreatedAt = DateTime.UtcNow,
                },
                new Event
                {
                    OrganizerId = organizerId,
                    Title = "Jazz Night при Дунава — Русе",
                    Description = "Камерна джаз вечер с гледка към реката. Топла атмосфера и качествена музика.",
                    StartTime = today.AddDays(3).AddHours(19).AddMinutes(30),
                    EndTime = today.AddDays(3).AddHours(22).AddMinutes(30),
                    Genre = EventGenre.Jazz,
                    Address = "Кей крайбрежна, Русе",
                    City = "Русе",
                    Latitude = 43.8564,
                    Longitude = 25.9658,
                    ImageUrl = SeedImage("jazz-ruse"),
                    IsApproved = true,
                    TicketingMode = EventTicketingMode.GeneralAdmission,
                    CreatedAt = DateTime.UtcNow,
                },
                new Event
                {
                    OrganizerId = organizerId,
                    Title = "Electronic Underground — Русе",
                    Description = "Техно и хаус до сутринта. Местни и международни DJ-и.",
                    StartTime = today.AddDays(5).AddHours(22),
                    EndTime = today.AddDays(6).AddHours(4),
                    Genre = EventGenre.Electronic,
                    Address = "Индустриална зона, Русе",
                    City = "Русе",
                    Latitude = 43.8564,
                    Longitude = 25.9658,
                    ImageUrl = SeedImage("electronic-ruse"),
                    IsApproved = true,
                    TicketingMode = EventTicketingMode.GeneralAdmission,
                    CreatedAt = DateTime.UtcNow,
                },
                new Event
                {
                    OrganizerId = organizerId,
                    Title = "Поп вечер в София",
                    Description = "Любими хитове и нови парчета на живо. Голяма сцена в центъра на София.",
                    StartTime = today.AddDays(4).AddHours(20),
                    EndTime = today.AddDays(4).AddHours(23),
                    Genre = EventGenre.Pop,
                    Address = "пл. Народно събрание 1, София",
                    City = "София",
                    Latitude = 42.6977,
                    Longitude = 23.3219,
                    ImageUrl = SeedImage("pop-sofia"),
                    IsApproved = true,
                    TicketingMode = EventTicketingMode.GeneralAdmission,
                    CreatedAt = DateTime.UtcNow,
                },
                new Event
                {
                    OrganizerId = organizerId,
                    Title = "Театър под звездите — Пловдив",
                    Description = "Класика на открито в Античния театър. Незабравима лятна вечер.",
                    StartTime = today.AddDays(6).AddHours(21),
                    EndTime = today.AddDays(6).AddHours(23).AddMinutes(30),
                    Genre = EventGenre.Theater,
                    Address = "Античен театър, Пловдив",
                    City = "Пловдив",
                    Latitude = 42.1465,
                    Longitude = 24.7480,
                    ImageUrl = SeedImage("theater-plovdiv"),
                    IsApproved = true,
                    TicketingMode = EventTicketingMode.GeneralAdmission,
                    CreatedAt = DateTime.UtcNow,
                },
            };
        }

        private static string SeedImage(string seed) => $"https://picsum.photos/seed/seed-event-{seed}/1200/720";
    }
}
