namespace AstCli.Models;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AstCliRequest))]
[JsonSerializable(typeof(SymbolInfoDto))]
[JsonSerializable(typeof(ReferenceLocationDto))]
[JsonSerializable(typeof(QuerySymbolResultDto))]
[JsonSerializable(typeof(FindReferencesResultDto))]
[JsonSerializable(typeof(RenameSymbolResultDto))]
[JsonSerializable(typeof(ReplaceSymbolResultDto))]
[JsonSerializable(typeof(GetSymbolInfoResultDto))]
[JsonSerializable(typeof(List<SymbolInfoDto>))]
[JsonSerializable(typeof(List<ReferenceLocationDto>))]
[JsonSerializable(typeof(List<string>))]
public partial class AstCliJsonContext : JsonSerializerContext
{
}
