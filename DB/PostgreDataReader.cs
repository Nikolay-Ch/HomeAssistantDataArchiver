using HomeAssistantDataArchiver.Properties;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;

namespace HomeAssistantDataArchiver.DB;
public class PostgreDataReader(IOptions<PostgresOptions> options, ILogger<PostgreDataReader> logger)
{
    private readonly string PgConnectionString = new NpgsqlConnectionStringBuilder
    {
        Host = options.Value.Host,
        Port = options.Value.Port,
        Username = options.Value.Username,
        Password = options.Value.Password,
        Database = options.Value.Database
    }.ToString();

    public async Task<NpgsqlConnection> OpenConnection(CancellationToken token)
    {
        logger?.LogTrace("{methodName} - start", nameof(OpenConnection));

        var connection = new NpgsqlConnection(PgConnectionString);
        await connection.OpenAsync(token);

        logger?.LogTrace("{methodName} - end", nameof(OpenConnection));

        return connection;
    }

    public async Task<DbDataReader> GetStatesSince(NpgsqlConnection connection, double lastTs, CancellationToken token)
    {
        logger?.LogTrace("{methodName} - start", nameof(GetStatesSince));

        string sql = DatabaseQuerires.GetPostgreReadStateQuery();

        var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("lastTs", lastTs - 300); // отнимаем 5 минут - на всякий случай, вдруг - что-то не учли в прошлый раз...

        logger?.LogTrace("{methodName} - end", nameof(GetStatesSince));

        return await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, token);
    }

    public async IAsyncEnumerable<IDictionary<string, object?>>
        ToDictionaryEnumerable(DbDataReader reader, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        logger?.LogTrace("{methodName} - start", nameof(ToDictionaryEnumerable));

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.GetValue(i);
            }
            yield return row;
        }

        logger?.LogTrace("{methodName} - end", nameof(ToDictionaryEnumerable));
    }
}
