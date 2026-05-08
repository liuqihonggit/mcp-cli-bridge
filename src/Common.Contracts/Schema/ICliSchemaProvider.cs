namespace Common.Contracts.Schema;

public interface ICliSchemaProvider
{
    static abstract JsonElement GetSchema();
}
