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
[JsonSerializable(typeof(WorkspaceOverviewResultDto))]
[JsonSerializable(typeof(CsprojInfoDto))]
[JsonSerializable(typeof(DirectoryRoleDto))]
[JsonSerializable(typeof(FileContextResultDto))]
[JsonSerializable(typeof(DiagnosticsResultDto))]
[JsonSerializable(typeof(DiagnosticItemDto))]
[JsonSerializable(typeof(SymbolOutlineResultDto))]
[JsonSerializable(typeof(TypeOutlineDto))]
[JsonSerializable(typeof(MemberOutlineDto))]
[JsonSerializable(typeof(List<CsprojInfoDto>))]
[JsonSerializable(typeof(List<DirectoryRoleDto>))]
[JsonSerializable(typeof(List<DiagnosticItemDto>))]
[JsonSerializable(typeof(List<TypeOutlineDto>))]
[JsonSerializable(typeof(List<MemberOutlineDto>))]
[JsonSerializable(typeof(List<string>))]
public partial class AstCliJsonContext : JsonSerializerContext
{
}
