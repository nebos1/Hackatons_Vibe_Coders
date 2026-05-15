using EventsApp.Common;
using EventsApp.Data;
using EventsApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EventsApp.Infrastructure
{
    /// <summary>
    /// Idempotent demo seeder — re-runs on every app start and adds any missing records.
    /// Safe to invoke multiple times: organizers/users are matched by email, events and posts
    /// by content + organizer, so previously-seeded rows are left untouched.
    /// </summary>
    public static class DemoDataSeeder
    {
        // Must satisfy the strictest Identity policy across all environments
        // (production: RequiredLength=10, uppercase, lowercase, digit, non-alphanumeric).
        private const string DemoPassword = "Demo123!Pass";

        // Images are served from picsum.photos with deterministic seeds so every record gets
        // a stable, unique, high-quality photo without local uploads.
        private static string Img(string seed, int w, int h) => $"https://picsum.photos/seed/{seed}/{w}/{h}";

        public static async Task SeedAsync(IServiceProvider services)
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DemoDataSeeder");

            var rnd = new Random(20260515);
            var now = DateTime.UtcNow;
            int orgCreated = 0, orgSkipped = 0, evtCreated = 0, evtSkipped = 0, postCreated = 0, postSkipped = 0;

            // ── 40 Organizers (idempotent) ────────────────────────────────────────
            var organizers = new[]
            {
                new OrgSpec("sofia-sound-collective", "Александър", "Петров", "Sofia Sound Collective", "София", EventGenre.Techno, "Колектив от диджеи и промоутъри за тъмни техно нощи в столицата.", "#1234d9"),
                new OrgSpec("beat-rooms-plovdiv", "Мира", "Стоянова", "Beat Rooms Plovdiv", "Пловдив", EventGenre.Jazz, "Камерни джаз и lounge вечери в сърцето на Капана.", "#7a3eb1"),
                new OrgSpec("sea-wave-events", "Никола", "Кирилов", "Sea Wave Events", "Бургас", EventGenre.Festival, "Летни фестивали и beach parties по Черноморието.", "#ff7849"),
                new OrgSpec("varna-bass-society", "Емилия", "Янкова", "Varna Bass Society", "Варна", EventGenre.DrumAndBass, "Bass култура на морския бряг — DnB, dubstep и UK garage.", "#0bb3a0"),
                new OrgSpec("ruse-vinyl-club", "Калоян", "Маринов", "Ruse Vinyl Club", "Русе", EventGenre.HipHop, "Hip-hop, soul и funk вечери само от винил.", "#c2185b"),
                new OrgSpec("burgas-beach-arena", "Татяна", "Илиева", "Burgas Beach Arena", "Бургас", EventGenre.House, "Open-air площадка на южния плаж — house, melodic, indie dance.", "#ffb300"),
                new OrgSpec("sofia-jazz-society", "Деян", "Костов", "Sofia Jazz Society", "София", EventGenre.Jazz, "Българската сцена за съвременен и класически джаз от 2009.", "#3949ab"),
                new OrgSpec("plovdiv-philharmonic", "Десислава", "Чавдарова", "Plovdiv Philharmonic", "Пловдив", EventGenre.Classical, "Симфоничен оркестър с концерти всеки месец на 5 сцени.", "#5d4037"),
                new OrgSpec("velin-folk-stage", "Велин", "Динев", "Velin Folk Stage", "Велико Търново", EventGenre.Folk, "Българска народна и балкан фолк сцена. Танци, песни, ракия.", "#558b2f"),
                new OrgSpec("stara-zagora-rock", "Кристиян", "Стоилов", "Stara Zagora Rock", "Стара Загора", EventGenre.Rock, "Класически рок и хард рок концерти на най-голямата открита сцена в Тракия.", "#37474f"),
                new OrgSpec("indie-circuit-bg", "Боряна", "Велинова", "Indie Circuit BG", "София", EventGenre.Indie, "Турнета на индийски и алтернативни групи. Малки клубове, голяма енергия.", "#7c4dff"),
                new OrgSpec("metal-forge-vt", "Радослав", "Тонев", "Metal Forge VT", "Велико Търново", EventGenre.Metal, "Метъл, хардкор и пост-метъл вечери. Здраво и шумно.", "#212121"),
                new OrgSpec("comedy-cellar-sofia", "Мила", "Йорданова", "Comedy Cellar Sofia", "София", EventGenre.Comedy, "Stand-up комедия 4 пъти седмично. Местни и международни комици.", "#d50000"),
                new OrgSpec("art-house-cinema", "Тодор", "Стефанов", "Art House Cinema", "София", EventGenre.Cinema, "Авторско, фестивално и късометражно кино. Прожекции + Q&A с режисьорите.", "#455a64"),
                new OrgSpec("varna-festival-hub", "Елица", "Камбурова", "Varna Festival Hub", "Варна", EventGenre.Festival, "Целогодишна фестивална програма — от джаз през world music до електронна.", "#00897b"),
                new OrgSpec("burgas-theater-co", "Светослав", "Драгнев", "Burgas Theater Co", "Бургас", EventGenre.Theater, "Авторски театрални продукции и пърформанси с акцент върху съвременна драматургия.", "#6a1b9a"),
                new OrgSpec("rnb-room-sofia", "Габриела", "Колева", "R&B Room Sofia", "София", EventGenre.Rnb, "R&B, neo-soul и slow jam вечери на ротация в три клуба.", "#ad1457"),
                new OrgSpec("punk-basement", "Иво", "Митев", "Punk Basement", "Пловдив", EventGenre.Punk, "DIY punk и hardcore сцена с 20+ концерта годишно.", "#bf360c"),
                new OrgSpec("alt-stage-vt", "Антония", "Балева", "Alt Stage VT", "Велико Търново", EventGenre.Alternative, "Алтернативен рок и shoegaze. Малки зали, силни емоции.", "#283593"),
                new OrgSpec("blues-bar-ruse", "Свежен", "Бориславов", "Blues Bar Ruse", "Русе", EventGenre.Blues, "Класически делта и Чикаго блус. Hammond, китари, харпa.", "#3e2723"),
                new OrgSpec("reggae-roots-bg", "Зорница", "Радева", "Reggae Roots BG", "Варна", EventGenre.Reggae, "Roots reggae и dub звукови системи в open-air сетинг.", "#33691e"),
                new OrgSpec("salsa-latino-sofia", "Хосе", "Иванов", "Salsa Latino Sofia", "София", EventGenre.Latino, "Salsa, bachata и latin вечери — уроци + парти. Bring your dancing shoes.", "#e64a19"),
                new OrgSpec("disco-fever-bg", "Аделина", "Симеонова", "Disco Fever BG", "Бургас", EventGenre.Disco, "70-те и 80-те disco класики на огледално кълбо.", "#f06292"),
                new OrgSpec("live-house-mtm", "Мартин", "Тошев", "Live House MTM", "Стара Загора", EventGenre.LiveMusic, "Малка зала с прецизен звук — седмични live концерти на BG групи.", "#0277bd"),
                new OrgSpec("opera-na-platno", "Камелия", "Доганова", "Опера На Платно", "София", EventGenre.Opera, "Прожекции на световни оперни постановки на голям екран с обяснения.", "#4527a0"),
                new OrgSpec("ballet-academy-bg", "Виктория", "Найденова", "Ballet Academy BG", "София", EventGenre.Ballet, "Класически и съвременен балет. Спектакли + open class за публиката.", "#ec407a"),
                new OrgSpec("food-vibes-plovdiv", "Стилиян", "Първанов", "Food Vibes Plovdiv", "Пловдив", EventGenre.FoodAndDrinks, "Wine tasting, gastro pop-up и chef takeover в Капана.", "#8d6e63"),
                new OrgSpec("street-art-sofia", "Цветелина", "Михова", "Street Art Sofia", "София", EventGenre.Art, "Изложби, графити обиколки и live painting в централна София.", "#ff5722"),
                new OrgSpec("network-pulse-bg", "Любомир", "Атанасов", "Network Pulse BG", "София", EventGenre.Networking, "Networking вечери за tech, media и creative industries.", "#1e88e5"),
                new OrgSpec("charity-runs-bg", "Райна", "Енева", "Charity Runs BG", "Варна", EventGenre.Charity, "Благотворителни концерти и runs в полза на детски домове.", "#43a047"),
                new OrgSpec("outdoor-rila", "Цветан", "Първанов", "Outdoor Rila", "Благоевград", EventGenre.Outdoor, "Хайкинг и open-air концерти в подножието на Рила.", "#2e7d32"),
                new OrgSpec("nightlife-collective", "Деница", "Стоянова", "Nightlife Collective", "София", EventGenre.Nightlife, "Promo агенция за късни нощи в София. Различен клуб всяка събота.", "#311b92"),
                new OrgSpec("gaming-arena-bg", "Васил", "Кръстев", "Gaming Arena BG", "Пловдив", EventGenre.Gaming, "Esports турнири и LAN party-та. CS, Valorant, FIFA.", "#01579b"),
                new OrgSpec("kids-fun-burgas", "Биляна", "Йорданова", "Kids Fun Burgas", "Бургас", EventGenre.Kids, "Детски театър, работилници и фестивали за деца от 3 до 12.", "#fbc02d"),
                new OrgSpec("conference-pulse", "Кристина", "Андонова", "Conference Pulse", "София", EventGenre.Conference, "Конференции за дизайн, продуктов мениджмънт и стартъп екосистема.", "#039be5"),
                new OrgSpec("workshop-lab-bg", "Йоана", "Цачева", "Workshop Lab BG", "Пловдив", EventGenre.Workshop, "Творчески уъркшопи — керамика, photography, screen printing.", "#fb8c00"),
                new OrgSpec("trap-house-sofia", "Самуил", "Костадинов", "Trap House Sofia", "София", EventGenre.Trap, "Trap, drill и phonk нощи. Млади BG изпълнители + USA headliners.", "#4a148c"),
                new OrgSpec("chalga-arena", "Маги", "Стефанова", "Chalga Arena", "Пловдив", EventGenre.Chalga, "Поп-фолк звезди на голяма сцена. Шоу, светлини, фойерверки.", "#e91e63"),
                new OrgSpec("exhibition-quadrat", "Боян", "Иванов", "Exhibition Quadrat", "Варна", EventGenre.Exhibition, "Съвременно изкуство и фотография. Месечни вернисажи с автори.", "#9e9d24"),
                new OrgSpec("sports-fan-zone", "Огнян", "Михайлов", "Sports Fan Zone", "София", EventGenre.Sports, "Live screenings на големи мачове + watching parties с фен клубовете.", "#c62828"),
            };

            var orgUsers = new List<ApplicationUser>(organizers.Length);
            var organizerProfilesByUserId = new Dictionary<string, OrganizerProfile>();

            foreach (var o in organizers)
            {
                var email = $"{o.Slug.Replace("-", ".")}@demo.bg";
                var username = o.Slug.Replace("-", ".");
                var avatar = Img($"org-{o.Slug}-avatar", 600, 600);
                var cover = Img($"org-{o.Slug}-cover", 1600, 600);

                var existing = await userManager.FindByEmailAsync(email);
                ApplicationUser user;

                if (existing != null)
                {
                    user = existing;
                    orgSkipped++;
                    // Update avatar/bio if the user predates the new image strategy.
                    if (string.IsNullOrEmpty(user.ProfileImageUrl) || !user.ProfileImageUrl.Contains("picsum.photos"))
                    {
                        user.ProfileImageUrl = avatar;
                        user.Bio = o.Bio;
                        await userManager.UpdateAsync(user);
                    }
                }
                else
                {
                    user = new ApplicationUser
                    {
                        UserName = username,
                        Email = email,
                        EmailConfirmed = true,
                        FirstName = o.First,
                        LastName = o.Last,
                        Bio = o.Bio,
                        ProfileImageUrl = avatar,
                        CreatedAt = now.AddMonths(-rnd.Next(3, 24)),
                    };
                    var res = await userManager.CreateAsync(user, DemoPassword);
                    if (!res.Succeeded)
                    {
                        var errs = string.Join("; ", res.Errors.Select(e => e.Code + ": " + e.Description));
                        logger.LogWarning("DemoSeeder: failed to create organizer {Email} — {Errors}", email, errs);
                        continue;
                    }
                    orgCreated++;
                }

                if (!await userManager.IsInRoleAsync(user, GlobalConstants.Roles.Organizer))
                {
                    await userManager.AddToRoleAsync(user, GlobalConstants.Roles.Organizer);
                }

                if (!await db.OrganizerData.AnyAsync(d => d.OrganizerId == user.Id))
                {
                    db.OrganizerData.Add(new OrganizerData
                    {
                        OrganizerId = user.Id,
                        OrganizationName = o.DisplayName,
                        Description = o.Bio,
                        PhoneNumber = $"+359 8{rnd.Next(7, 10)} {rnd.Next(100, 999)} {rnd.Next(1000, 9999)}",
                        Website = $"https://{o.Slug}.bg",
                        CompanyNumber = $"BG{rnd.Next(200000000, 299999999)}",
                        Approved = true,
                        CreatedAt = user.CreatedAt,
                    });
                }

                var profile = await db.OrganizerProfiles.FirstOrDefaultAsync(p => p.OwnerId == user.Id);
                if (profile == null)
                {
                    profile = new OrganizerProfile
                    {
                        OwnerId = user.Id,
                        DisplayName = o.DisplayName,
                        Tagline = o.Bio,
                        Description = o.Bio + " Следвай ни, за да не пропускаш нашите събития и предпродажби.",
                        AvatarImageUrl = avatar,
                        CoverImageUrl = cover,
                        City = o.City,
                        BrandColor = o.BrandColor,
                        Website = $"https://{o.Slug}.bg",
                        PhoneNumber = $"+359 8{rnd.Next(7, 10)} {rnd.Next(100, 999)} {rnd.Next(1000, 9999)}",
                        ContactEmail = email,
                        InstagramUrl = $"https://instagram.com/{o.Slug.Replace("-", "")}",
                        FacebookUrl = $"https://facebook.com/{o.Slug.Replace("-", "")}",
                        IsDefault = true,
                        IsActive = true,
                        IsApproved = true,
                        CreatedAt = user.CreatedAt,
                    };
                    db.OrganizerProfiles.Add(profile);
                }
                else if (string.IsNullOrEmpty(profile.CoverImageUrl) || string.IsNullOrEmpty(profile.AvatarImageUrl))
                {
                    profile.AvatarImageUrl = avatar;
                    profile.CoverImageUrl = cover;
                    profile.BrandColor ??= o.BrandColor;
                    profile.City ??= o.City;
                    db.OrganizerProfiles.Update(profile);
                }

                organizerProfilesByUserId[user.Id] = profile;
                orgUsers.Add(user);
            }
            await db.SaveChangesAsync();

            // ── Regular users (audience) ──────────────────────────────────────────
            var users = new[]
            {
                new UserSpec("ivan-dimitrov", "Иван", "Димитров", "Меломан, DJ от време на време."),
                new UserSpec("petya-koleva", "Петя", "Колева", "Обичам джаз и виновни вечери в Капана."),
                new UserSpec("georgi-todorov", "Георги", "Тодоров", "Живея за летните фестивали по морето."),
                new UserSpec("maria-vasileva", "Мария", "Василева", "Хроникьор на нощния живот в София."),
                new UserSpec("stefan-raykov", "Стефан", "Райков", "Пътувам за концерти. Бира + рок > всичко."),
                new UserSpec("yana-stoeva", "Яна", "Стоева", "Балет, опера, кино. Влюбена в Бургас."),
                new UserSpec("emil-popov", "Емил", "Попов", "Подкаствам за музика. Винаги съм по митингите."),
                new UserSpec("denitsa-mihaylova", "Деница", "Михайлова", "House + морето = моят летен план."),
            };
            var regularUsers = new List<ApplicationUser>();
            foreach (var u in users)
            {
                var email = $"{u.Slug.Replace("-", ".")}@demo.bg";
                var existing = await userManager.FindByEmailAsync(email);
                if (existing != null)
                {
                    regularUsers.Add(existing);
                    continue;
                }
                var au = new ApplicationUser
                {
                    UserName = u.Slug.Replace("-", "."),
                    Email = email,
                    EmailConfirmed = true,
                    FirstName = u.First,
                    LastName = u.Last,
                    Bio = u.Bio,
                    ProfileImageUrl = Img($"usr-{u.Slug}", 400, 400),
                    CreatedAt = now.AddMonths(-rnd.Next(2, 18)),
                };
                var res = await userManager.CreateAsync(au, DemoPassword);
                if (!res.Succeeded)
                {
                    var errs = string.Join("; ", res.Errors.Select(e => e.Code + ": " + e.Description));
                    logger.LogWarning("DemoSeeder: failed to create user {Email} — {Errors}", email, errs);
                    continue;
                }
                await userManager.AddToRoleAsync(au, GlobalConstants.Roles.User);
                regularUsers.Add(au);
            }
            await db.SaveChangesAsync();

            // If we have zero regular users (extremely unlikely), nothing to attribute likes/comments to.
            if (regularUsers.Count == 0) return;

            // ── 40 Events (idempotent — match by Title + OrganizerId) ────────────
            var eventSpecs = new[]
            {
                new EvtSpec(0, "underground-pulse-12", "Underground Pulse Vol. 12", "Дванадесетата нощ от Underground Pulse събира двама международни хедлайнера и трима локални любимци за безкомпромисно техно сет от 22:00 до 06:00.", "София", "Mixtape 5, ул. Шейново 7", 42.6957, 23.3338, 14, 8, true),
                new EvtSpec(1, "jazz-sessions-vol-7", "Jazz Sessions Vol. 7", "Седмо издание на джаз вечерите в Капана. Българско трио + специален китарист от Гърция в интимна камерна обстановка.", "Пловдив", "Капана, ул. Абаджийска 8", 42.1505, 24.7505, 9, 4, true),
                new EvtSpec(2, "sunwaves-burgas-beach", "Sunwaves x Burgas Beach", "Двудневен open-air фестивал на плажа с 12 диджеи на две сцени. Парти от залез до изгрев.", "Бургас", "Морска градина, северен плаж", 42.4912, 27.4805, 35, 36, true),
                new EvtSpec(3, "bass-night-varna", "Bass Night Varna", "DnB и dubstep вечер с три headlinera от UK сцената. Soundsystem на 12kW.", "Варна", "Club Exit, ул. Дунав 5", 43.2050, 27.9148, 12, 7, true),
                new EvtSpec(4, "vinyl-hip-hop-90s", "Vinyl Only: 90s Hip Hop Night", "Само от винил. Класиките на 90-те с двама диджеи, които си носят кутиите. Ограничен капацитет — 120 души.", "Русе", "Bar Strand, ул. Александровска 50", 43.8483, 25.9536, 7, 5, true),
                new EvtSpec(5, "beach-house-open-air", "Beach House Open Air", "Деветчасов open-air сет на южния плаж. Melodic, deep и indie dance от изгрев до залез.", "Бургас", "Южен плаж, секция B", 42.4861, 27.4747, 28, 9, true),
                new EvtSpec(6, "blue-monk-evening", "Blue Monk Evening", "Класически джаз стандарти изпълнени от квинтет с фокус върху Monk и Coltrane.", "София", "One More Bar, ул. Цар Шишман 12", 42.6912, 23.3270, 21, 3, true),
                new EvtSpec(7, "tchaikovsky-piano-night", "Чайковски: Пиано Концерти", "Цялостно изпълнение на първи и втори пиано концерт от Плодивската филхармония.", "Пловдив", "Античен театър, Стария град", 42.1466, 24.7510, 30, 2, true),
                new EvtSpec(8, "folk-fest-vt", "Балкан Фолк Фест", "Хорà, ръченици и балкан звуци. Гостуващи състави от Сърбия, Македония и Гърция.", "Велико Търново", "Площад Цар Асен I", 43.0758, 25.6172, 18, 6, false),
                new EvtSpec(9, "rock-revival-sz", "Rock Revival Stara Zagora", "Две местни рок групи + headliner от Германия. Класически хард рок без компромис.", "Стара Загора", "Парк Аязмо, открита сцена", 42.4253, 25.6342, 24, 4, true),
                new EvtSpec(10, "indie-circuit-spring", "Indie Circuit: Spring Edition", "Турне на три indie групи в малка зала. Близък контакт, скъсани китари.", "София", "Live & Loud, ул. Раковски 99", 42.6932, 23.3252, 11, 4, true),
                new EvtSpec(11, "metal-forge-night", "Metal Forge Night", "Български метъл бенд + хедлайнер от Финландия. Брутално и точно.", "Велико Търново", "Hall Metropolis, ул. Никола Габровски 78", 43.0843, 25.6311, 16, 5, true),
                new EvtSpec(12, "stand-up-thursday", "Stand-up Thursday", "Седмична stand-up вечер с 6 комици. От открит микрофон до утвърдени имена.", "София", "Comedy Cellar, ул. Аксаков 4", 42.6925, 23.3289, 3, 3, true),
                new EvtSpec(13, "kino-noir-screening", "Кино Ноар: Прожекция и разговор", "Прожекция на нова BG авторска лента + Q&A с режисьорите.", "София", "Дом на Киното, ул. Екзарх Йосиф 37", 42.6995, 23.3268, 5, 3, true),
                new EvtSpec(14, "varna-jazz-festival", "Варна Jazz Festival", "Триdневен джаз фестивал с международни хедлайнери и BG състави. 3 сцени, 18 концерта.", "Варна", "Двореца на Културата и Спорта", 43.2014, 27.9156, 42, 72, true),
                new EvtSpec(15, "theatre-monologue-night", "Театър: Вечер на Монолога", "Пет монолога от млади BG автори, изпълнени от утвърдени актьори.", "Бургас", "Драматичен Театър Адриана Будевска", 42.4944, 27.4664, 13, 3, true),
                new EvtSpec(16, "rnb-room-after-hours", "R&B Room After Hours", "Слоу джем вечер с три DJ-я на ротация. Neo-soul, R&B и retro funk.", "София", "Club 31, ул. Цар Симеон 31", 42.7039, 23.3299, 6, 5, true),
                new EvtSpec(17, "punk-basement-show", "Punk Basement Show #34", "Четири punk и hardcore групи на една сцена. DIY, без bullshit.", "Пловдив", "Petnoto, ул. Янко Сакъзов 12", 42.1450, 24.7460, 19, 4, false),
                new EvtSpec(18, "shoegaze-mountain-stage", "Shoegaze Mountain Stage", "Алтернативен рок и shoegaze. Български и сръбски групи в малка планинска зала.", "Велико Търново", "Bohemian Bar, ул. Дунав 24", 43.0820, 25.6280, 26, 5, true),
                new EvtSpec(19, "blues-bar-jam", "Blues Bar Jam Session", "Открита блус джам сесия — донеси инструмент или просто слушай. Hammond + guitars + harp.", "Русе", "Blues Bar, ул. Княжеска 13", 43.8500, 25.9500, 8, 4, false),
                new EvtSpec(20, "roots-reggae-sundown", "Roots Reggae Sundown", "Roots reggae вечер с dub звукова система. Open-air на залез.", "Варна", "Морски Парк, площадка Север", 43.2099, 27.9251, 22, 6, false),
                new EvtSpec(21, "salsa-night-class-party", "Salsa Night: Class + Party", "Час урок по салса (всички нива) + 4 часа парти. Bachata и cumbia също.", "София", "Studio Latino, ул. Костенски Водопад 4", 42.6612, 23.3128, 4, 5, true),
                new EvtSpec(22, "disco-fever-80s", "Disco Fever: 80s Edition", "Класиките на 80-те под огледално кълбо. Дрес код: блясък.", "Бургас", "Coconut Club, ул. Лермонтов 8", 42.4960, 27.4710, 17, 6, true),
                new EvtSpec(23, "live-house-tribute-night", "Live House: Tribute Night", "Tribute на Pink Floyd от BG cover банд с пълен светлинен сетап.", "Стара Загора", "Live House MTM, ул. Парчевич 12", 42.4270, 25.6310, 31, 3, true),
                new EvtSpec(24, "opera-cinema-aida", "Опера На Платно: Аида", "HD прожекция на Aida от La Scala с подробни обяснения преди представлението.", "София", "Дом на Киното, голяма зала", 42.6995, 23.3268, 20, 3, true),
                new EvtSpec(25, "ballet-swan-lake-light", "Лебедово Езеро: Лек Вариант", "Съкратена 90-минутна версия на Лебедово езеро с пълен оркестър.", "София", "Sofia Opera, бул. Дондуков 30", 42.6975, 23.3354, 36, 2, true),
                new EvtSpec(26, "wine-tasting-balkan", "Балкан Wine Tasting", "Тематична дегустация на 12 балкански вина с сомелиер.", "Пловдив", "Капана Wine Cellar, ул. Кирил Нектариев 15", 42.1500, 24.7510, 10, 3, true),
                new EvtSpec(27, "street-art-tour-sofia", "Street Art Tour София", "3-часова обиколка с гид + live painting в края на маршрута.", "София", "Старт: пл. Народно Събрание", 42.6940, 23.3340, 5, 3, false),
                new EvtSpec(28, "tech-networking-mixer", "Tech Networking Mixer", "Networking вечер за tech, product и creative industries. Light bites + free drinks.", "София", "Betahaus, ул. Крум Попов 56-58", 42.6727, 23.3068, 7, 3, false),
                new EvtSpec(29, "charity-run-varna", "Charity Run Варна 10K", "Благотворителен 10K за подкрепа на детски дом. Стартови такси отиват изцяло за каузата.", "Варна", "Морска градина, главна алея", 43.2050, 27.9170, 25, 3, false),
                new EvtSpec(30, "rila-sunset-hike", "Залез на Мусала: хайкинг + live music", "Хайкинг до връх Мусала с акустичен концерт на върха при залез.", "Благоевград", "Старт: лифт Боровец", 42.2685, 23.6018, 33, 10, false),
                new EvtSpec(31, "nightlife-collective-sat", "Nightlife Collective: Saturday Resident", "Седмично резидентско парти с три ротиращи се клуба. Тема: melodic techno.", "София", "Different location each week", 42.6975, 23.3242, 6, 6, true),
                new EvtSpec(32, "esports-cs-tournament", "Esports CS2 Открит Турнир", "Open Counter-Strike 2 turnir с парично разпределение от 5000 лв.", "Пловдив", "Gaming Arena, мол Гранд", 42.1390, 24.7430, 14, 10, true),
                new EvtSpec(33, "kids-theatre-saturday", "Детски театър: Седмична Събота", "Театрална постановка за деца от 5 до 11 г. с интерактивни моменти.", "Бургас", "Малка зала, ОКИ Морско Казино", 42.4945, 27.4720, 9, 2, true),
                new EvtSpec(34, "design-conference-2026", "Design Conference 2026", "Едноdневна конференция за продуктов и UX дизайн. 10 говорители + 3 workshop-а.", "София", "Sofia Tech Park, ул. Цариградско шосе 111", 42.6688, 23.4220, 45, 9, true),
                new EvtSpec(35, "ceramics-weekend-workshop", "Керамика: Уикенд Workshop", "Двуdневен уъркшоп за ръчно изработване на чаши и купи. Включва всички материали.", "Пловдив", "Workshop Lab, ул. Митрополит Паисий 18", 42.1465, 24.7470, 23, 16, true),
                new EvtSpec(36, "trap-night-sofia", "Trap Night Sofia", "Trap + drill вечер с BG MC-и и DJ от ATL. Hype, smoke, energy.", "София", "Mixtape 5", 42.6957, 23.3338, 15, 6, true),
                new EvtSpec(37, "chalga-arena-summer", "Chalga Arena Summer Edition", "Поп-фолк звезди с пълно шоу — танци, светлини, fireworks.", "Пловдив", "Откр. сцена Чаталджа", 42.1310, 24.7510, 38, 5, true),
                new EvtSpec(38, "exhibition-vernisage", "Vernissage: Светлина и Сянка", "Откриване на изложба от съвременен BG фотограф. Гостуващ DJ + wine.", "Варна", "Художествена Галерия Варна", 43.2070, 27.9120, 11, 4, false),
                new EvtSpec(39, "champions-league-fanzone", "Champions League Watching Party", "Live screening на финала на UEFA Champions League с BBQ, бира и фен сектор.", "София", "Fan Zone, пл. Александър Невски", 42.6963, 23.3328, 20, 4, false),
            };

            var createdEvents = new List<Event>();
            var newlyCreatedEventIds = new HashSet<int>();
            for (int i = 0; i < eventSpecs.Length; i++)
            {
                var e = eventSpecs[i];
                if (e.OrgIdx >= orgUsers.Count) continue;
                var org = orgUsers[e.OrgIdx];

                var existing = await db.Events.FirstOrDefaultAsync(x => x.Title == e.Title && x.OrganizerId == org.Id);
                if (existing != null)
                {
                    if (string.IsNullOrEmpty(existing.ImageUrl) || !existing.ImageUrl.Contains("picsum.photos"))
                    {
                        existing.ImageUrl = Img($"evt-{e.Slug}", 1200, 720);
                        db.Events.Update(existing);
                    }
                    createdEvents.Add(existing);
                    evtSkipped++;
                    continue;
                }
                evtCreated++;

                var start = now.AddDays(e.StartOffsetDays).AddHours(rnd.Next(0, 6) + 18);
                var ev = new Event
                {
                    OrganizerId = org.Id,
                    Title = e.Title,
                    Description = e.Description,
                    City = e.City,
                    Address = e.Address,
                    Latitude = e.Lat,
                    Longitude = e.Lng,
                    Genre = organizers[e.OrgIdx].Genre,
                    ImageUrl = Img($"evt-{e.Slug}", 1200, 720),
                    StartTime = start,
                    EndTime = start.AddHours(e.DurationHours),
                    OrganizerProfileId = organizerProfilesByUserId.TryGetValue(org.Id, out var pf) ? pf.Id : null,
                    IsApproved = true,
                    CreatedAt = now.AddDays(-Math.Abs(e.StartOffsetDays) - 5),
                };
                db.Events.Add(ev);
                createdEvents.Add(ev);
            }
            await db.SaveChangesAsync();

            // Mark which event ids are newly created (so we skip duplicating their tickets/likes)
            for (int i = 0; i < createdEvents.Count; i++)
            {
                if (createdEvents[i].Id != 0 && createdEvents[i].CreatedAt > now.AddMinutes(-2))
                {
                    // No reliable way after SaveChanges to tell "new" from "existing" — instead,
                    // we'll detect by checking if the event has any tickets/likes already.
                }
            }

            // ── Tickets (only for events without any tickets yet) ────────────────
            var ticketsByEvent = new Dictionary<int, List<Ticket>>();
            for (int i = 0; i < eventSpecs.Length; i++)
            {
                if (i >= createdEvents.Count) continue;
                if (!eventSpecs[i].HasTickets) continue;
                var ev = createdEvents[i];

                var existingTicketCount = await db.Tickets.CountAsync(t => t.EventId == ev.Id);
                if (existingTicketCount > 0)
                {
                    var existingList = await db.Tickets.Where(t => t.EventId == ev.Id).ToListAsync();
                    ticketsByEvent[ev.Id] = existingList;
                    continue;
                }

                var genre = ev.Genre;
                var ticketSet = genre switch
                {
                    EventGenre.Festival => new[]
                    {
                        ("Earlybird Pass", "Двудневен пропуск, ограничено количество.", 89m, 100, 42),
                        ("Standard Pass", "Двудневен пропуск.", 129m, 400, 215),
                        ("VIP Pass", "VIP зона, бар, паркинг.", 249m, 60, 18),
                    },
                    EventGenre.Classical or EventGenre.Opera or EventGenre.Ballet => new[]
                    {
                        ("Партер", "Седящи места, партер.", 45m, 120, 56),
                        ("Балкон", "Седящи места, балкон.", 30m, 80, 31),
                        ("Премиум ложа", "VIP ложа за 4 души.", 180m, 8, 3),
                    },
                    EventGenre.Jazz or EventGenre.Blues or EventGenre.LiveMusic => new[]
                    {
                        ("Стандартен билет", "Свободни места.", 25m, 80, 47),
                        ("VIP маса", "Маса до сцената за 4 души.", 120m, 10, 6),
                    },
                    EventGenre.HipHop or EventGenre.Trap or EventGenre.Rnb => new[]
                    {
                        ("Ранна предпродажба", "Ограничено до 50 бр.", 20m, 50, 31),
                        ("Стандартен билет", "Стандартен вход.", 30m, 200, 124),
                    },
                    EventGenre.Techno or EventGenre.House or EventGenre.DrumAndBass or EventGenre.Nightlife => new[]
                    {
                        ("Earlybird", "Първите 100.", 18m, 100, 88),
                        ("Стандарт", "Стандартна цена.", 28m, 400, 245),
                        ("VIP", "VIP зона, бар.", 60m, 50, 18),
                    },
                    EventGenre.Theater or EventGenre.Comedy or EventGenre.Cinema => new[]
                    {
                        ("Партер", "Седящи места партер.", 18m, 100, 65),
                        ("Премиум", "Първите редове.", 28m, 40, 22),
                    },
                    EventGenre.Workshop or EventGenre.Conference => new[]
                    {
                        ("Standard Pass", "Целодневен достъп + материали.", 95m, 150, 88),
                        ("Pro Pass", "Standard + networking dinner.", 185m, 40, 19),
                    },
                    EventGenre.Gaming or EventGenre.Sports => new[]
                    {
                        ("Spectator", "Зрителски вход.", 12m, 300, 180),
                        ("VIP / Player Pass", "Достъп до играчите + катиринг.", 45m, 80, 38),
                    },
                    _ => new[]
                    {
                        ("Standard", "Стандартен вход.", 22m, 250, 124),
                        ("VIP", "VIP зона.", 45m, 50, 18),
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
                newlyCreatedEventIds.Add(ev.Id);
            }
            await db.SaveChangesAsync();

            // ── User purchases (only for newly created events) ───────────────────
            foreach (var (evId, ticketList) in ticketsByEvent)
            {
                if (!newlyCreatedEventIds.Contains(evId)) continue;

                foreach (var tk in ticketList)
                {
                    var soldCount = Math.Min(tk.QuantityTotal - tk.QuantityRemaining, 20);
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
                            ut.UsedByOrganizerId = createdEvents.First(e => e.Id == evId).OrganizerId;
                        }
                        db.UserTickets.Add(ut);
                    }
                }
            }
            await db.SaveChangesAsync();

            // ── 20 Posts (idempotent — match by Content + OrganizerId) ────────────
            var postSpecs = new[]
            {
                new PostSpec(0, "Билетите за Underground Pulse Vol. 12 излязоха. Ranged предпродажба до петък — после се вдига цената.", 0, 1),
                new PostSpec(1, "Снимки от последното Jazz Sessions. Благодарим на всички, които ни подкрепиха.", 1, 2),
                new PostSpec(2, "Sunwaves x Burgas: разкриваме третия headliner следващия вторник. Подсказка: Берлин.", 2, 1),
                new PostSpec(3, "Bass Night Varna — sound check мина перфектно. До утре в 22:00.", 3, 1),
                new PostSpec(4, "Vinyl Only #34: цялата плейлиста ще е от записи преди 2000. Кутиите вече се пакетират.", 4, 1),
                new PostSpec(5, "Beach House Open Air — последни 30 билета. Линк в bio.", 5, 2),
                new PostSpec(6, "Blue Monk Evening: камерна обстановка, 60 места. Резервации през сайта.", 6, 1),
                new PostSpec(7, "Чайковски на Античния театър — пианистът пристигна вчера. Репетиция от 16:00.", 7, 1),
                new PostSpec(8, "Балкан Фолк Фест: пълна програма + сцени. 12 хор̀a, 4 държави.", 8, 2),
                new PostSpec(9, "Stand-up Thursday — тази седмица отворен микрофон. Запиши се през формата.", 12, 1),
                new PostSpec(10, "Кино Ноар: новата BG лента която НЕ видяхте на фестивала. Q&A с режисьорите.", 13, 1),
                new PostSpec(11, "Варна Jazz Festival — пълна програма онлайн. Trio билети с -20%.", 14, 2),
                new PostSpec(12, "Театър: 5 моноложа които ще ти разбият деня. От четвъртък.", 15, 1),
                new PostSpec(13, "R&B Room After Hours — резидентският DJ пуска ново EP в края на нощта.", 16, 1),
                new PostSpec(14, "Tech Networking Mixer — над 200 регистрации. Идваме с print badges.", 28, 1),
                new PostSpec(15, "Charity Run Варна 10K — стартова такса 100% за дома в Тополи. Регистрирай се.", 29, 1),
                new PostSpec(16, "Залез на Мусала: трекинг + акустичен сет. 6 часа нагоре, 3 часа надолу, 1 час концерт.", 30, 2),
                new PostSpec(17, "Design Conference 2026 — пълен lineup. 10 говорители, 3 уъркшопа, едно афтерпарти.", 34, 1),
                new PostSpec(18, "Trap Night Sofia — ATL DJ пристига довечера. Last call за table booking.", 36, 1),
                new PostSpec(19, "Vernissage: Светлина и Сянка — преглед на нашата вечер в галерията. Над 250 души минаха през изложбата.", 38, 2),
            };

            var newlyCreatedPostIds = new HashSet<int>();
            var createdPosts = new List<Post>();
            for (int i = 0; i < postSpecs.Length; i++)
            {
                var p = postSpecs[i];
                if (p.OrgIdx >= orgUsers.Count) continue;
                var org = orgUsers[p.OrgIdx];

                var existing = await db.Posts.FirstOrDefaultAsync(x => x.OrganizerId == org.Id && x.Content == p.Content);
                if (existing != null)
                {
                    createdPosts.Add(existing);
                    postSkipped++;
                    continue;
                }
                postCreated++;

                var post = new Post
                {
                    OrganizerId = org.Id,
                    OrganizerProfileId = organizerProfilesByUserId.TryGetValue(org.Id, out var profile) ? profile.Id : null,
                    EventId = p.EventIdx.HasValue && p.EventIdx.Value < createdEvents.Count ? createdEvents[p.EventIdx.Value].Id : (int?)null,
                    Content = p.Content,
                    CreatedAt = now.AddDays(-rnd.Next(1, 30)).AddHours(-rnd.Next(0, 23)),
                };
                db.Posts.Add(post);
                createdPosts.Add(post);
            }
            await db.SaveChangesAsync();

            for (int i = 0; i < postSpecs.Length; i++)
            {
                if (i >= createdPosts.Count) continue;
                var post = createdPosts[i];
                var spec = postSpecs[i];

                var hasImages = await db.PostImages.AnyAsync(pi => pi.PostId == post.Id);
                if (hasImages) continue;

                for (int img = 0; img < spec.ImageCount; img++)
                {
                    db.PostImages.Add(new PostImage
                    {
                        PostId = post.Id,
                        ImageUrl = Img($"post-{i + 1}-{img + 1}", 1000, 640),
                        MediaType = PostMediaType.Image,
                    });
                }
                newlyCreatedPostIds.Add(post.Id);
            }
            await db.SaveChangesAsync();

            // ── Likes & comments on events (only for events with zero likes so far) ──
            var eventComments = new[]
            {
                "Идвам! Кой иска да се събере преди?",
                "Last edition was insane.",
                "Има ли паркинг наблизо?",
                "Това е точно това, което ми трябваше за уикенда.",
                "Препоръчвам! Бях преди и звукът беше топ.",
                "Ще има ли late check-in?",
                "Можем ли да ползваме картата за плащане на бара?",
                "Колко време продължава първият сет?",
                "Първи път съм, какъв е дрес кодът?",
                "Кога излизат VIP билетите?",
            };

            foreach (var ev in createdEvents)
            {
                var hasLikes = await db.EventLikes.AnyAsync(l => l.EventId == ev.Id);
                if (hasLikes) continue;

                var likers = regularUsers.OrderBy(_ => rnd.Next()).Take(rnd.Next(2, regularUsers.Count + 1)).ToList();
                foreach (var u in likers)
                {
                    db.EventLikes.Add(new EventLike { EventId = ev.Id, UserId = u.Id, CreatedAt = now.AddDays(-rnd.Next(1, 30)) });
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

            // ── Likes & comments on posts (only for newly created posts) ─────────
            var postComments = new[]
            {
                "Идваме с приятели.",
                "Звучи страхотно, чакаме повече инфо.",
                "Кой ще е headliner-ът?",
                "Бях на миналата вечер — топ беше.",
                "Можем ли да си купим билет на входа?",
                "Кога има отстъпка за студенти?",
                "Любим организатор.",
                "Първи път ще идвам, какъв е дрес кодът?",
            };

            foreach (var post in createdPosts)
            {
                var hasLikes = await db.PostLikes.AnyAsync(l => l.PostId == post.Id);
                if (hasLikes) continue;

                var likers = regularUsers.OrderBy(_ => rnd.Next()).Take(rnd.Next(1, regularUsers.Count + 1)).ToList();
                foreach (var u in likers)
                {
                    db.PostLikes.Add(new PostLike { PostId = post.Id, UserId = u.Id, CreatedAt = now.AddDays(-rnd.Next(1, 20)) });
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

            logger.LogInformation(
                "DemoSeeder summary: organizers {OrgCreated} created / {OrgSkipped} already existed; events {EvtCreated}/{EvtSkipped}; posts {PostCreated}/{PostSkipped}; regular users {Users}.",
                orgCreated, orgSkipped, evtCreated, evtSkipped, postCreated, postSkipped, regularUsers.Count);
        }

        // ── Local spec records ───────────────────────────────────────────────────
        private sealed record OrgSpec(
            string Slug,
            string First,
            string Last,
            string DisplayName,
            string City,
            EventGenre Genre,
            string Bio,
            string BrandColor);

        private sealed record UserSpec(string Slug, string First, string Last, string Bio);

        private sealed record EvtSpec(
            int OrgIdx,
            string Slug,
            string Title,
            string Description,
            string City,
            string Address,
            double Lat,
            double Lng,
            int StartOffsetDays,
            int DurationHours,
            bool HasTickets);

        private sealed record PostSpec(int OrgIdx, string Content, int? EventIdx, int ImageCount);
    }
}
