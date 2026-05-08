namespace EventsApp.Common
{
    public static class CityCoordinates
    {
        private static readonly Dictionary<string, (double Lat, double Lng)> Coords =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["Sofia"] = (42.6977, 23.3219),
                ["София"] = (42.6977, 23.3219),
                ["Plovdiv"] = (42.1354, 24.7453),
                ["Пловдив"] = (42.1354, 24.7453),
                ["Varna"] = (43.2141, 27.9147),
                ["Варна"] = (43.2141, 27.9147),
                ["Burgas"] = (42.5048, 27.4626),
                ["Бургас"] = (42.5048, 27.4626),
                ["Ruse"] = (43.8564, 25.9658),
                ["Русе"] = (43.8564, 25.9658),
                ["Stara Zagora"] = (42.4258, 25.6342),
                ["Стара Загора"] = (42.4258, 25.6342),
                ["Pleven"] = (43.4170, 24.6166),
                ["Плевен"] = (43.4170, 24.6166),
                ["Sliven"] = (42.6824, 26.3293),
                ["Сливен"] = (42.6824, 26.3293),
                ["Dobrich"] = (43.5675, 27.8275),
                ["Добрич"] = (43.5675, 27.8275),
                ["Shumen"] = (43.2706, 26.9229),
                ["Шумен"] = (43.2706, 26.9229),
                ["Pernik"] = (42.6055, 23.0307),
                ["Перник"] = (42.6055, 23.0307),
                ["Haskovo"] = (41.9344, 25.5556),
                ["Хасково"] = (41.9344, 25.5556),
                ["Yambol"] = (42.4842, 26.5036),
                ["Ямбол"] = (42.4842, 26.5036),
                ["Pazardzhik"] = (42.1928, 24.3378),
                ["Пазарджик"] = (42.1928, 24.3378),
                ["Blagoevgrad"] = (42.0119, 23.0897),
                ["Благоевград"] = (42.0119, 23.0897),
                ["Veliko Tarnovo"] = (43.0757, 25.6172),
                ["Велико Търново"] = (43.0757, 25.6172),
                ["Vratsa"] = (43.2102, 23.5527),
                ["Враца"] = (43.2102, 23.5527),
                ["Gabrovo"] = (42.8740, 25.3187),
                ["Габрово"] = (42.8740, 25.3187),
                ["Asenovgrad"] = (42.0167, 24.8667),
                ["Асеновград"] = (42.0167, 24.8667),
                ["Vidin"] = (43.9961, 22.8775),
                ["Видин"] = (43.9961, 22.8775),
                ["Kazanlak"] = (42.6175, 25.3942),
                ["Казанлък"] = (42.6175, 25.3942),
                ["Kyustendil"] = (42.2842, 22.6911),
                ["Кюстендил"] = (42.2842, 22.6911),
                ["Montana"] = (43.4123, 23.2256),
                ["Монтана"] = (43.4123, 23.2256),
                ["Targovishte"] = (43.2503, 26.5722),
                ["Търговище"] = (43.2503, 26.5722),
                ["Razgrad"] = (43.5333, 26.5167),
                ["Разград"] = (43.5333, 26.5167),
                ["Silistra"] = (44.1167, 27.2667),
                ["Силистра"] = (44.1167, 27.2667),
            };

        public static bool TryGetCoordinates(string city, out double lat, out double lng)
        {
            if (!string.IsNullOrWhiteSpace(city) && Coords.TryGetValue(city.Trim(), out var c))
            {
                lat = c.Lat;
                lng = c.Lng;
                return true;
            }

            lat = 0;
            lng = 0;
            return false;
        }

        public static IReadOnlyList<string> GetCanonicalCities()
        {
            return Coords
                .Keys
                .Where(IsAscii)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(city => city)
                .ToArray();
        }

        public static IReadOnlyDictionary<string, (double Lat, double Lng)> GetCanonicalCoordinates()
        {
            return Coords
                .Where(kv => IsAscii(kv.Key))
                .GroupBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.First().Value,
                    StringComparer.OrdinalIgnoreCase);
        }

        public static string? GetCanonicalName(string? city)
        {
            if (string.IsNullOrWhiteSpace(city))
            {
                return null;
            }

            var trimmed = city.Trim();
            if (!Coords.TryGetValue(trimmed, out var match))
            {
                return trimmed;
            }

            return Coords
                .Where(kv => kv.Value.Lat == match.Lat && kv.Value.Lng == match.Lng && IsAscii(kv.Key))
                .Select(kv => kv.Key)
                .OrderBy(name => name)
                .FirstOrDefault() ?? trimmed;
        }

        public static IReadOnlyList<string> GetEquivalentNames(string? city)
        {
            if (string.IsNullOrWhiteSpace(city))
            {
                return Array.Empty<string>();
            }

            var trimmed = city.Trim();
            if (!Coords.TryGetValue(trimmed, out var match))
            {
                return new[] { trimmed };
            }

            return Coords
                .Where(kv => kv.Value.Lat == match.Lat && kv.Value.Lng == match.Lng)
                .Select(kv => kv.Key)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static bool IsAscii(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.All(ch => ch < 128);
        }
    }
}
