namespace HomeAssistantDataArchiver.Properties;

public class ClickHouseOptions
{
    public const string SectionName = "ClickHouse";

    public required string Host { get; set; }
    public required ushort Port { get; set; }
    public required string Username { get; set; }
    public required string Password { get; set; }
    public required string Database { get; set; }
}
