using HomeAssistantDataArchiver.Properties;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace HomeAssistantDataArchiver.HomeAssistant;

public class HomeAssistantWebSocketClient(IOptions<HomeAssistantOptions> options, ILogger<HomeAssistantWebSocketClient> logger) : IDisposable
{
    private Uri Uri { get; } = new Uri(options.Value.Uri);
    private string Token { get; } = options.Value.Token;
    private ILogger<HomeAssistantWebSocketClient> Logger { get; } = logger;

    private ClientWebSocket? Ws { get; set; }
    private int RequestId { get; set; } = 1;
    private bool Disposed { get; set; } = false;

    private async Task EnsureConnected(CancellationToken cancellationToken)
    {
        EnsureNotDisposed();

        if (Ws?.State == WebSocketState.Open)
            return;

        try
        {
            Ws?.Dispose();

            Ws = new ClientWebSocket();

            Logger.LogInformation("Connecting to Home Assistant at {Uri}...", Uri);

            await Ws.ConnectAsync(Uri, cancellationToken);
            Logger.LogTrace("WebSocket connection established. Starting auth flow...");

            // 1. Принимаем auth_required
            using (await ReceiveRaw(cancellationToken)) { }

            // 2. Авторизация
            await SendRaw(new { type = "auth", access_token = Token }, cancellationToken);

            using var authStream = await ReceiveRaw(cancellationToken);

            var authResponse = await JsonSerializer.DeserializeAsync<JsonElement>(authStream, cancellationToken: cancellationToken);

            if (authResponse.GetProperty("type").GetString() != "auth_ok")
            {
                Logger.LogError("Auth failed. Response: {Response}", authResponse.GetRawText());
                throw new Exception("Home Assistant Auth Failed");
            }

            Logger.LogInformation("Successfully authenticated with Home Assistant.");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to connect or authorize with Home Assistant at {Uri}", Uri);
            throw;
        }
    }

    public async Task<JsonElement> GetDataAsync(HomeAssistantTableType type, CancellationToken cancellationToken)
    {
        Logger.LogTrace("Fetching data for type: {Type}", type);
        
        EnsureNotDisposed();

        await EnsureConnected(cancellationToken);

        string action = type switch
        {
            HomeAssistantTableType.Entity => "config/entity_registry/list",
            HomeAssistantTableType.Device => "config/device_registry/list",
            HomeAssistantTableType.Area => "config/area_registry/list",
            HomeAssistantTableType.Floor => "config/floor_registry/list",

            _ => throw new NotSupportedException()
        };

        int id = RequestId++;
        await SendRaw(new { id, type = action }, cancellationToken);

        using var responseStream = await ReceiveRaw(cancellationToken);

        var response = await JsonSerializer.DeserializeAsync<HomeAssistantWebSocketResponse>(responseStream, cancellationToken: cancellationToken);

        if (response == null || !response.success)
            throw new Exception(response?.error?.message);

        return response.result;
    }

    private async Task SendRaw(object data, CancellationToken cancellationToken)
    {
        EnsureNotDisposed();

        if (Ws == null)
            throw new NotSupportedException("WebSocket not connected!");

        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data));

        await Ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
    }

    private async Task<Stream> ReceiveRaw(CancellationToken cancellationToken)
    {
        EnsureNotDisposed();

        if (Ws == null)
            throw new NotSupportedException("WebSocket not connected!");

        byte[] buffer = ArrayPool<byte>.Shared.Rent(1024 * 1024);

        var ms = new MemoryStream();

        try
        {
            WebSocketReceiveResult result;

            do
            {
                result = await Ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                await ms.WriteAsync(buffer.AsMemory(0, result.Count), cancellationToken);
            } while (!result.EndOfMessage);
        }
        catch
        {
            ms.Dispose();
            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        ms.Position = 0;
        return ms;
    }

    private void EnsureNotDisposed()
    {
        if (Disposed)
            throw new NotImplementedException("Object is disposed");
    }


    public void Dispose() 
    {
        if (!Disposed)
        {
            Ws?.Dispose();

            GC.SuppressFinalize(this);

            Disposed = true;
        }
    }

    public async IAsyncEnumerable<IDictionary<string, object?>>
        ToDictionaryEnumerable(JsonElement element, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (element.ValueKind != JsonValueKind.Array) yield break;

        foreach (var item in element.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var row = new Dictionary<string, object?>();
            foreach (var prop in item.EnumerateObject())
                row[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number => prop.Value.GetDouble(), // Для временных меток
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    _=> prop.Value.GetRawText(),
                    // Для массивов и объектов возвращаем как есть, 
                    // либо можно сразу десериализовать в Dictionary/List
                    //_ => prop.Value.ValueKind == JsonValueKind.Array
                    //     ? JsonSerializer.Deserialize<List<string>>(prop.Value.GetRawText())
                    //     : JsonSerializer.Deserialize<Dictionary<string, object>>(prop.Value.GetRawText())
                    //_ => JsonSerializer.Deserialize<object>(prop.Value.GetRawText())
                    //_ => JsonNode.Parse(prop.Value.GetRawText())
                };


            //row[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
            //    ? prop.Value.GetString()
            //    : prop.Value.GetRawText();

            await Task.Yield();

            yield return row;
        }
    }
}
