namespace EventsApp.Infrastructure
{
    public static class DotEnvLoader
    {
        public static IDictionary<string, string> LoadIntoConfiguration(string envPath, IConfigurationBuilder configBuilder)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(envPath))
            {
                return dict;
            }

            foreach (var line in File.ReadAllLines(envPath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("#")) continue;

                var idx = line.IndexOf('=');
                if (idx <= 0) continue;

                var key = line.Substring(0, idx).Trim();
                var value = line.Substring(idx + 1).Trim();

                if (value.Length >= 2 &&
                    ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
                {
                    value = value.Substring(1, value.Length - 2);
                }

                dict[key] = value;
            }

            var mapped = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in dict)
            {
                mapped[kv.Key] = kv.Value;

                if (string.Equals(kv.Key, "Sirma_key", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(kv.Key, "SIRMA_API_KEY", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(kv.Key, "AI_API_KEY", StringComparison.OrdinalIgnoreCase))
                {
                    mapped["AI:ApiKey"] = kv.Value;
                    mapped["SirmaAi:ApiKey"] = kv.Value;
                }
                if (string.Equals(kv.Key, "Sirma_project_id", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(kv.Key, "SIRMA_PROJECT_ID", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(kv.Key, "AI_PROJECT_ID", StringComparison.OrdinalIgnoreCase))
                {
                    mapped["AI:ProjectId"] = kv.Value;
                }
                if (string.Equals(kv.Key, "Sirma_agent_id", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(kv.Key, "SIRMA_AGENT_ID", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(kv.Key, "AI_AGENT_ID", StringComparison.OrdinalIgnoreCase))
                {
                    mapped["AI:AgentId"] = kv.Value;
                    mapped["SirmaAi:AgentId"] = kv.Value;
                }
                if (string.Equals(kv.Key, "Sirma_domain", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(kv.Key, "SIRMA_DOMAIN", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(kv.Key, "SIRMA_AI_DOMAIN", StringComparison.OrdinalIgnoreCase))
                {
                    mapped["SirmaAi:Domain"] = kv.Value;
                }
                if (string.Equals(kv.Key, "GOOGLE_MAPS_KEY", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(kv.Key, "GOOGLE_MAPS_API_KEY", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(kv.Key, "GoogleMaps_key", StringComparison.OrdinalIgnoreCase))
                {
                    mapped["GoogleMaps:ApiKey"] = kv.Value;
                }
            }

            configBuilder.AddInMemoryCollection(mapped);
            return dict;
        }
    }
}
