namespace Service.Json;

public static class JsonConstants
{
    public static JsonElement EmptyObject { get; }

    static JsonConstants()
    {
        using var doc = JsonDocument.Parse("{}");
        EmptyObject = doc.RootElement.Clone();
    }
}
