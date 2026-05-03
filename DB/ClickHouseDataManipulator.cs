using ClickHouse.Client.ADO;
using HomeAssistantDataArchiver.HomeAssistant;
using HomeAssistantDataArchiver.Properties;
using Microsoft.Extensions.Options;

namespace HomeAssistantDataArchiver.DB;

public class ClickHouseDataManipulator
{
    private string ChConnectionString => new ClickHouseConnectionStringBuilder
    {
        Host = Options.Value.Host,
        Port = Options.Value.Port,
        Username = Options.Value.Username,
        Password = Options.Value.Password,
        Database = Options.Value.Database
    }.ToString();

    private IOptions<ClickHouseOptions> Options { get; }
    private ILogger<ClickHouseDataManipulator>? Logger { get; }

    public ClickHouseDataManipulator(IOptions<ClickHouseOptions> options, ILogger<ClickHouseDataManipulator> logger)
    {
        Options = options;
        Logger = logger;

        Logger?.LogInformation("Beginning creating ClickHouse tables (if needed) at: {time}", DateTimeOffset.Now);
        var cts = new CancellationTokenSource();
        using var conn = OpenClickHouseConnection(cts.Token).Result;
        CreateTablesIfNeeded(conn, cts.Token).Wait();
        Logger?.LogInformation("Сreation ClickHouse tables complete at: {time}", DateTimeOffset.Now);
    }

    public async Task<ClickHouseConnection> OpenClickHouseConnection(CancellationToken cancellationToken)
    {
        Logger?.LogTrace("{methodName} - start", nameof(OpenClickHouseConnection));

        using var conn = new ClickHouseConnection(ChConnectionString);
        await conn.OpenAsync(cancellationToken);

        Logger?.LogTrace("{methodName} - end", nameof(OpenClickHouseConnection));

        return conn;
    }

    public async Task CreateTablesIfNeeded(ClickHouseConnection connection, CancellationToken cancellationToken)
    {
        Logger?.LogTrace("{methodName} - start", nameof(CreateTablesIfNeeded));

        foreach (var hatt in Enum.GetValues<HomeAssistantTableType>())
        {
            using var pgCommand = connection.CreateCommand();
            pgCommand.CommandText = DatabaseQuerires.GetClickHouseCreateScript(hatt);
            await pgCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        Logger?.LogTrace("{methodName} - end", nameof(CreateTablesIfNeeded));
    }

    public async Task<double> GetLastStateTimestampAsync(ClickHouseConnection connection, CancellationToken cancellationToken)
    {
        Logger?.LogTrace("{methodName} - start", nameof(GetLastStateTimestampAsync));

        using var cmd = connection.CreateCommand();

        cmd.CommandText = DatabaseQuerires.GetLastStateTimestampQuery();
        var result = await cmd.ExecuteScalarAsync(cancellationToken);

        Logger?.LogTrace("{methodName} - end", nameof(GetLastStateTimestampAsync));

        return result != DBNull.Value ? Convert.ToDouble(result) : 0;
    }
}
