using ClickHouse.Client.ADO;
using ClickHouse.Client.Copy;
using HomeAssistantDataArchiver.HomeAssistant;
using HomeAssistantDataArchiver.Properties;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text.Json;

namespace HomeAssistantDataArchiver.DB;

public class DbMethods(
    IOptions<CommonOptions> options,
    PostgreDataReader postgreDataReader,
    ClickHouseDataManipulator clickHouseDataManipulator,
    ILogger<DbMethods> logger)
{

    // Настройки
    private int BatchSize { get; } = options.Value.SensorDataMigrationBatchSize;
    private CultureInfo Culture { get; } = CultureInfo.InvariantCulture;

    public async Task MigrateEntitiesAsync(HomeAssistantWebSocketClient ws, CancellationToken cancellationToken)
    {
        logger?.LogTrace("{methodName} - start", nameof(MigrateEntitiesAsync));

        var chConn = await clickHouseDataManipulator.OpenClickHouseConnection(cancellationToken);

        foreach (var hatt in Enum.GetValues<HomeAssistantTableType>())
        {
            if (!hatt.IsRequestedByApi())
                continue;

            logger?.LogInformation("Read data: {hatt} from HomeAssistant", hatt);
            var dataToStore = ws.ToDictionaryEnumerable(await ws.GetDataAsync(hatt, cancellationToken), cancellationToken);

            await StoreDataToClickHouse(hatt, chConn, dataToStore, cancellationToken);
        }

        logger?.LogTrace("{methodName} - end", nameof(MigrateEntitiesAsync));
    }

    public async Task MigrateWithProgressAsync(CancellationToken cancellationToken)
    {
        logger?.LogTrace("{methodName} - start", nameof(MigrateWithProgressAsync));

        using var pgConn = await postgreDataReader.OpenConnection(cancellationToken);

        using var chConn = await clickHouseDataManipulator.OpenClickHouseConnection(cancellationToken);

        logger?.LogInformation("Query last stored record");

        double lastTs = await clickHouseDataManipulator.GetLastStateTimestampAsync(chConn, cancellationToken);

        logger?.LogInformation("Last stored record: {time}", DateTimeOffset.FromUnixTimeMilliseconds((long)(lastTs * 1000)));

        using var reader = await postgreDataReader.GetStatesSince(pgConn, lastTs, cancellationToken);
        var dataToStore = postgreDataReader.ToDictionaryEnumerable(reader, cancellationToken);

        await StoreDataToClickHouse(HomeAssistantTableType.State, chConn, dataToStore, cancellationToken);

        logger?.LogInformation("Migration completed successfully at: {time}!", DateTimeOffset.Now);

        logger?.LogTrace("{methodName} - end", nameof(MigrateWithProgressAsync));
    }

    private async Task StoreDataToClickHouse(
        HomeAssistantTableType tableType,
        ClickHouseConnection chConn,
        IAsyncEnumerable<IDictionary<string, object?>> dataToStore,
        CancellationToken cancellationToken)
    {
        using var bulkCopy = new ClickHouseBulkCopy(chConn)
        {
            DestinationTableName = DatabaseQuerires.GetClickHouseTableName(tableType),
            BatchSize = BatchSize
        };

        await bulkCopy.InitAsync();

        var batch = new List<object?[]>();
        long processedCount = 0;

        logger?.LogInformation("Beginning reading records at: {time}", DateTimeOffset.Now);

        await foreach (IDictionary<string, object?> row in dataToStore.WithCancellation(cancellationToken))
        {
            logger?.LogTrace("Parsing {count} row", batch.Count);
            batch.Add(FillData(row, tableType));

            processedCount++;

            if (batch.Count >= BatchSize)
            {
                logger?.LogInformation("Read {count} records at: {time}", batch.Count, DateTimeOffset.Now);

                logger?.LogInformation("Beginning storing records at: {time}", DateTimeOffset.Now);

                await bulkCopy.WriteToServerAsync(batch, cancellationToken);

                batch.Clear();

                if (cancellationToken.IsCancellationRequested)
                    break;
            }
        }

        if (batch.Count > 0)
            await bulkCopy.WriteToServerAsync(batch, cancellationToken);
    }

    private object?[] FillData(IDictionary<string, object?> row, HomeAssistantTableType homeAssistantTableType)
    {
        switch (homeAssistantTableType)
        {
            case HomeAssistantTableType.Entity:
                return [
                    (string)row["entity_id"]!,
                    (string)row["id"]!,
                    (string)row["unique_id"]!,
                    (string?)row["device_id"],
                    (string?)row["area_id"],
                    (string?)row["config_entry_id"],
                    (string?)row["config_subentry_id"],
                    (string?)row["name"],
                    (string?)row["original_name"],
                    (string?)row["icon"],
                    (string)row["platform"]!,
                    (string?)row["entity_category"],
                    (string?)row["translation_key"],
                    (string?)row["disabled_by"],
                    (string?)row["hidden_by"],
                    (bool)row["has_entity_name"]!,
                    JsonSerializer.Deserialize<string[]>(row["labels"]?.ToString() ?? "[]") ?? [],
                    JsonSerializer.Deserialize<Dictionary<string, string>>(row["categories"]?.ToString() ?? "{}") ?? [],
                    row["options"]?.ToString() ?? "{}",
                    DateTimeOffset.FromUnixTimeMilliseconds((long)((row["created_at"] as double? ?? 0.0) * 1000)).DateTime,
                    DateTimeOffset.FromUnixTimeMilliseconds((long)((row["modified_at"] as double? ?? 0.0) * 1000)).DateTime,
                    DateTime.UtcNow
                ];

            case HomeAssistantTableType.Device:
                return [
                    row["id"]?.ToString() ?? string.Empty,
                    row["name"]?.ToString() ?? string.Empty,
                    row["name_by_user"]?.ToString() ?? string.Empty,
                    row["manufacturer"]?.ToString() ?? string.Empty,
                    row["model"]?.ToString() ?? string.Empty,
                    row["model_id"]?.ToString() ?? string.Empty,
                    row["sw_version"]?.ToString() ?? string.Empty,
                    row["hw_version"]?.ToString() ?? string.Empty,
                    row["serial_number"]?.ToString(), // Nullable(String)
                    row["area_id"]?.ToString() ?? string.Empty,
                    row["via_device_id"]?.ToString(), // Nullable(String)
                    JsonSerializer.Deserialize<string[][]>(row["identifiers"]?.ToString() ?? "[]") ?? [],
                    JsonSerializer.Deserialize<string[][]>(row["connections"]?.ToString() ?? "[]") ?? [],
                    JsonSerializer.Deserialize<string[]>(row["labels"]?.ToString() ?? "[]") ?? [],
                    DateTimeOffset.FromUnixTimeMilliseconds((long)((row["created_at"] as double? ?? 0.0) * 1000)).DateTime,
                    DateTimeOffset.FromUnixTimeMilliseconds((long)((row["modified_at"] as double? ?? 0.0) * 1000)).DateTime,
                    DateTime.UtcNow
                ];
            case HomeAssistantTableType.Area:
                return [
                    row["area_id"]?.ToString(),
                    row["name"]?.ToString(),
                    row["icon"]?.ToString(),
                    row["picture"]?.ToString(),
                    row["floor_id"]?.ToString(),
                    row["temperature_entity_id"]?.ToString(),
                    row["humidity_entity_id"]?.ToString(),
                    JsonSerializer.Deserialize<string[]>(row["aliases"]?.ToString() ?? "[]") ?? [],
                    JsonSerializer.Deserialize<string[]>(row["labels"]?.ToString() ?? "[]") ?? [],
                    DateTimeOffset.FromUnixTimeMilliseconds((long)((row["created_at"] as double? ?? 0.0) * 1000)).DateTime,
                    DateTimeOffset.FromUnixTimeMilliseconds((long)((row["modified_at"] as double? ?? 0.0) * 1000)).DateTime,
                    DateTime.UtcNow
                ];

            case HomeAssistantTableType.Floor:
                return [
                    row["floor_id"] ?.ToString(),
                    row["name"]?.ToString(),
                    Convert.ToSByte(row["level"] ?? 0),
                    row["icon"]?.ToString(),
                    JsonSerializer.Deserialize<string[]>(row["aliases"]?.ToString() ?? "[]") ?? [],
                    DateTimeOffset.FromUnixTimeMilliseconds((long)((row["created_at"] as double? ?? 0.0) * 1000)).DateTime,
                    DateTimeOffset.FromUnixTimeMilliseconds((long)((row["modified_at"] as double? ?? 0.0) * 1000)).DateTime,
                    DateTime.UtcNow
                ];

            case HomeAssistantTableType.State:
                double ts = (double)row["last_updated_ts"]!;
                string entityId = (string?)row["entity_id"] ?? "unknown";
                var state = (string?)row["state"];
                string attrs = (string?)row["shared_attrs"] ?? "{}";

                var (type, val) = ParseState(state, Culture);
                var currentDate = DateTimeOffset.FromUnixTimeMilliseconds((long)(ts * 1000)).DateTime;

                return [
                    currentDate,
                    entityId,
                    type,
                    type == "i" ? val : null,
                    type == "f" ? val : null,
                    type == "b" ? val : null,
                    type == "s" ? state : null,
                    attrs
                ];
            default:
                throw new NotSupportedException();
        }
    }

    (string Type, object? Val) ParseState(string? state, CultureInfo ci)
    {
        if (string.IsNullOrWhiteSpace(state) || state == "unknown" || state == "unavailable")
            return ("s", state);

        var lower = state.ToLower();
        if (lower is "on" or "home" or "true") return ("b", true);
        if (lower is "off" or "not_home" or "false") return ("b", false);

        if (long.TryParse(state, out var i)) return ("i", i);
        if (double.TryParse(state, NumberStyles.Any, ci, out var d)) return ("f", d);

        return ("s", state);
    }
}
