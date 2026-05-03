namespace HomeAssistantDataArchiver.Properties;

public class HomeAssistantOptions
{
    public const string SectionName = "HomeAssistant";

    public string Uri { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
}
