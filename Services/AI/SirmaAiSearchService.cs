using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EventsApp.Models;
using Microsoft.Extensions.Options;

namespace EventsApp.Services.AI
{
    public class SirmaAiSearchService : IAiSearchService
    {
        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private static readonly string SearchInstructions =
@"You are a search parser for Evento, an events discovery platform in Bulgaria.
Extract structured search filters from the user query.
Return ONLY valid JSON. No markdown. No explanations.

Schema:
{
  ""city"": string or null,
  ""genre"": string or null,
  ""keyword"": string or null,
  ""dateIntent"": string or null,
  ""dateFrom"": string or null,
  ""dateTo"": string or null,
  ""nearMe"": boolean,
  ""latitude"": number or null,
  ""longitude"": number or null,
  ""keywords"": string[]
}

Rules:
- Detect Bulgarian cities, including София, Пловдив, Варна, Бургас, Русе, Стара Загора, Плевен, Велико Търново, Благоевград, Шумен, Добрич, Сливен, Перник, Хасково, Ямбол. Also detect latin spellings: sofia, plovdiv, varna, burgas, ruse, stara zagora, pleven, veliko tarnovo, blagoevgrad, shumen, dobrich, sliven, pernik, haskovo, yambol. Always return city in canonical English form (Sofia, Plovdiv, Varna, Burgas, Ruse, Stara Zagora, Pleven, Sliven, Dobrich, Shumen, Pernik, Haskovo, Yambol, Pazardzhik, Blagoevgrad, Veliko Tarnovo, Vratsa, Gabrovo, Asenovgrad, Vidin, Kazanlak, Kyustendil, Montana, Targovishte, Razgrad, Silistra) or null.
- Detect music/event genres: techno, техно, house, хаус, chalga, чалга, rock, рок, pop, hip hop, jazz, live music, festival. Map genre to one of [Techno, House, HipHop, Pop, Rock, Jazz, Classical, Other] or null.
- If the query says 'около мен', 'near me', set nearMe=true.
- If the query contains a city, set city.
- If the query contains a genre, set genre.
- Ignore conversational filler such as 'искам', 'искам да участвам', 'търся', 'покажи ми', 'намери', 'събития', 'event', 'events' unless it is truly useful for search.
- Put only the remaining meaningful domain words in keyword (single short term) and keywords (array of all meaningful tokens).
- If city/genre/date already capture the user intent and the remaining words are generic or conversational, set keyword=null and keywords=[].
- Treat hackathon-related wording such as 'хакатон', 'хакатона', 'hackathon', 'hack' as the same concept; prefer a short keyword like 'hack' when a keyword is needed.
- A Google Maps toolkit (geocode_address) is available. When you detect a city, you SHOULD call geocode_address with the city name plus ', Bulgaria' (e.g. 'Ruse, Bulgaria') to obtain accurate latitude/longitude and put them into the latitude and longitude fields. If geocoding fails, leave them null.
- dateIntent: short label such as 'tonight','tomorrow','this weekend','this week' or null.
- dateFrom / dateTo: ISO 8601 date strings. Resolve relative phrases:
    'tonight','tazi vecher' -> today
    'tomorrow','utre' -> tomorrow
    'this weekend','uikenda' -> upcoming Saturday and Sunday
    'this week','tazi sedmica' -> today through next Sunday
  Otherwise null.
- Return null for unknown fields.
- Return ONLY the JSON.";

        private static string GetDescriptionInstructions(string? lang) => (lang == "en")
            ? @"You are a marketing copywriter for an events platform (concerts, parties, club nights, festivals).
Write a single concise event description (60-120 words) in plain text (no markdown headings, no lists).
LANGUAGE: Write the description in English. Use natural, fluent English.
Tone: energetic, inviting, modern. Mention the genre and city if relevant. Do NOT invent specific artist names, prices, or times - stick to the inputs given.
Output ONLY the description text in English, no preamble, no quotes."
            : @"You are a marketing copywriter for an events platform (concerts, parties, club nights, festivals).
Write a single concise event description (60-120 words) in plain text (no markdown headings, no lists).
LANGUAGE: Write the description in Bulgarian (български език). Use natural, fluent Bulgarian — not a translation.
Tone: energetic, inviting, modern. Mention the genre and city if relevant. Do NOT invent specific artist names, prices, or times - stick to the inputs given.
Output ONLY the description text in Bulgarian, no preamble, no quotes, no English text.";

        private readonly HttpClient _http;
        private readonly AiOptions _opts;
        private readonly ILogger<SirmaAiSearchService> _logger;
        private readonly SemaphoreSlim _provisionLock = new(1, 1);

        private string? _agentId;

        public AiStatus LastStatus { get; private set; } = AiStatus.Disabled;
        public string? LastStatusDetail { get; private set; }

        public SirmaAiSearchService(HttpClient http, IOptions<AiOptions> opts, ILogger<SirmaAiSearchService> logger)
        {
            _http = http;
            _opts = opts.Value;
            _logger = logger;
            _agentId = string.IsNullOrWhiteSpace(_opts.AgentId) ? null : _opts.AgentId;

            if (_opts.IsConfigured)
            {
                _http.BaseAddress = new Uri(_opts.BaseUrl.TrimEnd('/') + "/");
                _http.DefaultRequestHeaders.Remove("X-API-Key");
                _http.DefaultRequestHeaders.Add("X-API-Key", _opts.ApiKey);
                _http.Timeout = TimeSpan.FromSeconds(Math.Max(5, _opts.TimeoutSeconds));
                LastStatus = AiStatus.Ok;
            }
            else
            {
                LastStatus = AiStatus.Disabled;
                LastStatusDetail = "AI:ApiKey or AI:BaseUrl is empty.";
            }
        }

        public bool IsEnabled => _opts.IsConfigured;

        public Task<AiSearchIntent?> InterpretAsync(string query, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Task.FromResult<AiSearchIntent?>(null);
            }
            return InterpretInternalAsync(query, cancellationToken);
        }

        private async Task<AiSearchIntent?> InterpretInternalAsync(string query, CancellationToken cancellationToken)
        {
            var prompt = SearchInstructions + "\n\nUser query: " + query.Trim() +
                         "\nCurrent UTC date: " + DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            var raw = await RunAgentAsync(prompt, "smart-search", cancellationToken);
            if (raw == null) return null;

            var intent = ParseIntentJson(raw);
            if (intent == null)
            {
                LastStatus = AiStatus.ParseFailed;
                LastStatusDetail = "AI returned a response that didn't match the expected JSON shape.";
                _logger.LogWarning("Sirma AI returned unparseable response: {Body}", Truncate(raw, 300));
                return null;
            }

            intent.RawQuery = query.Trim();
            return intent;
        }

        public async Task<string?> GenerateTextAsync(string prompt, string tag, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(prompt)) return null;
            var raw = await RunAgentAsync(prompt, tag, cancellationToken);
            if (raw == null) return null;
            var clean = raw.Trim().Trim('"').Trim();
            return string.IsNullOrWhiteSpace(clean) ? null : clean;
        }

        public async Task<string?> GenerateEventDescriptionAsync(string title, string? city, string? genre, string? hints, string? lang = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(title)) return null;

            var sb = new StringBuilder();
            sb.AppendLine(GetDescriptionInstructions(lang));
            sb.AppendLine();
            sb.AppendLine("Event title: " + title.Trim());
            if (!string.IsNullOrWhiteSpace(city)) sb.AppendLine("City: " + city.Trim());
            if (!string.IsNullOrWhiteSpace(genre)) sb.AppendLine("Genre: " + genre.Trim());
            if (!string.IsNullOrWhiteSpace(hints)) sb.AppendLine("Extra notes from organizer: " + hints.Trim());

            var raw = await RunAgentAsync(sb.ToString(), "describe", cancellationToken);
            if (raw == null) return null;

            var clean = raw.Trim().Trim('"').Trim();
            return string.IsNullOrWhiteSpace(clean) ? null : clean;
        }

        private async Task<string?> RunAgentAsync(string prompt, string tag, CancellationToken cancellationToken)
        {
            if (!_opts.IsConfigured)
            {
                LastStatus = AiStatus.Disabled;
                LastStatusDetail = "AI:ApiKey is not configured.";
                return null;
            }

            string? agentId;
            try
            {
                agentId = await EnsureAgentAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                LastStatus = AiStatus.ProvisionFailed;
                LastStatusDetail = ex.Message;
                _logger.LogWarning(ex, "Sirma AI provisioning failed");
                return null;
            }

            if (string.IsNullOrEmpty(agentId)) return null;

            try
            {
                using var form = new MultipartFormDataContent
                {
                    { new StringContent(prompt, Encoding.UTF8, "text/plain"), "message" },
                    { new StringContent(tag), "tag" },
                };

                using var resp = await _http.PostAsync($"agents/{agentId}/run", form, cancellationToken);
                var body = await resp.Content.ReadAsStringAsync(cancellationToken);

                if (!resp.IsSuccessStatusCode)
                {
                    LastStatus = AiStatus.CallFailed;
                    LastStatusDetail = $"HTTP {(int)resp.StatusCode}: {Truncate(body, 200)}";
                    _logger.LogWarning("Sirma run failed {Status}: {Body}", (int)resp.StatusCode, Truncate(body, 400));
                    return null;
                }

                using var doc = JsonDocument.Parse(body);
                if (!doc.RootElement.TryGetProperty("data", out var data) ||
                    !data.TryGetProperty("content", out var content))
                {
                    LastStatus = AiStatus.ParseFailed;
                    LastStatusDetail = "Response missing data.content";
                    return null;
                }

                LastStatus = AiStatus.Ok;
                LastStatusDetail = null;

                return content.ValueKind == JsonValueKind.String
                    ? content.GetString() ?? string.Empty
                    : content.GetRawText();
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                LastStatus = AiStatus.CallFailed;
                LastStatusDetail = $"Sirma timed out after {_opts.TimeoutSeconds}s.";
                _logger.LogWarning("Sirma AI timed out after {Sec}s", _opts.TimeoutSeconds);
                return null;
            }
            catch (Exception ex)
            {
                LastStatus = AiStatus.CallFailed;
                LastStatusDetail = ex.Message;
                _logger.LogWarning(ex, "Sirma AI call failed");
                return null;
            }
        }

        // Image generation via Sirma removed — use text-only AI features.

        private async Task<string?> EnsureAgentAsync(CancellationToken ct)
        {
            if (!string.IsNullOrEmpty(_agentId)) return _agentId;
            if (!_opts.CanProvision)
            {
                LastStatus = AiStatus.MissingProjectId;
                LastStatusDetail = "AI:ProjectId is not set. Either set it (from your Sirma dashboard > Settings > Project) or paste an existing agent UUID into AI:AgentId / .env Sirma_agent_id.";
                _logger.LogWarning("Sirma AI: ProjectId not set. Set AI:ProjectId or AI:AgentId to enable smart features.");
                return null;
            }

            await _provisionLock.WaitAsync(ct);
            try
            {
                if (!string.IsNullOrEmpty(_agentId)) return _agentId;

                var found = await FindExistingAgentAsync(ct);
                if (!string.IsNullOrEmpty(found))
                {
                    _agentId = found;
                    _logger.LogInformation("Sirma AI: Reusing existing agent {AgentId}", _agentId);
                    return _agentId;
                }

                _agentId = await CreateAgentAsync(ct);
                if (!string.IsNullOrEmpty(_agentId))
                {
                    _logger.LogInformation("Sirma AI: Provisioned new agent {AgentId}", _agentId);
                }
                else
                {
                    LastStatus = AiStatus.ProvisionFailed;
                }
                return _agentId;
            }
            finally
            {
                _provisionLock.Release();
            }
        }

        private async Task<string?> FindExistingAgentAsync(CancellationToken ct)
        {
            try
            {
                using var resp = await _http.GetAsync("agents", ct);
                if (!resp.IsSuccessStatusCode) return null;
                var body = await resp.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(body);
                if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                    return null;

                foreach (var ag in data.EnumerateArray())
                {
                    var name = ag.TryGetProperty("name", out var n) ? n.GetString() : null;
                    var preset = ag.TryGetProperty("presetName", out var p) ? p.GetString() : null;
                    if (string.Equals(name, _opts.AgentName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(preset, _opts.PresetName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (ag.TryGetProperty("aiServiceAgentId", out var aid))
                            return aid.GetString();
                        if (ag.TryGetProperty("id", out var id))
                            return id.GetString() ?? id.GetRawText();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Sirma AI: list agents failed");
            }
            return null;
        }

        private async Task<string?> CreateAgentAsync(CancellationToken ct)
        {
            var payload = new
            {
                projectId = _opts.ProjectId,
                name = _opts.AgentName,
                presetName = _opts.PresetName,
                description = "Evento assistant: parses search queries and generates event copy.",
                configuration = new
                {
                    model = new
                    {
                        provider = _opts.ProviderConfigId,
                        modelName = _opts.ModelName,
                        temperature = 0.4,
                        maxTokens = 600,
                    },
                    instructions = "You are a multi-purpose helper. Follow the instructions provided in each user message exactly.",
                    addDatetimeToContext = true,
                    markdown = false,
                    timeoutSeconds = 20,
                    maxIterations = 1,
                    parallelToolCalls = false,
                    tools = Array.Empty<object>(),
                    memory = new { enableAgenticMemory = false, enableUserMemories = false, enableSessionSummaries = false },
                    sessionStorage = new { addHistoryToContext = false, readChatHistory = false, numHistoryRuns = 0 },
                },
            };

            using var json = new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json");
            using var resp = await _http.PostAsync("agents", json, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                LastStatus = AiStatus.ProvisionFailed;
                LastStatusDetail = $"Create agent failed HTTP {(int)resp.StatusCode}: {Truncate(body, 200)}";
                _logger.LogWarning("Sirma AI: create agent failed {Status}: {Body}", (int)resp.StatusCode, Truncate(body, 400));
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("data", out var data) &&
                data.TryGetProperty("aiServiceAgentId", out var aid))
            {
                return aid.GetString();
            }
            return null;
        }

        private static AiSearchIntent? ParseIntentJson(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            var s = raw.Trim();
            if (s.StartsWith("```"))
            {
                var first = s.IndexOf('\n');
                var last = s.LastIndexOf("```", StringComparison.Ordinal);
                if (first > 0 && last > first) s = s.Substring(first + 1, last - first - 1).Trim();
            }
            var startBrace = s.IndexOf('{');
            var endBrace = s.LastIndexOf('}');
            if (startBrace < 0 || endBrace <= startBrace) return null;
            s = s.Substring(startBrace, endBrace - startBrace + 1);

            try
            {
                using var doc = JsonDocument.Parse(s);
                var r = doc.RootElement;
                var intent = new AiSearchIntent
                {
                    City = ReadString(r, "city"),
                    Keyword = ReadString(r, "keyword"),
                    DateIntent = ReadString(r, "dateIntent"),
                    NearMe = r.TryGetProperty("nearMe", out var nm) && nm.ValueKind == JsonValueKind.True,
                    Latitude = ReadDouble(r, "latitude"),
                    Longitude = ReadDouble(r, "longitude"),
                    Keywords = ReadStringArray(r, "keywords"),
                };

                var genreStr = ReadString(r, "genre");
                if (!string.IsNullOrWhiteSpace(genreStr))
                {
                    if (Enum.TryParse<EventGenre>(genreStr, ignoreCase: true, out var g))
                    {
                        intent.Genre = g;
                    }
                    else
                    {
                        var normalized = NormalizeGenre(genreStr);
                        if (normalized != null && Enum.TryParse<EventGenre>(normalized, ignoreCase: true, out var g2))
                        {
                            intent.Genre = g2;
                        }
                    }
                }

                if (TryParseDate(ReadString(r, "dateFrom"), out var df)) intent.DateFrom = df;
                if (TryParseDate(ReadString(r, "dateTo"), out var dt)) intent.DateTo = dt;

                if (intent.City == null && intent.Keyword == null && intent.Genre == null &&
                    intent.DateFrom == null && intent.DateTo == null && !intent.NearMe &&
                    intent.Keywords.Length == 0 && intent.Latitude == null && intent.Longitude == null)
                {
                    return null;
                }
                return intent;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string? ReadString(JsonElement el, string prop)
        {
            if (!el.TryGetProperty(prop, out var v)) return null;
            if (v.ValueKind == JsonValueKind.Null) return null;
            if (v.ValueKind != JsonValueKind.String) return null;
            var s = v.GetString();
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        }

        private static double? ReadDouble(JsonElement el, string prop)
        {
            if (!el.TryGetProperty(prop, out var v)) return null;
            if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d)) return d;
            if (v.ValueKind == JsonValueKind.String &&
                double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var ds)) return ds;
            return null;
        }

        private static string[] ReadStringArray(JsonElement el, string prop)
        {
            if (!el.TryGetProperty(prop, out var v) || v.ValueKind != JsonValueKind.Array)
                return Array.Empty<string>();
            var list = new List<string>();
            foreach (var item in v.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var s = item.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) list.Add(s.Trim());
                }
            }
            return list.ToArray();
        }

        private static string? NormalizeGenre(string s)
        {
            var lower = s.Trim().ToLowerInvariant();
            return lower switch
            {
                "техно" or "techno" => "Techno",
                "хаус" or "house" => "House",
                "хип хоп" or "хип-хоп" or "hip hop" or "hip-hop" or "hiphop" or "rap" or "рап" => "HipHop",
                "поп" or "pop" or "чалга" or "chalga" => "Pop",
                "рок" or "rock" => "Rock",
                "джаз" or "jazz" => "Jazz",
                "класика" or "classical" or "classic" => "Classical",
                _ => null,
            };
        }

        private static bool TryParseDate(string? s, out DateTime result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(s)) return false;
            return DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out result);
        }

        private static string Truncate(string s, int max) =>
            string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max] + "...";
    }
}
