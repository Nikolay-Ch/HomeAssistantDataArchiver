namespace HomeAssistantDataArchiver.Properties;

public class CommonOptions
{
    public const string SectionName = "Common";

    public required int SensorDataMigrationBatchSize { get; set; }
    public required int ImportEntitiesExecutionIntervalInSeconds { get; set; }
    public required int SensorDataMigrationExecutionIntervalInSeconds { get; set; }
}
