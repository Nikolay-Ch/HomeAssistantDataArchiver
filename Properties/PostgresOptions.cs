namespace HomeAssistantDataArchiver.Properties;

public class PostgresOptions
{
    public const string SectionName = "Postgres";

    public required string Host { get; set; }
    public required ushort Port { get; set; }
    public required string Username { get; set; }
    public required string Password { get; set; }
    public required string Database { get; set; }
}
