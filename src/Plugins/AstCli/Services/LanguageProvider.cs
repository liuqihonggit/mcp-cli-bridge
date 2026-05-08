namespace AstCli.Services;

internal sealed class LanguageInfo
{
    public string Name { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string FileExtension { get; init; } = string.Empty;
    public bool IsSupported { get; init; }
}

internal interface ILanguageProvider
{
    LanguageInfo Language { get; }
    string FileSearchPattern { get; }
    IReadOnlyList<string> ProjectFilePatterns { get; }
}

internal sealed class CSharpLanguageProvider : ILanguageProvider
{
    private static readonly IReadOnlyList<string> s_projectFilePatterns =
        ["*.csproj"];

    public LanguageInfo Language => new()
    {
        Name = "csharp",
        DisplayName = "C#",
        FileExtension = ".cs",
        IsSupported = true
    };

    public string FileSearchPattern => "*.cs";
    public IReadOnlyList<string> ProjectFilePatterns => s_projectFilePatterns;
}

internal sealed class UnsupportedLanguageProvider : ILanguageProvider
{
    private static readonly IReadOnlyList<string> s_emptyPatterns = [];

    public LanguageInfo Language { get; }
    public string FileSearchPattern => "";
    public IReadOnlyList<string> ProjectFilePatterns => s_emptyPatterns;

    public UnsupportedLanguageProvider(string name, string displayName, string fileExtension)
    {
        Language = new LanguageInfo
        {
            Name = name,
            DisplayName = displayName,
            FileExtension = fileExtension,
            IsSupported = false
        };
    }
}

internal static class LanguageProviderRegistry
{
    private static readonly Dictionary<string, Func<ILanguageProvider>> s_providers = new()
    {
        { "csharp", static () => new CSharpLanguageProvider() },
        { "cs", static () => new CSharpLanguageProvider() },
        { "c#", static () => new CSharpLanguageProvider() },
    };

    private static readonly Dictionary<string, (string Name, string DisplayName)> s_knownLanguages = new()
    {
        { ".py", ("python", "Python") },
        { ".pyw", ("python", "Python") },
        { ".ts", ("typescript", "TypeScript") },
        { ".tsx", ("typescript", "TypeScript (JSX)") },
        { ".js", ("javascript", "JavaScript") },
        { ".jsx", ("javascript", "JavaScript (JSX)") },
        { ".mjs", ("javascript", "JavaScript (ES Module)") },
        { ".java", ("java", "Java") },
        { ".go", ("go", "Go") },
        { ".rs", ("rust", "Rust") },
        { ".cpp", ("cpp", "C++") },
        { ".c", ("c", "C") },
        { ".h", ("cpp", "C/C++ Header") },
        { ".hpp", ("cpp", "C++ Header") },
        { ".rb", ("ruby", "Ruby") },
        { ".php", ("php", "PHP") },
        { ".swift", ("swift", "Swift") },
        { ".kt", ("kotlin", "Kotlin") },
        { ".kts", ("kotlin", "Kotlin Script") },
        { ".scala", ("scala", "Scala") },
        { ".fs", ("fsharp", "F#") },
        { ".fsx", ("fsharp", "F# Script") },
        { ".vb", ("vb", "Visual Basic") },
        { ".lua", ("lua", "Lua") },
        { ".r", ("r", "R") },
        { ".dart", ("dart", "Dart") },
        { ".elixir", ("elixir", "Elixir") },
        { ".ex", ("elixir", "Elixir") },
        { ".erl", ("erlang", "Erlang") },
        { ".zig", ("zig", "Zig") },
        { ".nim", ("nim", "Nim") },
        { ".perl", ("perl", "Perl") },
        { ".pl", ("perl", "Perl") },
        { ".pm", ("perl", "Perl Module") },
    };

    public static ILanguageProvider GetProvider(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return new CSharpLanguageProvider();

        var normalized = language.Trim().ToLowerInvariant();

        if (s_providers.TryGetValue(normalized, out var factory))
            return factory();

        if (s_knownLanguages.TryGetValue(normalized, out var langInfo))
        {
            if (s_providers.TryGetValue(langInfo.Name, out var extFactory))
                return extFactory();

            return new UnsupportedLanguageProvider(langInfo.Name, langInfo.DisplayName, normalized);
        }

        return new UnsupportedLanguageProvider(normalized, normalized, "");
    }

    public static List<string> GetSupportedLanguages()
    {
        return s_providers.Keys.ToList();
    }

    public static void Register(string key, Func<ILanguageProvider> factory)
    {
        s_providers[key.ToLowerInvariant()] = factory;
    }

    internal static void Unregister(string key)
    {
        s_providers.Remove(key.ToLowerInvariant());
    }
}
