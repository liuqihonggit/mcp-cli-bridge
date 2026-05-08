using AstCli.Services;

namespace MyMemoryServer.UnitTests.LanguageProvider;

public sealed class LanguageProviderRegistryTests
{
    [Fact]
    public void GetProvider_CSharpByName_ReturnsSupportedProvider()
    {
        var provider = LanguageProviderRegistry.GetProvider("csharp");

        provider.Language.IsSupported.Should().BeTrue();
        provider.Language.Name.Should().Be("csharp");
        provider.Language.FileExtension.Should().Be(".cs");
        provider.FileSearchPattern.Should().Be("*.cs");
    }

    [Fact]
    public void GetProvider_CSharpByShortName_ReturnsSupportedProvider()
    {
        var provider = LanguageProviderRegistry.GetProvider("cs");

        provider.Language.IsSupported.Should().BeTrue();
        provider.Language.Name.Should().Be("csharp");
    }

    [Fact]
    public void GetProvider_CSharpBySymbol_ReturnsSupportedProvider()
    {
        var provider = LanguageProviderRegistry.GetProvider("c#");

        provider.Language.IsSupported.Should().BeTrue();
        provider.Language.Name.Should().Be("csharp");
    }

    [Fact]
    public void GetProvider_NullOrDefault_ReturnsCSharpProvider()
    {
        var nullProvider = LanguageProviderRegistry.GetProvider(null);
        var emptyProvider = LanguageProviderRegistry.GetProvider("");

        nullProvider.Language.IsSupported.Should().BeTrue();
        nullProvider.Language.Name.Should().Be("csharp");
        emptyProvider.Language.IsSupported.Should().BeTrue();
    }

    [Fact]
    public void GetProvider_PythonByExtension_ReturnsUnsupportedProvider()
    {
        var provider = LanguageProviderRegistry.GetProvider(".py");

        provider.Language.IsSupported.Should().BeFalse();
        provider.Language.Name.Should().Be("python");
        provider.Language.DisplayName.Should().Be("Python");
    }

    [Fact]
    public void GetProvider_TypeScriptByExtension_ReturnsUnsupportedProvider()
    {
        var provider = LanguageProviderRegistry.GetProvider(".ts");

        provider.Language.IsSupported.Should().BeFalse();
        provider.Language.Name.Should().Be("typescript");
    }

    [Fact]
    public void GetProvider_JavaByExtension_ReturnsUnsupportedProvider()
    {
        var provider = LanguageProviderRegistry.GetProvider(".java");

        provider.Language.IsSupported.Should().BeFalse();
        provider.Language.Name.Should().Be("java");
    }

    [Fact]
    public void GetProvider_UnknownLanguage_ReturnsUnsupportedProvider()
    {
        var provider = LanguageProviderRegistry.GetProvider("brainfuck");

        provider.Language.IsSupported.Should().BeFalse();
        provider.Language.Name.Should().Be("brainfuck");
    }

    [Fact]
    public void GetProvider_CaseInsensitive_ReturnsCorrectProvider()
    {
        var upperProvider = LanguageProviderRegistry.GetProvider("CSHARP");
        var mixedProvider = LanguageProviderRegistry.GetProvider("CSharp");

        upperProvider.Language.IsSupported.Should().BeTrue();
        mixedProvider.Language.IsSupported.Should().BeTrue();
    }

    [Fact]
    public void GetSupportedLanguages_ContainsCSharp()
    {
        var supported = LanguageProviderRegistry.GetSupportedLanguages();

        supported.Should().Contain("csharp");
    }

    [Fact]
    public void Register_NewSupportedLanguage_ReturnsProvider()
    {
        LanguageProviderRegistry.Register("fsharp", static () => new TestFSharpLanguageProvider());

        var provider = LanguageProviderRegistry.GetProvider("fsharp");
        provider.Language.IsSupported.Should().BeTrue();
        provider.Language.Name.Should().Be("fsharp");

        LanguageProviderRegistry.Unregister("fsharp");
    }

    [Fact]
    public void Register_OverwriteExisting_ReplacesProvider()
    {
        LanguageProviderRegistry.Register("csharp", static () => new TestFSharpLanguageProvider());

        var provider = LanguageProviderRegistry.GetProvider("csharp");
        provider.Language.Name.Should().Be("fsharp");

        LanguageProviderRegistry.Register("csharp", static () => new CSharpLanguageProvider());
    }

    private sealed class TestFSharpLanguageProvider : ILanguageProvider
    {
        public LanguageInfo Language => new()
        {
            Name = "fsharp",
            DisplayName = "F#",
            FileExtension = ".fs",
            IsSupported = true
        };
        public string FileSearchPattern => "*.fs";
        public IReadOnlyList<string> ProjectFilePatterns => ["*.fsproj"];
    }
}
