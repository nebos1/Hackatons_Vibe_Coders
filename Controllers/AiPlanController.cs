using System.Globalization;
using System.Text;
using EventsApp.Common;
using EventsApp.Data;
using EventsApp.Models;
using EventsApp.Services.AI;
using EventsApp.ViewModels.Wrapped;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventsApp.Controllers
{
    public class AiPlanController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IAiSearchService _ai;

        public AiPlanController(ApplicationDbContext db, IAiSearchService ai)
        {
            _db = db;
            _ai = ai;
        }

        public async Task<IActionResult> Index(string? city, DateTime? when, string? vibe)
        {
            var cities = await _db.Events
                .AsNoTracking()
                .Where(e => e.IsApproved)
                .Select(e => e.City)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            var vm = new DayPlanViewModel
            {
                City = city,
                When = when?.Date ?? DateTime.UtcNow.Date,
                Vibe = vibe,
                Cities = cities,
            };

            if (string.IsNullOrWhiteSpace(city))
            {
                return View(vm);
            }

            var dayStart = vm.When;
            var dayEnd = dayStart.AddDays(1);
            var cityVariants = CityCoordinates.GetEquivalentNames(city);

            var candidates = await _db.Events
                .AsNoTracking()
                .Where(e => e.IsApproved
                    && cityVariants.Contains(e.City)
                    && e.StartTime >= dayStart
                    && e.StartTime < dayEnd)
                .OrderBy(e => e.StartTime)
                .Select(e => new DayPlanCandidate
                {
                    Id = e.Id,
                    Title = e.Title,
                    City = e.City,
                    Address = e.Address,
                    StartTime = e.StartTime,
                    EndTime = e.EndTime,
                    Genre = e.Genre,
                    ImageUrl = e.ImageUrl,
                })
                .ToListAsync();

            vm.Candidates = candidates;

            if (candidates.Count == 0)
            {
                vm.AiPlan = "Няма публикувани събития за този ден в избрания град. Опитай с друга дата или близък град.";
                return View(vm);
            }

            if (!_ai.IsEnabled)
            {
                vm.AiError = "AI асистентът не е конфигуриран — показваме само списъка със събития.";
                return View(vm);
            }

            var prompt = BuildPrompt(vm, candidates);
            try
            {
                var result = await _ai.GenerateTextAsync(prompt, "day-plan");
                if (string.IsNullOrWhiteSpace(result))
                {
                    vm.AiError = "AI не върна отговор — пробвай пак след малко.";
                }
                else
                {
                    vm.AiPlan = result;
                }
            }
            catch (Exception ex)
            {
                vm.AiError = "AI грешка: " + ex.Message;
            }

            return View(vm);
        }

        private static string BuildPrompt(DayPlanViewModel vm, IReadOnlyList<DayPlanCandidate> events)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Ти си AI компаньон на платформа за събития (Evento) в България.");
            sb.AppendLine("Задача: предложи кратък план за вечер/ден в избрания град. Използвай ТОЧНО събитията, които са дадени по-долу — НЕ измисляй нови.");
            sb.AppendLine("Формат на отговора:");
            sb.AppendLine("- Един въвеждащ ред (макс 1 изречение).");
            sb.AppendLine("- 3 секции с emoji и час: ☕ преди (опционално, дори без събитие), 🎵 основно (ВИНАГИ от списъка със събития, цитирай точното заглавие), 🍷 след (опционално).");
            sb.AppendLine("- Под основното събитие напиши: \"Защо: <1 кратко изречение>\".");
            sb.AppendLine("- Никакви markdown заглавия, никакви списъци с тирета. Чист текст с празни редове между секциите.");
            sb.AppendLine();
            sb.AppendLine($"Град: {vm.City}");
            sb.AppendLine($"Дата: {vm.When:dd.MM.yyyy} ({vm.When.ToString("dddd", new CultureInfo("bg-BG"))})");
            if (!string.IsNullOrWhiteSpace(vm.Vibe))
            {
                sb.AppendLine($"Настроение / контекст: {vm.Vibe}");
            }
            sb.AppendLine();
            sb.AppendLine("Налични събития за избрания ден:");
            foreach (var e in events.Take(20))
            {
                sb.AppendLine($"- [{e.Id}] {e.StartTime:HH:mm} — {e.Title} · {e.Genre.GetDisplayName()} · {e.Address ?? e.City}");
            }
            sb.AppendLine();
            sb.AppendLine("Ако имаш няколко добри основни събития, избери ЕДНО според настроението. Бъди топъл и конкретен. Изведи само плана.");
            return sb.ToString();
        }
    }
}
