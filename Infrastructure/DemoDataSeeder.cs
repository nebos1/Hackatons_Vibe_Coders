using EventsApp.Common;
using EventsApp.Data;
using EventsApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EventsApp.Infrastructure
{
    public static class DemoDataSeeder
    {
        private const string DemoPassword = "Demo123";
        // Sentinel email used to detect whether the demo set has already been seeded.
        private const string SentinelEmail = "ivan.dimitrov@demo.bg";

        public static async Task SeedAsync(IServiceProvider services)
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

            if (await db.Users.AnyAsync(u => u.Email == SentinelEmail)) return;

            var rnd = new Random(20260425);
            var now = DateTime.UtcNow;

            var organizers = new[]
            {
                new
                {
                    Email = "sofia.sound@demo.bg", Username = "sofia.sound", First = "Александър", Last = "Петров",
                    OrgName = "Sofia Sound Collective", Phone = "+359 88 555 0101", Web = "https://sofiasound.bg",
                    Company = "BG203456789",
                    Bio = "Колектив от диджеи и промоутъри за тъмни техно нощи в столицата.",
                    Avatar = "https://images.unsplash.com/photo-1571266028243-d220bc6e3e3e?auto=format&fit=crop&w=400&q=80",
                },
                new
                {
                    Email = "beat.rooms@demo.bg", Username = "beat.rooms", First = "Мира", Last = "Стоянова",
                    OrgName = "Beat Rooms Plovdiv", Phone = "+359 89 412 7733", Web = "https://beatrooms.bg",
                    Company = "BG204998112",
                    Bio = "Камерни джаз и lounge вечери в сърцето на Капана.",
                    Avatar = "https://images.unsplash.com/photo-1494790108377-be9c29b29330?auto=format&fit=crop&w=400&q=80",
                },
                new
                {
                    Email = "sea.wave@demo.bg", Username = "sea.wave", First = "Никола", Last = "Кирилов",
                    OrgName = "Sea Wave Events", Phone = "+359 87 200 4480", Web = "https://seawave.bg",
                    Company = "BG205667291",
                    Bio = "Летни фестивали и beach paries по Черноморието.",
                    Avatar = "https://images.unsplash.com/photo-1500648767791-00dcc994a43e?auto=format&fit=crop&w=400&q=80",
                },
            };

            var orgUsers = new List<ApplicationUser>();
            foreach (var o in organizers)
            {
                var u = new ApplicationUser
                {
                    UserName = o.Username,
                    Email = o.Email,
                    EmailConfirmed = true,
                    FirstName = o.First,
                    LastName = o.Last,
                    Bio = o.Bio,
                    ProfileImageUrl = o.Avatar,
                    CreatedAt = now.AddMonths(-6),
                };
                var res = await userManager.CreateAsync(u, DemoPassword);
                if (!res.Succeeded) continue;

                await userManager.AddToRoleAsync(u, GlobalConstants.Roles.Organizer);
                db.OrganizerData.Add(new OrganizerData
                {
                    OrganizerId = u.Id,
                    OrganizationName = o.OrgName,
                    Description = o.Bio,
                    PhoneNumber = o.Phone,
                    Website = o.Web,
                    CompanyNumber = o.Company,
                    Approved = true,
                    CreatedAt = now.AddMonths(-6),
                });
                orgUsers.Add(u);
            }

            var users = new[]
            {
                new { Email = "ivan.dimitrov@demo.bg", Username = "ivan.dimitrov", First = "Иван", Last = "Димитров",
                    Bio = "Меломан, DJ от време на време.",
                    Avatar = "https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?auto=format&fit=crop&w=400&q=80" },
                new { Email = "petya.k@demo.bg", Username = "petya.k", First = "Петя", Last = "Колева",
                    Bio = "Обичам джаз и виновни вечери в Капана.",
                    Avatar = "https://images.unsplash.com/photo-1438761681033-6461ffad8d80?auto=format&fit=crop&w=400&q=80" },
                new { Email = "georgi.t@demo.bg", Username = "georgi.t", First = "Георги", Last = "Тодоров",
                    Bio = "Живея за летните фестивали по морето.",
                    Avatar = "https://images.unsplash.com/photo-1607746882042-944635dfe10e?auto=format&fit=crop&w=400&q=80" },
                new { Email = "maria.v@demo.bg", Username = "maria.v", First = "Мария", Last = "Василева",
                    Bio = "Хроникьор на нощния живот в София.",
                    Avatar = "https://images.unsplash.com/photo-1544005313-94ddf0286df2?auto=format&fit=crop&w=400&q=80" },
                new { Email = "stefan.r@demo.bg", Username = "stefan.r", First = "Стефан", Last = "Райков",
                    Bio = "Пътувам за концерти. Бира + рок > всичко.",
                    Avatar = "https://images.unsplash.com/photo-1628157588553-5eeea00af15c?auto=format&fit=crop&w=400&q=80" },
            };

            var regularUsers = new List<ApplicationUser>();
            foreach (var u in users)
            {
                var au = new ApplicationUser
                {
                    UserName = u.Username,
                    Email = u.Email,
                    EmailConfirmed = true,
                    FirstName = u.First,
                    LastName = u.Last,
                    Bio = u.Bio,
                    ProfileImageUrl = u.Avatar,
                    CreatedAt = now.AddMonths(-3),
                };
                var res = await userManager.CreateAsync(au, DemoPassword);
                if (!res.Succeeded) continue;
                await userManager.AddToRoleAsync(au, GlobalConstants.Roles.User);
                regularUsers.Add(au);
            }

            await db.SaveChangesAsync();

            // Events: id maps to whether tickets should be generated.
            var eventsSpec = new[]
            {
                new
                {
                    Org = orgUsers[0],
                    Title = "Underground Pulse Vol. 12",
                    Description = "Дванадесетата нощ от Underground Pulse поредицата събира два международни хедлайнера и трима локални любимци за безкомпромисно техно сет от 22:00 до 06:00. Очаквайте мощна звукова система, дим, лазери и плътна публика.",
                    City = "София", Address = "Mixtape 5, ул. Шейново 7",
                    Lat = 42.6957, Lng = 23.3338,
                    Genre = EventGenre.Electronic,
                    Image = "https://images.unsplash.com/photo-1571266028243-d220bc6e3e3e?auto=format&fit=crop&w=1200&q=80",
                    StartOffsetDays = 14, DurationHours = 8, HasTickets = true,
                },
                new
                {
                    Org = orgUsers[0],
                    Title = "House Therapy: Sunday Edition",
                    Description = "Дневен сет с deep и soulful house в градината на НДК. Без билети, безплатен вход — само вибрация и слънце.",
                    City = "София", Address = "НДК, пл. България 1",
                    Lat = 42.6868, Lng = 23.3192,
                    Genre = EventGenre.Pop,
                    Image = "https://images.unsplash.com/photo-1429962714451-bb934ecdc4ec?auto=format&fit=crop&w=1200&q=80",
                    StartOffsetDays = 21, DurationHours = 6, HasTickets = false,
                },
                new
                {
                    Org = orgUsers[1],
                    Title = "Jazz Sessions Vol. 7",
                    Description = "Седмо издание на джаз вечерите в Капана. Българско трио + специален китарист от Гърция в интимна камерна обстановка.",
                    City = "Пловдив", Address = "Капана, ул. Абаджийска 8",
                    Lat = 42.1505, Lng = 24.7505,
                    Genre = EventGenre.Jazz,
                    Image = "https://images.unsplash.com/photo-1516280440614-37939bbacd81?auto=format&fit=crop&w=1200&q=80",
                    StartOffsetDays = 9, DurationHours = 4, HasTickets = true,
                },
                new
                {
                    Org = orgUsers[1],
                    Title = "Акустично & Живо",
                    Description = "Камерна вечер с трима акустични изпълнители и swap китари. Подходящо за идеална първа среща.",
                    City = "Пловдив", Address = "Кафе Petnoto, ул. 4-ти януари 25",
                    Lat = 42.1450, Lng = 24.7460,
                    Genre = EventGenre.Folk,
                    Image = "https://images.unsplash.com/photo-1493225457124-a3eb161ffa5f?auto=format&fit=crop&w=1200&q=80",
                    StartOffsetDays = -10, DurationHours = 3, HasTickets = false,
                },
                new
                {
                    Org = orgUsers[2],
                    Title = "Sunwaves x Burgas Beach",
                    Description = "Двудневен open-air фестивал на плажа с 12 диджеи на две сцени. Парти от залез до изгрев.",
                    City = "Бургас", Address = "Морска градина, северен плаж",
                    Lat = 42.4912, Lng = 27.4805,
                    Genre = EventGenre.Festival,
                    Image = "https://images.unsplash.com/photo-1459749411175-04bf5292ceea?auto=format&fit=crop&w=1200&q=80",
                    StartOffsetDays = 35, DurationHours = 36, HasTickets = true,
                },
                new
                {
                    Org = orgUsers[2],
                    Title = "Varna Beach Sessions",
                    Description = "Седмични петъчни сетове на плажа на Варна. Свободен достъп, donation jar за артистите.",
                    City = "Варна", Address = "Морско казино, плаж Север",
                    Lat = 43.2099, Lng = 27.9251,
                    Genre = EventGenre.Electronic,
                    Image = "https://images.unsplash.com/photo-1470225620780-dba8ba36b745?auto=format&fit=crop&w=1200&q=80",
                    StartOffsetDays = 4, DurationHours = 5, HasTickets = false,
                },
                new
                {
                    Org = orgUsers[0],
                    Title = "Vinyl Only: 90s Hip Hop Night",
                    Description = "Само от винил. Класиките на 90-те с двама диджеи, които си носят кутиите. Ограничен капацитет — 120 души.",
                    City = "София", Address = "Bar 100 grams, ул. Парчевич 38",
                    Lat = 42.6929, Lng = 23.3158,
                    Genre = EventGenre.HipHop,
                    Image = "https://images.unsplash.com/photo-1514525253161-7a46d19cd819?auto=format&fit=crop&w=1200&q=80",
                    StartOffsetDays = 7, DurationHours = 5, HasTickets = true,
                },
                new
                {
                    Org = orgUsers[1],
                    Title = "Класика на свещи",
                    Description = "Камерен квартет изпълнява Вивалди и Бах в зала, осветена от 200 свещи. Без приказки, без телефони.",
                    City = "Пловдив", Address = "Античен театър, Стария град",
                    Lat = 42.1466, Lng = 24.7510,
                    Genre = EventGenre.Classical,
                    Image = "https://images.unsplash.com/photo-1465847899084-d164df4dedc6?auto=format&fit=crop&w=1200&q=80",
                    StartOffsetDays = 28, DurationHours = 2, HasTickets = true,
                },
            };

            var createdEvents = new List<Event>();
            foreach (var e in eventsSpec)
            {
                var start = now.AddDays(e.StartOffsetDays);
                var ev = new Event
                {
                    OrganizerId = e.Org.Id,
                    Title = e.Title,
                    Description = e.Description,
                    City = e.City,
                    Address = e.Address,
                    Latitude = e.Lat,
                    Longitude = e.Lng,
                    Genre = e.Genre,
                    ImageUrl = e.Image,
                    StartTime = start,
                    EndTime = start.AddHours(e.DurationHours),
                    IsApproved = true,
                    CreatedAt = now.AddDays(-Math.Abs(e.StartOffsetDays) - 5),
                };
                db.Events.Add(ev);
                createdEvents.Add(ev);
            }
            await db.SaveChangesAsync();

            // Tickets — only for events flagged HasTickets
            var ticketsByEvent = new Dictionary<int, List<Ticket>>();
            for (int i = 0; i < eventsSpec.Length; i++)
            {
                if (!eventsSpec[i].HasTickets) continue;
                var ev = createdEvents[i];

                var ticketSet = ev.Genre switch
                {
                    EventGenre.Festival => new[]
                    {
                        ("Earlybird Pass", "Двудневен пропуск, ограничено количество.", 89m, 100, 42),
                        ("Standard Pass", "Двудневен пропуск.", 129m, 400, 215),
                        ("VIP Pass", "VIP зона, бар, паркинг.", 249m, 60, 18),
                    },
                    EventGenre.Classical => new[]
                    {
                        ("Места партер", "Седящи места, партер.", 45m, 80, 36),
                        ("Балкон", "Седящи места, балкон.", 30m, 60, 21),
                    },
                    EventGenre.Jazz => new[]
                    {
                        ("Стандартен билет", "Свободни места.", 25m, 80, 47),
                    },
                    EventGenre.HipHop => new[]
                    {
                        ("Ранна предпродажба", "Ограничено до 50 бр.", 20m, 50, 31),
                        ("Стандартен билет", "Стандартен вход.", 30m, 70, 12),
                    },
                    _ => new[]
                    {
                        ("Standard", "Целогодишен достъп до залата.", 35m, 250, 124),
                        ("VIP", "Зона с бар и сядане.", 70m, 50, 18),
                    },
                };

                var list = new List<Ticket>();
                foreach (var (name, desc, price, total, sold) in ticketSet)
                {
                    var tk = new Ticket
                    {
                        EventId = ev.Id,
                        Name = name,
                        Description = desc,
                        Price = price,
                        QuantityTotal = total,
                        QuantityRemaining = total - sold,
                        IsActive = true,
                        CreatedAt = now.AddDays(-30),
                    };
                    db.Tickets.Add(tk);
                    list.Add(tk);
                }
                ticketsByEvent[ev.Id] = list;
            }
            await db.SaveChangesAsync();

            // Generate user purchases (Paid transactions) so the dashboard stats are populated.
            foreach (var (evId, ticketList) in ticketsByEvent)
            {
                foreach (var tk in ticketList)
                {
                    var soldCount = tk.QuantityTotal - tk.QuantityRemaining;
                    soldCount = Math.Min(soldCount, 30); // cap at 30 user-tickets per type for seed
                    if (soldCount <= 0) continue;

                    for (int s = 0; s < soldCount; s++)
                    {
                        var buyer = regularUsers[rnd.Next(regularUsers.Count)];
                        var tx = new Transaction
                        {
                            UserId = buyer.Id,
                            TotalAmount = tk.Price,
                            Status = GlobalConstants.TransactionStatuses.Paid,
                            CreatedAt = now.AddDays(-rnd.Next(1, 28)).AddHours(-rnd.Next(0, 23)),
                        };
                        db.Transactions.Add(tx);

                        var ut = new UserTicket
                        {
                            TicketId = tk.Id,
                            Transaction = tx,
                            QrCode = Guid.NewGuid().ToString("N"),
                            IsUsed = rnd.NextDouble() < 0.18,
                            CreatedAt = tx.CreatedAt,
                        };
                        if (ut.IsUsed)
                        {
                            ut.UsedAt = tx.CreatedAt.AddHours(rnd.Next(1, 48));
                            ut.UsedByOrganizerId = ((Event)createdEvents.First(e => e.Id == evId)).OrganizerId;
                        }
                        db.UserTickets.Add(ut);
                    }
                }
            }
            await db.SaveChangesAsync();

            // Posts (some referencing events, some standalone)
            var postSpecs = new (ApplicationUser Org, string Content, int? EventIdx, string[] Images)[]
            {
                (orgUsers[0], "Билетите за Underground Pulse Vol. 12 излязоха. Ranged предпродажба до петък — после се вдига цената.", 0,
                    new[] { "https://images.unsplash.com/photo-1571266028243-d220bc6e3e3e?auto=format&fit=crop&w=1000&q=80" }),
                (orgUsers[0], "Снимки от последното House Therapy. Благодарим на всички! 🌞", 1,
                    new[] {
                        "https://images.unsplash.com/photo-1429962714451-bb934ecdc4ec?auto=format&fit=crop&w=1000&q=80",
                        "https://images.unsplash.com/photo-1493225457124-a3eb161ffa5f?auto=format&fit=crop&w=1000&q=80"
                    }),
                (orgUsers[1], "Beat Rooms се мести в нова зала! Очаквайте Jazz Sessions Vol. 7 на новия адрес.", 2,
                    new[] { "https://images.unsplash.com/photo-1516280440614-37939bbacd81?auto=format&fit=crop&w=1000&q=80" }),
                (orgUsers[1], "Понякога не ни трябват пиано и контрабас — само 6 струни и една свещ.", 3,
                    Array.Empty<string>()),
                (orgUsers[2], "Sunwaves x Burgas: разкриваме третия headliner следващия вторник. Подсказка: Берлин 🇩🇪", 4,
                    new[] { "https://images.unsplash.com/photo-1459749411175-04bf5292ceea?auto=format&fit=crop&w=1000&q=80" }),
                (orgUsers[2], "Beach Session #4 беше пълнен. Видео от вечерта — линк в bio.", 5,
                    new[] { "https://images.unsplash.com/photo-1470225620780-dba8ba36b745?auto=format&fit=crop&w=1000&q=80" }),
                (orgUsers[0], "Търсим резидентни диджеи за есенната ни поредица. DM-нете ни до 30-ти.", null,
                    Array.Empty<string>()),
                (orgUsers[1], "Какво бихте искали да чуете на следващия Jazz Sessions? Коментирайте — топ 3 ще влязат в плейлистата на вечерта.", null,
                    Array.Empty<string>()),
            };

            var createdPosts = new List<Post>();
            foreach (var p in postSpecs)
            {
                var post = new Post
                {
                    OrganizerId = p.Org.Id,
                    EventId = p.EventIdx.HasValue ? createdEvents[p.EventIdx.Value].Id : null,
                    Content = p.Content,
                    CreatedAt = now.AddDays(-rnd.Next(1, 25)).AddHours(-rnd.Next(0, 23)),
                };
                db.Posts.Add(post);
                createdPosts.Add(post);
            }
            await db.SaveChangesAsync();

            for (int i = 0; i < postSpecs.Length; i++)
            {
                foreach (var img in postSpecs[i].Images)
                {
                    db.PostImages.Add(new PostImage
                    {
                        PostId = createdPosts[i].Id,
                        ImageUrl = img,
                        MediaType = PostMediaType.Image,
                    });
                }
            }
            await db.SaveChangesAsync();

            // Likes & comments on events
            var eventComments = new[]
            {
                "Идвам! Кой иска да се събере преди?",
                "Last edition was insane 🔥",
                "Има ли паркинг наблизо?",
                "Това е точно това, което ми трябваше за уикенда.",
                "Препоръчвам! Бях преди и звукът беше топ.",
                "Ще има ли late check-in?",
                "Можем ли да ползваме картата за плащане на бара?",
                "Колко време продължава първият сет?",
            };

            foreach (var ev in createdEvents)
            {
                var likers = regularUsers.OrderBy(_ => rnd.Next()).Take(rnd.Next(2, regularUsers.Count + 1)).ToList();
                foreach (var u in likers)
                {
                    db.EventLikes.Add(new EventLike
                    {
                        EventId = ev.Id,
                        UserId = u.Id,
                        CreatedAt = now.AddDays(-rnd.Next(1, 30)),
                    });
                }

                var commenters = regularUsers.OrderBy(_ => rnd.Next()).Take(rnd.Next(1, 4)).ToList();
                foreach (var u in commenters)
                {
                    db.EventComments.Add(new EventComment
                    {
                        EventId = ev.Id,
                        UserId = u.Id,
                        Content = eventComments[rnd.Next(eventComments.Length)],
                        CreatedAt = now.AddDays(-rnd.Next(1, 25)).AddHours(-rnd.Next(0, 23)),
                    });
                }
            }

            // Likes & comments on posts
            var postComments = new[]
            {
                "❤️",
                "Идваме с приятели!",
                "Звучи страхотно, чакаме повече инфо.",
                "Кой ще е headliner-ът?",
                "Бях на миналата вечер — топ беше.",
                "Можем ли да си купим билет на входа?",
                "Кога има отстъпка за студенти?",
                "Любим организатор 🙌",
            };

            foreach (var post in createdPosts)
            {
                var likers = regularUsers.OrderBy(_ => rnd.Next()).Take(rnd.Next(1, regularUsers.Count + 1)).ToList();
                foreach (var u in likers)
                {
                    db.PostLikes.Add(new PostLike
                    {
                        PostId = post.Id,
                        UserId = u.Id,
                        CreatedAt = now.AddDays(-rnd.Next(1, 20)),
                    });
                }

                var commenters = regularUsers.OrderBy(_ => rnd.Next()).Take(rnd.Next(0, 3)).ToList();
                foreach (var u in commenters)
                {
                    db.PostComments.Add(new PostComment
                    {
                        PostId = post.Id,
                        UserId = u.Id,
                        Content = postComments[rnd.Next(postComments.Length)],
                        CreatedAt = post.CreatedAt.AddHours(rnd.Next(1, 60)),
                    });
                }
            }

            await db.SaveChangesAsync();
        }
    }
}
