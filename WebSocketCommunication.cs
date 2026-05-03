using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace HomeAssistantDataArchiver
{
    internal class WebSocketCommunication(Uri uri, ILogger<WebSocketCommunication> logger)
    {
        private class HaResponse
        {
            public bool success { get; set; }
            public List<JsonElement>? result { get; set; }
        }

        public string Token { get; set; } = "";

        private async Task Send(ClientWebSocket ws, object data, CancellationToken cancellationToken) =>
            await ws.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data)), WebSocketMessageType.Text, true, cancellationToken);

        private async Task<string> Receive(ClientWebSocket ws, CancellationToken cancellationToken)
        {
            var buffer = new byte[1024 * 1024 * 100];
            var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            return Encoding.UTF8.GetString(buffer, 0, result.Count);
        }

        private async Task<ClientWebSocket?> OpenConnection(CancellationToken cancellationToken)
        {
            using var ws = new ClientWebSocket();
            await ws.ConnectAsync(uri, cancellationToken);

            // Read server greetings (auth_required)
            string welcome = await Receive(ws, cancellationToken);
            Console.WriteLine($"Server: {welcome}");

            // Send token
            await Send(ws, new { type = "auth", access_token = Token }, cancellationToken);

            // Chech authorization information
            string authStatus = await Receive(ws, cancellationToken);
            if (!authStatus.Contains("auth_ok"))
            {
                logger.LogError("Authorization error! Check token.");
                return null;
            }

            logger.LogInformation("Авторизация успешна!");

            return ws;
        }


        public async Task ReadData(CancellationToken cancellationToken)
        {
            using var ws = await OpenConnection(cancellationToken);

            await Send(ws, new { id = 1, type = "config/entity_registry/list" }, cancellationToken);
            var entities = JsonSerializer.Deserialize<HaResponse>(await Receive(ws, cancellationToken));

            await Send(ws, new { id = 2, type = "config/device_registry/list" }, cancellationToken);
            var devices = JsonSerializer.Deserialize<HaResponse>(await Receive(ws, cancellationToken));

            await Send(ws, new { id = 3, type = "config/area_registry/list" }, cancellationToken);
            var areas = JsonSerializer.Deserialize<HaResponse>(await Receive(ws, cancellationToken));

            await Send(ws, new { id = 4, type = "config/floor_registry/list" }, cancellationToken);
            var floors = JsonSerializer.Deserialize<HaResponse>(await Receive(ws, cancellationToken));
        }

    }
}
