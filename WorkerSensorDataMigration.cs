using HomeAssistantDataArchiver.DB;
using HomeAssistantDataArchiver.Properties;
using Microsoft.Extensions.Options;

namespace HomeAssistantDataArchiver;

public class WorkerSensorDataMigration(IOptions<CommonOptions> options, DbMethods dbMethods, IHostApplicationLifetime lifetime, ILogger<WorkerSensorDataMigration> logger) : BackgroundService
{
    private int SensorDataMigrationExecutionIntervalInSeconds { get; } = options.Value.SensorDataMigrationExecutionIntervalInSeconds;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                logger.LogInformation("Doing migration data process... at: {time}", DateTimeOffset.Now);

                await dbMethods.MigrateWithProgressAsync(cancellationToken);

                logger.LogInformation("Migration data process has finished");

                logger.LogInformation("Sleeping for a {timeout} seconds... at: {time}", SensorDataMigrationExecutionIntervalInSeconds, DateTimeOffset.Now);
                await Task.Delay(SensorDataMigrationExecutionIntervalInSeconds * 1000, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "FATAL ERROR. Stop application.");
                lifetime.StopApplication();
                throw;
            }
        }

        logger.LogInformation("exitting at: {time}", DateTimeOffset.Now);
    }
}
