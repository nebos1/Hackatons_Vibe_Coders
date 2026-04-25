namespace EventsApp.Services.AI
{
    public class AiOptions
    {
        public const string SectionName = "AI";

        public string Provider { get; set; } = "Sirma";
        public string BaseUrl { get; set; } = "https://stage.sirma.ai/client/api/v1";
        public string ApiKey { get; set; } = string.Empty;
        public int ProjectId { get; set; }
        public string AgentName { get; set; } = "GrooveOnSearch";
        public string PresetName { get; set; } = "GrooveOn Smart Search";
        public int ProviderConfigId { get; set; } = 1610;
        public string ModelName { get; set; } = "gpt-4.1-mini";
        public string? AgentId { get; set; }
        public bool AutoProvisionAgent { get; set; } = true;
        public int TimeoutSeconds { get; set; } = 20;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(ApiKey) &&
            !string.IsNullOrWhiteSpace(BaseUrl);

        public bool CanProvision =>
            IsConfigured && AutoProvisionAgent && ProjectId > 0;
    }
}
