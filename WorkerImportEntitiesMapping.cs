using HomeAssistantDataArchiver.DB;
using HomeAssistantDataArchiver.HomeAssistant;
using HomeAssistantDataArchiver.Properties;
using Microsoft.Extensions.Options;

namespace HomeAssistantDataArchiver;

internal class WorkerImportEntitiesMapping(IOptions<CommonOptions> options, IServiceScopeFactory scopeFactory, DbMethods dbMethods, IHostApplicationLifetime lifetime, ILogger<WorkerImportEntitiesMapping> logger) : BackgroundService
{
    private int ImportEntitiesExecutionIntervalInSeconds { get; } = options.Value.ImportEntitiesExecutionIntervalInSeconds;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("running at: {time}", DateTimeOffset.Now);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();

                var ws = scope.ServiceProvider.GetService<HomeAssistantWebSocketClient>();
                if (ws == null)
                {
                    logger.LogError("Can't create {HomeAssistantWebSocketClient} class", nameof(HomeAssistantWebSocketClient));
                    throw new Exception($"Can't create {nameof(HomeAssistantWebSocketClient)} class");
                }

                logger.LogInformation("Doing migration service data process... at: {time}", DateTimeOffset.Now);

                await dbMethods.MigrateEntitiesAsync(ws, cancellationToken);

                logger.LogInformation("Migration service data process has finished");

                logger.LogInformation("Sleeping for a {timeout} seconds... at: {time}", ImportEntitiesExecutionIntervalInSeconds, DateTimeOffset.Now);
                await Task.Delay(ImportEntitiesExecutionIntervalInSeconds * 1000, cancellationToken);
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
