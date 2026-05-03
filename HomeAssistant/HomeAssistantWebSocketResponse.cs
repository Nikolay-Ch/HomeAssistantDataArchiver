using System.Text.Json;

namespace HomeAssistantDataArchiver.HomeAssistant;

// Контейнер для ответов WebSocket
public class HomeAssistantWebSocketResponse
{
    public int id { get; set; }
    public string? type { get; set; }
    public bool success { get; set; }
    public JsonElement result { get; set; }
    public HomeAssistantError? error { get; set; }
}

public class HomeAssistantError
{
    public string? code { get; set; }
    public string? message { get; set; }
}
