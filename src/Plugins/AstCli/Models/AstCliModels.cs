namespace AstCli.Models;

public sealed class AstCliRequest
{
    [JsonPropertyName("command")]
    public string Command { get; set; } = string.Empty;

    [JsonPropertyName("projectPath")]
    public string? ProjectPath { get; set; }

    [JsonPropertyName("filePath")]
    public string? FilePath { get; set; }

    [JsonPropertyName("symbolName")]
    public string? SymbolName { get; set; }

    [JsonPropertyName("newName")]
    public string? NewName { get; set; }

    [JsonPropertyName("lineNumber")]
    public int LineNumber { get; set; }

    [JsonPropertyName("columnNumber")]
    public int ColumnNumber { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    [JsonPropertyName("insertText")]
    public string? InsertText { get; set; }

    [JsonPropertyName("position")]
    public int Position { get; set; }

    [JsonPropertyName("prefix")]
    public string? Prefix { get; set; }

    [JsonPropertyName("filter")]
    public string? Filter { get; set; }

    [JsonPropertyName("dryRun")]
    public bool DryRun { get; set; }

    [JsonPropertyName("pattern")]
    public string? Pattern { get; set; }

    [JsonPropertyName("replacement")]
    public string? Replacement { get; set; }

    [JsonPropertyName("useRegex")]
    public bool UseRegex { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("paramType")]
    public string? ParamType { get; set; }

    [JsonPropertyName("paramName")]
    public string? ParamName { get; set; }

    [JsonPropertyName("addConfigureAwait")]
    public bool AddConfigureAwait { get; set; }
}

public sealed class SymbolInfoDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("fullName")]
    public string? FullName { get; set; }

    [JsonPropertyName("containingNamespace")]
    public string? ContainingNamespace { get; set; }

    [JsonPropertyName("filePath")]
    public string? FilePath { get; set; }

    [JsonPropertyName("line")]
    public int Line { get; set; }

    [JsonPropertyName("column")]
    public int Column { get; set; }
}

public sealed class ReferenceLocationDto
{
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    [JsonPropertyName("line")]
    public int Line { get; set; }

    [JsonPropertyName("column")]
    public int Column { get; set; }

    [JsonPropertyName("symbolName")]
    public string SymbolName { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("context")]
    public string? Context { get; set; }
}

public sealed class QuerySymbolResultDto
{
    [JsonPropertyName("symbolName")]
    public string SymbolName { get; set; } = string.Empty;

    [JsonPropertyName("symbols")]
    public List<SymbolInfoDto> Symbols { get; set; } = [];

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }
}

public sealed class FindReferencesResultDto
{
    [JsonPropertyName("symbolName")]
    public string SymbolName { get; set; } = string.Empty;

    [JsonPropertyName("symbol")]
    public SymbolInfoDto? Symbol { get; set; }

    [JsonPropertyName("references")]
    public List<ReferenceLocationDto> References { get; set; } = [];

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }
}

public sealed class RenameSymbolResultDto
{
    [JsonPropertyName("oldName")]
    public string OldName { get; set; } = string.Empty;

    [JsonPropertyName("newName")]
    public string NewName { get; set; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("modifiedFiles")]
    public List<string> ModifiedFiles { get; set; } = [];

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

public sealed class ReplaceSymbolResultDto
{
    [JsonPropertyName("oldName")]
    public string OldName { get; set; } = string.Empty;

    [JsonPropertyName("newName")]
    public string NewName { get; set; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("modifiedFileCount")]
    public int ModifiedFileCount { get; set; }

    [JsonPropertyName("modifiedFiles")]
    public List<string> ModifiedFiles { get; set; } = [];

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

public sealed class GetSymbolInfoResultDto
{
    [JsonPropertyName("symbol")]
    public SymbolInfoDto? Symbol { get; set; }

    [JsonPropertyName("found")]
    public bool Found { get; set; }
}

public sealed class WorkspaceOverviewResultDto
{
    [JsonPropertyName("projectPath")]
    public string ProjectPath { get; set; } = string.Empty;

    [JsonPropertyName("totalFiles")]
    public int TotalFiles { get; set; }

    [JsonPropertyName("totalLines")]
    public int TotalLines { get; set; }

    [JsonPropertyName("namespaces")]
    public List<string> Namespaces { get; set; } = [];

    [JsonPropertyName("csprojFiles")]
    public List<CsprojInfoDto> CsprojFiles { get; set; } = [];

    [JsonPropertyName("directoryRoles")]
    public List<DirectoryRoleDto> DirectoryRoles { get; set; } = [];

    [JsonPropertyName("entryPoints")]
    public List<string> EntryPoints { get; set; } = [];
}

public sealed class CsprojInfoDto
{
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    [JsonPropertyName("projectReferences")]
    public List<string> ProjectReferences { get; set; } = [];
}

public sealed class DirectoryRoleDto
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;
}

public sealed class FileContextResultDto
{
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    [JsonPropertyName("systemUsings")]
    public List<string> SystemUsings { get; set; } = [];

    [JsonPropertyName("projectUsings")]
    public List<string> ProjectUsings { get; set; } = [];

    [JsonPropertyName("referencedSymbols")]
    public List<SymbolInfoDto> ReferencedSymbols { get; set; } = [];

    [JsonPropertyName("sameNamespaceSymbols")]
    public List<SymbolInfoDto> SameNamespaceSymbols { get; set; } = [];

    [JsonPropertyName("reverseDependencies")]
    public List<string> ReverseDependencies { get; set; } = [];
}

public sealed class DiagnosticsResultDto
{
    [JsonPropertyName("projectPath")]
    public string ProjectPath { get; set; } = string.Empty;

    [JsonPropertyName("errors")]
    public List<DiagnosticItemDto> Errors { get; set; } = [];

    [JsonPropertyName("totalErrorCount")]
    public int TotalErrorCount { get; set; }

    [JsonPropertyName("totalWarningCount")]
    public int TotalWarningCount { get; set; }
}

public sealed class DiagnosticItemDto
{
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    [JsonPropertyName("line")]
    public int Line { get; set; }

    [JsonPropertyName("column")]
    public int Column { get; set; }

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

public sealed class SymbolOutlineResultDto
{
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    [JsonPropertyName("types")]
    public List<TypeOutlineDto> Types { get; set; } = [];
}

public sealed class TypeOutlineDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("accessibility")]
    public string Accessibility { get; set; } = string.Empty;

    [JsonPropertyName("startLine")]
    public int StartLine { get; set; }

    [JsonPropertyName("endLine")]
    public int EndLine { get; set; }

    [JsonPropertyName("members")]
    public List<MemberOutlineDto> Members { get; set; } = [];
}

public sealed class MemberOutlineDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("accessibility")]
    public string Accessibility { get; set; } = string.Empty;

    [JsonPropertyName("line")]
    public int Line { get; set; }
}

public sealed class StringLiteralInfoDto
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    [JsonPropertyName("line")]
    public int Line { get; set; }

    [JsonPropertyName("column")]
    public int Column { get; set; }

    [JsonPropertyName("length")]
    public int Length { get; set; }

    [JsonPropertyName("context")]
    public string? Context { get; set; }
}

public sealed class StringQueryResultDto
{
    [JsonPropertyName("projectPath")]
    public string ProjectPath { get; set; } = string.Empty;

    [JsonPropertyName("strings")]
    public List<StringLiteralInfoDto> Strings { get; set; } = [];

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("countByKind")]
    public Dictionary<string, int> CountByKind { get; set; } = [];
}

public sealed class StringInsertResultDto
{
    [JsonPropertyName("insertText")]
    public string InsertText { get; set; } = string.Empty;

    [JsonPropertyName("position")]
    public int Position { get; set; }

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("transformedCount")]
    public int TransformedCount { get; set; }

    [JsonPropertyName("modifiedFiles")]
    public List<string> ModifiedFiles { get; set; } = [];

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

public sealed class StringReplaceResultDto
{
    [JsonPropertyName("pattern")]
    public string Pattern { get; set; } = string.Empty;

    [JsonPropertyName("replacement")]
    public string Replacement { get; set; } = string.Empty;

    [JsonPropertyName("useRegex")]
    public bool UseRegex { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("transformedCount")]
    public int TransformedCount { get; set; }

    [JsonPropertyName("modifiedFiles")]
    public List<string> ModifiedFiles { get; set; } = [];

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

public sealed class UnsupportedLanguageResultDto
{
    [JsonPropertyName("detectedLanguage")]
    public string DetectedLanguage { get; set; } = string.Empty;

    [JsonPropertyName("detectedLanguageDisplayName")]
    public string DetectedLanguageDisplayName { get; set; } = string.Empty;

    [JsonPropertyName("supportedLanguages")]
    public List<string> SupportedLanguages { get; set; } = [];

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

public sealed class AsyncMigrationResultDto
{
    [JsonPropertyName("command")]
    public string Command { get; set; } = string.Empty;

    [JsonPropertyName("symbolName")]
    public string SymbolName { get; set; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("modifiedFiles")]
    public List<string> ModifiedFiles { get; set; } = [];

    [JsonPropertyName("modifiedFileCount")]
    public int ModifiedFileCount { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
