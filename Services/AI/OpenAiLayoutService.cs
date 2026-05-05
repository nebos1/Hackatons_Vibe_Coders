using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EventsApp.Models;
using EventsApp.ViewModels.Layouts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace EventsApp.Services.AI
{
    public class OpenAiLayoutService : ILayoutAiService
    {
        private const long MaxImageBytes = 5L * 1024 * 1024;

        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() },
        };

        private const string SystemPrompt =
@"You are an expert venue seating and event layout designer for Evento.
Create a practical, editable venue layout JSON for an organizer.
Return ONLY valid JSON. No markdown. No comments.

Output schema:
{
  ""canvasWidth"": 1200,
  ""canvasHeight"": 820,
  ""floors"": [{ ""clientId"": ""floor-1"", ""name"": ""Партер"" }],
  ""sections"": [
    {
      ""clientId"": ""section-1"",
      ""floorId"": ""floor-1"",
      ""floorName"": ""Партер"",
      ""name"": ""Основна секция"",
      ""type"": ""Seated"",
      ""shape"": ""Rounded"",
      ""capacity"": 120,
      ""priceModifier"": 0,
      ""x"": 80,
      ""y"": 90,
      ""width"": 700,
      ""height"": 420,
      ""rotation"": 0,
      ""seats"": [
        {
          ""clientId"": ""seat-1"",
          ""row"": ""A"",
          ""number"": ""1"",
          ""label"": ""A1"",
          ""x"": 40,
          ""y"": 70,
          ""radius"": 15,
          ""rotation"": 0,
          ""capacity"": 1,
          ""isCapacityUnlimited"": false,
          ""seatType"": ""Standard"",
          ""status"": ""Active""
        }
      ]
    }
  ]
}

Allowed enum values:
- section type: Seated, Standing, VIP, Table
- section shape: Rectangle, Rounded, Circle, Stage
- seatType: Standard, Accessible, VIP, Table
- seat status: Active, Blocked

Rules:
- Use Bulgarian names for floors, sections and tables.
- Keep coordinates inside the 1200x820 canvas.
- Put the stage near the top when the venue is performance oriented.
- Seated sections should contain individual seat objects.
- Standing zones can have capacity and no seats.
- Tables are selectable bookable objects: represent each table as one seat with seatType Table.
- Table capacity is the group size for one purchase. If the user asks for unlimited/no limit, set isCapacityUnlimited true, but the table is still one bookable object.
- If the image is available, infer a sensible approximation from it. If uncertain, use the written description.
- Avoid generating more than 650 seat/table objects total.";

        private readonly HttpClient _http;
        private readonly AiOptions _opts;
        private readonly ILogger<OpenAiLayoutService> _logger;

        public OpenAiLayoutService(HttpClient http, IOptions<AiOptions> opts, ILogger<OpenAiLayoutService> logger)
        {
            _http = http;
            _opts = opts.Value;
            _logger = logger;

            if (_opts.IsConfigured)
            {
                _http.BaseAddress = new Uri("https://api.openai.com/v1/");
                _http.DefaultRequestHeaders.Remove("Authorization");
                _http.DefaultRequestHeaders.Add("Authorization", "Bearer " + _opts.ApiKey);
                _http.Timeout = TimeSpan.FromSeconds(Math.Max(60, _opts.TimeoutSeconds));
            }
        }

        public bool IsEnabled => _opts.IsConfigured;

        public string? LastError { get; private set; }

        public async Task<VenueLayoutJsonModel?> GenerateLayoutAsync(
            string? description,
            IFormFile? image,
            CancellationToken cancellationToken = default)
        {
            LastError = null;

            if (!IsEnabled)
            {
                LastError = "OPENAI_API_KEY липсва или е празен.";
                return null;
            }

            var prompt = BuildPrompt(description, image);
            var userParts = new List<object>
            {
                new { type = "text", text = prompt },
            };

            var imageDataUrl = await TryBuildImageDataUrlAsync(image, cancellationToken);
            if (imageDataUrl != null)
            {
                userParts.Add(new
                {
                    type = "image_url",
                    image_url = new { url = imageDataUrl, detail = "low" },
                });
            }

            var payload = new
            {
                model = _opts.ModelName,
                messages = new object[]
                {
                    new { role = "system", content = SystemPrompt },
                    new { role = "user", content = userParts },
                },
                temperature = 0.2,
                max_tokens = 6000,
                response_format = new { type = "json_object" },
            };

            try
            {
                using var content = new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json");
                using var response = await _http.PostAsync("chat/completions", content, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    LastError = $"OpenAI върна HTTP {(int)response.StatusCode}.";
                    _logger.LogWarning("OpenAI layout generation failed {Status}: {Body}", (int)response.StatusCode, Truncate(body, 500));
                    return null;
                }

                using var doc = JsonDocument.Parse(body);
                var text = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                var layout = ParseLayout(text);
                if (layout == null)
                {
                    LastError = "OpenAI върна отговор, но не беше валидна layout схема.";
                    _logger.LogWarning("OpenAI returned an invalid layout payload: {Payload}", Truncate(text ?? string.Empty, 500));
                    return null;
                }

                return NormalizeLayout(layout);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                LastError = $"OpenAI заявката отне повече от {_http.Timeout.TotalSeconds:0} секунди.";
                _logger.LogWarning("OpenAI layout generation timed out after {Seconds}s", _http.Timeout.TotalSeconds);
                return null;
            }
            catch (Exception ex)
            {
                LastError = "OpenAI заявката не успя: " + ex.Message;
                _logger.LogWarning(ex, "OpenAI layout generation failed");
                return null;
            }
        }

        private static string BuildPrompt(string? description, IFormFile? image)
        {
            var text = string.IsNullOrWhiteSpace(description)
                ? "Няма текстово описание. Използвай качената снимка и направи чиста начална схема."
                : description.Trim();

            return
$@"Organizer description:
{text}

Image attached: {(image?.Length > 0 ? "yes" : "no")}

Create an editable MVP layout. Prefer clarity over perfect precision:
- Use multiple floors if the description mentions етаж, балкон, balcony, level, floor.
- Use table objects for маси/tables/booths.
- Use standing sections for правостоящи/dancefloor/standing.
- Use VIP sections or VIP tables when mentioned.
- If exact counts are missing, infer reasonable counts from the venue type.";
        }

        private static async Task<string?> TryBuildImageDataUrlAsync(IFormFile? image, CancellationToken cancellationToken)
        {
            if (image == null || image.Length <= 0)
            {
                return null;
            }

            if (image.Length > MaxImageBytes)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(image.ContentType) ||
                !image.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            await using var stream = image.OpenReadStream();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, cancellationToken);
            return $"data:{image.ContentType};base64,{Convert.ToBase64String(ms.ToArray())}";
        }

        private static VenueLayoutJsonModel? ParseLayout(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var json = raw.Trim();
            if (json.StartsWith("```", StringComparison.Ordinal))
            {
                var firstLine = json.IndexOf('\n');
                var lastFence = json.LastIndexOf("```", StringComparison.Ordinal);
                if (firstLine >= 0 && lastFence > firstLine)
                {
                    json = json[(firstLine + 1)..lastFence].Trim();
                }
            }

            var start = json.IndexOf('{');
            var end = json.LastIndexOf('}');
            if (start < 0 || end <= start)
            {
                return null;
            }

            json = json[start..(end + 1)];

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("layout", out var layoutRoot))
                {
                    root = layoutRoot;
                }

                return JsonSerializer.Deserialize<VenueLayoutJsonModel>(root.GetRawText(), JsonOpts);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static VenueLayoutJsonModel? NormalizeLayout(VenueLayoutJsonModel layout)
        {
            layout.CanvasWidth = Clamp(layout.CanvasWidth, 900, 1800, 1200);
            layout.CanvasHeight = Clamp(layout.CanvasHeight, 650, 1400, 820);
            layout.Floors ??= new List<LayoutFloorJsonModel>();
            layout.Sections ??= new List<LayoutSectionJsonModel>();

            if (layout.Sections.Count == 0)
            {
                return null;
            }

            if (layout.Floors.Count == 0)
            {
                var names = layout.Sections
                    .Select(s => string.IsNullOrWhiteSpace(s.FloorName) ? "Етаж 1" : s.FloorName.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                layout.Floors = names.Count == 0
                    ? new List<LayoutFloorJsonModel> { new() { ClientId = "floor-1", Name = "Етаж 1" } }
                    : names.Select((name, index) => new LayoutFloorJsonModel
                    {
                        ClientId = $"floor-{index + 1}",
                        Name = name,
                    }).ToList();
            }

            for (var i = 0; i < layout.Floors.Count; i++)
            {
                layout.Floors[i].ClientId = string.IsNullOrWhiteSpace(layout.Floors[i].ClientId)
                    ? $"floor-{i + 1}"
                    : layout.Floors[i].ClientId.Trim();
                layout.Floors[i].Name = string.IsNullOrWhiteSpace(layout.Floors[i].Name)
                    ? $"Етаж {i + 1}"
                    : layout.Floors[i].Name.Trim();
            }

            var totalSeats = 0;
            for (var i = 0; i < layout.Sections.Count; i++)
            {
                var section = layout.Sections[i];
                var floor = layout.Floors.FirstOrDefault(f =>
                    string.Equals(f.ClientId, section.FloorId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(f.Name, section.FloorName, StringComparison.OrdinalIgnoreCase)) ?? layout.Floors[0];

                section.ClientId = string.IsNullOrWhiteSpace(section.ClientId) ? $"section-{i + 1}" : section.ClientId.Trim();
                section.FloorId = floor.ClientId;
                section.FloorName = floor.Name;
                section.Name = string.IsNullOrWhiteSpace(section.Name) ? $"Секция {i + 1}" : section.Name.Trim();
                section.Shape = NormalizeShape(section.Shape, section.Type);
                section.X = Clamp(section.X, 0, layout.CanvasWidth - 80, 80);
                section.Y = Clamp(section.Y, 0, layout.CanvasHeight - 70, 80);
                section.Width = Clamp(section.Width, 100, layout.CanvasWidth - section.X, 320);
                section.Height = Clamp(section.Height, 80, layout.CanvasHeight - section.Y, 200);
                section.Rotation = Clamp(section.Rotation, -45, 45, 0);
                section.Seats ??= new List<SeatJsonModel>();

                if (totalSeats + section.Seats.Count > 650)
                {
                    section.Seats = section.Seats.Take(Math.Max(0, 650 - totalSeats)).ToList();
                }

                for (var s = 0; s < section.Seats.Count; s++)
                {
                    var seat = section.Seats[s];
                    seat.ClientId = string.IsNullOrWhiteSpace(seat.ClientId) ? $"seat-{i + 1}-{s + 1}" : seat.ClientId.Trim();
                    seat.Row = string.IsNullOrWhiteSpace(seat.Row) ? "A" : seat.Row.Trim();
                    seat.Number = string.IsNullOrWhiteSpace(seat.Number) ? (s + 1).ToString() : seat.Number.Trim();
                    seat.Label = string.IsNullOrWhiteSpace(seat.Label) ? seat.Row + seat.Number : seat.Label.Trim();
                    seat.X = Clamp(seat.X, 0, Math.Max(0, section.Width - seat.Radius * 2), 24);
                    seat.Y = Clamp(seat.Y, 0, Math.Max(0, section.Height - seat.Radius * 2), 52);
                    seat.Radius = Clamp(seat.Radius, 9, 55, seat.SeatType == SeatType.Table ? 30 : 15);
                    seat.Rotation = Clamp(seat.Rotation, -180, 180, 0);
                    seat.Capacity = Math.Clamp(seat.Capacity <= 0 ? 1 : seat.Capacity, 1, 100);

                    if (section.Type == LayoutSectionType.Table)
                    {
                        seat.SeatType = SeatType.Table;
                    }
                }

                totalSeats += section.Seats.Count;
                section.Capacity = section.Seats.Count > 0
                    ? section.Seats.Sum(s => s.IsCapacityUnlimited ? 0 : Math.Max(1, s.Capacity))
                    : Math.Max(0, section.Capacity);
            }

            return layout;
        }

        private static string NormalizeShape(string? value, LayoutSectionType type)
        {
            var shape = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (shape is "Rectangle" or "Rounded" or "Circle" or "Stage")
            {
                return shape;
            }

            return type == LayoutSectionType.Table ? "Circle" : "Rounded";
        }

        private static double Clamp(double value, double min, double max, double fallback)
        {
            if (!double.IsFinite(value))
            {
                return fallback;
            }

            if (max < min)
            {
                max = min;
            }

            return Math.Max(min, Math.Min(max, value));
        }

        private static string Truncate(string value, int max)
        {
            return string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max] + "...";
        }
    }
}
