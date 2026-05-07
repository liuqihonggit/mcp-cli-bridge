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
