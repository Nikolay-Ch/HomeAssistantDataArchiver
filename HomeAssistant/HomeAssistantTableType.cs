namespace HomeAssistantDataArchiver.HomeAssistant;

public enum HomeAssistantTableType
{
    Entity,
    Device,
    Area,
    Floor,
    State
}

public static class HomeAssistantTableTypeExtensions
{
    public static bool IsRequestedByApi(this HomeAssistantTableType tableType) =>
        tableType switch
        {
            HomeAssistantTableType.Entity or
            HomeAssistantTableType.Device or
            HomeAssistantTableType.Area or
            HomeAssistantTableType.Floor => true,
            _ => false
        };
}