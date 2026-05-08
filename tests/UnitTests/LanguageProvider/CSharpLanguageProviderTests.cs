using AstCli.Services;

namespace MyMemoryServer.UnitTests.LanguageProvider;

public sealed class CSharpLanguageProviderTests
{
    [Fact]
    public void CSharpLanguageProvider_FileSearchPattern_ShouldBeCs()
    {
        var provider = new CSharpLanguageProvider();
        provider.FileSearchPattern.Should().Be("*.cs");
    }

    [Fact]
    public void CSharpLanguageProvider_ProjectFilePatterns_ShouldContainCsproj()
    {
        var provider = new CSharpLanguageProvider();
        provider.ProjectFilePatterns.Should().Contain("*.csproj");
    }

    [Fact]
    public void CSharpLanguageProvider_ProjectFilePatterns_CountIsOne()
    {
        var provider = new CSharpLanguageProvider();
        provider.ProjectFilePatterns.Should().HaveCount(1);
        provider.ProjectFilePatterns[0].Should().Be("*.csproj");
    }

    [Fact]
    public void UnsupportedLanguageProvider_ProjectFilePatterns_ShouldBeEmpty()
    {
        var provider = new UnsupportedLanguageProvider("python", "Python", ".py");
        provider.ProjectFilePatterns.Should().BeEmpty();
    }
}
