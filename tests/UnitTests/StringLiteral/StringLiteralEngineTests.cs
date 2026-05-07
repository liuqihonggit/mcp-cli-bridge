using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AstCli.Models;

namespace MyMemoryServer.UnitTests.StringLiteral;

public sealed class StringLiteralEngineTests
{
    private static StringQueryResultDto QueryCode(string code, string? prefix = null, string? filter = null)
    {
        var tree = CSharpSyntaxTree.ParseText(code, path: "test.cs");
        var root = tree.GetCompilationUnitRoot();
        var collector = new TestStringLiteralCollector("test.cs", code, prefix, filter);
        collector.Visit(root);
        var strings = collector.Results;
        var countByKind = strings.GroupBy(s => s.Kind).ToDictionary(g => g.Key, g => g.Count());
        return new StringQueryResultDto
        {
            ProjectPath = "test",
            Strings = strings,
            TotalCount = strings.Count,
            CountByKind = countByKind
        };
    }

    private static string TransformCode(string code, string insertText, int position, string? filter = null)
    {
        var tree = CSharpSyntaxTree.ParseText(code, path: "test.cs");
        var root = tree.GetCompilationUnitRoot();
        var rewriter = new TestStringLiteralRewriter(insertText, position, filter);
        var newRoot = rewriter.Visit(root);
        return newRoot.ToFullString();
    }

    private static string GetStringKind(SyntaxToken token)
    {
        var text = token.Text;
        if (text.StartsWith("\"\"\"", StringComparison.Ordinal))
            return "Raw";
        if (text.StartsWith("@\"", StringComparison.Ordinal))
            return "Verbatim";
        return "Regular";
    }

    private static string GetInterpolatedStringKind(InterpolatedStringExpressionSyntax node)
    {
        var startToken = node.StringStartToken.Text;
        if (startToken.Contains("\"\"\"", StringComparison.Ordinal))
            return "InterpolatedRaw";
        if (startToken.Contains('@'))
            return "VerbatimInterpolated";
        return "Interpolated";
    }

    #region Query Tests

    [Fact]
    public void Query_RegularString_ShouldBeRecognized()
    {
        var result = QueryCode("""var x = "hello";""");
        result.TotalCount.Should().Be(1);
        result.Strings[0].Value.Should().Be("hello");
        result.Strings[0].Kind.Should().Be("Regular");
    }

    [Fact]
    public void Query_VerbatimString_ShouldBeRecognized()
    {
        var result = QueryCode("""var x = @"hello";""");
        result.TotalCount.Should().Be(1);
        result.Strings[0].Value.Should().Be("hello");
        result.Strings[0].Kind.Should().Be("Verbatim");
    }

    [Fact]
    public void Query_RawString_ShouldBeRecognized()
    {
        var code = "var x = \"\"\"hello\"\"\";";
        var result = QueryCode(code);
        result.TotalCount.Should().Be(1);
        result.Strings[0].Value.Should().Be("hello");
        result.Strings[0].Kind.Should().Be("Raw");
    }

    [Fact]
    public void Query_InterpolatedString_ShouldBeRecognized()
    {
        var result = QueryCode("""var x = $"hello {name}";""");
        result.TotalCount.Should().Be(1);
        result.Strings[0].Kind.Should().Be("Interpolated");
    }

    [Fact]
    public void Query_MultipleStrings_ShouldFindAll()
    {
        var code = """
                   var a = "first";
                   var b = "second";
                   var c = @"third";
                   """;
        var result = QueryCode(code);
        result.TotalCount.Should().Be(3);
        result.CountByKind["Regular"].Should().Be(2);
        result.CountByKind["Verbatim"].Should().Be(1);
    }

    [Fact]
    public void Query_WithPrefixFilter_ShouldReturnOnlyMatching()
    {
        var code = """
                   var a = "MCP001_Error";
                   var b = "normal_string";
                   var c = "MCP002_Warning";
                   """;
        var result = QueryCode(code, prefix: "MCP");
        result.TotalCount.Should().Be(2);
        result.Strings.Should().OnlyContain(s => s.Value.StartsWith("MCP"));
    }

    [Fact]
    public void Query_WithContentFilter_ShouldReturnOnlyMatching()
    {
        var code = """
                   var a = "hello world";
                   var b = "foo bar";
                   var c = "hello there";
                   """;
        var result = QueryCode(code, filter: "hello");
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public void Query_CharLiteral_ShouldBeExcluded()
    {
        var result = QueryCode("""var x = 'a';""");
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public void Query_CommentStrings_ShouldBeExcluded()
    {
        var code = """
                   // This is a "comment"
                   var x = "real";
                   """;
        var result = QueryCode(code);
        result.TotalCount.Should().Be(1);
        result.Strings[0].Value.Should().Be("real");
    }

    [Fact]
    public void Query_VerbatimInterpolatedString_ShouldBeRecognized()
    {
        var result = QueryCode("""var x = $@"hello {name}";""");
        result.TotalCount.Should().Be(1);
        result.Strings[0].Kind.Should().Be("VerbatimInterpolated");
    }

    #endregion

    #region Prefix Tests (position=0)

    [Fact]
    public void Prefix_RegularString_ShouldInsertAtBeginning()
    {
        var result = TransformCode("""var x = "hello";""", "PREFIX_", 0);
        result.Should().Contain("\"PREFIX_hello\"");
    }

    [Fact]
    public void Prefix_VerbatimString_ShouldInsertAtBeginning()
    {
        var result = TransformCode("""var x = @"hello";""", "PREFIX_", 0);
        result.Should().Contain("PREFIX_hello");
    }

    [Fact]
    public void Prefix_RawString_ShouldInsertAtBeginning()
    {
        var code = "var x = \"\"\"hello\"\"\";";
        var result = TransformCode(code, "PREFIX_", 0);
        result.Should().Contain("PREFIX_hello");
    }

    [Fact]
    public void Prefix_InterpolatedString_ShouldInsertAtBeginning()
    {
        var result = TransformCode("""var x = $"hello {name}";""", "PREFIX_", 0);
        result.Should().Contain("PREFIX_hello");
    }

    #endregion

    #region Suffix Tests (position=end)

    [Fact]
    public void Suffix_RegularString_ShouldInsertAtEnd()
    {
        var result = TransformCode("""var x = "hello";""", "_SUFFIX", int.MaxValue);
        result.Should().Contain("\"hello_SUFFIX\"");
    }

    [Fact]
    public void Suffix_VerbatimString_ShouldInsertAtEnd()
    {
        var result = TransformCode("""var x = @"hello";""", "_SUFFIX", int.MaxValue);
        result.Should().Contain("hello_SUFFIX");
    }

    [Fact]
    public void Suffix_InterpolatedString_ShouldInsertAtEnd()
    {
        var result = TransformCode("""var x = $"hello {name}";""", "_SUFFIX", int.MaxValue);
        result.Should().Contain("_SUFFIX");
    }

    #endregion

    #region Insert Tests (arbitrary position)

    [Fact]
    public void Insert_RegularString_MiddlePosition_ShouldInsertCorrectly()
    {
        var result = TransformCode("""var x = "hello";""", "_MID_", 3);
        result.Should().Contain("\"hel_MID_lo\"");
    }

    [Fact]
    public void Insert_PositionZero_ShouldBehaveLikePrefix()
    {
        var result = TransformCode("""var x = "hello";""", "PRE_", 0);
        result.Should().Contain("\"PRE_hello\"");
    }

    [Fact]
    public void Insert_PositionAtEnd_ShouldBehaveLikeSuffix()
    {
        var result = TransformCode("""var x = "hello";""", "_END", 5);
        result.Should().Contain("\"hello_END\"");
    }

    [Fact]
    public void Insert_NegativePosition_ShouldClampToZero()
    {
        var result = TransformCode("""var x = "hello";""", "PRE_", -1);
        result.Should().Contain("\"PRE_hello\"");
    }

    [Fact]
    public void Insert_PositionBeyondLength_ShouldClampToEnd()
    {
        var result = TransformCode("""var x = "hello";""", "_END", 100);
        result.Should().Contain("\"hello_END\"");
    }

    [Fact]
    public void Insert_WithFilter_ShouldOnlyModifyMatchingStrings()
    {
        var code = """
                   var a = "target_string";
                   var b = "other_string";
                   """;
        var result = TransformCode(code, "PREFIX_", 0, "target");
        result.Should().Contain("\"PREFIX_target_string\"");
        result.Should().Contain("\"other_string\"");
    }

    [Fact]
    public void Insert_MultipleStrings_ShouldModifyAll()
    {
        var code = """
                   var a = "first";
                   var b = "second";
                   """;
        var result = TransformCode(code, "PRE_", 0);
        result.Should().Contain("\"PRE_first\"");
        result.Should().Contain("\"PRE_second\"");
    }

    [Fact]
    public void Insert_InterpolatedString_MiddlePosition_ShouldSplitTextPart()
    {
        var result = TransformCode("""var x = $"hello world {name}";""", "_MID_", 5);
        result.Should().Contain("hello");
        result.Should().Contain("_MID_");
        result.Should().Contain("world");
    }

    #endregion
}

file sealed class TestStringLiteralCollector : CSharpSyntaxWalker
{
    private readonly string _filePath;
    private readonly string _sourceText;
    private readonly string? _prefix;
    private readonly string? _filter;

    public List<StringLiteralInfoDto> Results { get; } = [];

    public TestStringLiteralCollector(string filePath, string sourceText, string? prefix, string? filter)
    {
        _filePath = filePath;
        _sourceText = sourceText;
        _prefix = prefix;
        _filter = filter;
    }

    public override void VisitLiteralExpression(LiteralExpressionSyntax node)
    {
        if (node.Kind() == SyntaxKind.StringLiteralExpression)
        {
            var valueText = node.Token.ValueText;
            if (MatchesFilter(valueText))
            {
                var lineSpan = node.GetLocation().GetLineSpan();
                Results.Add(new StringLiteralInfoDto
                {
                    Value = valueText,
                    Kind = TestHelper.GetStringKind(node.Token),
                    FilePath = _filePath,
                    Line = lineSpan.StartLinePosition.Line,
                    Column = lineSpan.StartLinePosition.Character,
                    Length = valueText.Length
                });
            }
        }

        base.VisitLiteralExpression(node);
    }

    public override void VisitInterpolatedStringExpression(InterpolatedStringExpressionSyntax node)
    {
        var fullValue = GetInterpolatedValue(node);
        if (MatchesFilter(fullValue))
        {
            var lineSpan = node.GetLocation().GetLineSpan();
            Results.Add(new StringLiteralInfoDto
            {
                Value = fullValue,
                Kind = TestHelper.GetInterpolatedStringKind(node),
                FilePath = _filePath,
                Line = lineSpan.StartLinePosition.Line,
                Column = lineSpan.StartLinePosition.Character,
                Length = fullValue.Length
            });
        }

        base.VisitInterpolatedStringExpression(node);
    }

    private bool MatchesFilter(string value)
    {
        if (_prefix != null && !value.StartsWith(_prefix, StringComparison.OrdinalIgnoreCase))
            return false;
        if (_filter != null && !value.Contains(_filter, StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    private static string GetInterpolatedValue(InterpolatedStringExpressionSyntax node)
    {
        var sb = new StringBuilder();
        foreach (var content in node.Contents)
        {
            if (content is InterpolatedStringTextSyntax textPart)
                sb.Append(textPart.TextToken.ValueText);
            else if (content is InterpolationSyntax)
                sb.Append("{}");
        }
        return sb.ToString();
    }
}

file sealed class TestStringLiteralRewriter : CSharpSyntaxRewriter
{
    private readonly string _insertText;
    private readonly int _position;
    private readonly string? _filter;

    public TestStringLiteralRewriter(string insertText, int position, string? filter)
    {
        _insertText = insertText;
        _position = position;
        _filter = filter;
    }

    public override SyntaxNode? VisitLiteralExpression(LiteralExpressionSyntax node)
    {
        if (node.Kind() != SyntaxKind.StringLiteralExpression)
            return base.VisitLiteralExpression(node);

        var valueText = node.Token.ValueText;
        if (_filter != null && !valueText.Contains(_filter, StringComparison.OrdinalIgnoreCase))
            return base.VisitLiteralExpression(node);

        var position = Math.Clamp(_position, 0, valueText.Length);
        var newValue = string.Concat(valueText.AsSpan(0, position), _insertText, valueText.AsSpan(position));

        var kind = TestHelper.GetStringKind(node.Token);
        SyntaxToken newToken = kind switch
        {
            "Verbatim" => TestHelper.CreateVerbatimLiteral(newValue),
            "Raw" => TestHelper.CreateRawLiteral(node.Token, newValue),
            _ => SyntaxFactory.Literal(newValue)
        };

        return node.WithToken(newToken);
    }

    public override SyntaxNode? VisitInterpolatedStringExpression(InterpolatedStringExpressionSyntax node)
    {
        var fullValue = GetInterpolatedValue(node);
        if (_filter != null && !fullValue.Contains(_filter, StringComparison.OrdinalIgnoreCase))
            return base.VisitInterpolatedStringExpression(node);

        var position = Math.Clamp(_position, 0, fullValue.Length);

        if (position == 0)
        {
            var newContents = InsertAtBeginning(node);
            return node.WithContents(SyntaxFactory.List(newContents));
        }

        if (position >= fullValue.Length)
        {
            var newContents = InsertAtEnd(node);
            return node.WithContents(SyntaxFactory.List(newContents));
        }

        var splitContents = InsertAtMiddle(node, position);
        if (splitContents != null)
            return node.WithContents(SyntaxFactory.List(splitContents));

        return base.VisitInterpolatedStringExpression(node);
    }

    private List<InterpolatedStringContentSyntax> InsertAtBeginning(InterpolatedStringExpressionSyntax node)
    {
        var newContents = new List<InterpolatedStringContentSyntax>();
        var textToken = SyntaxFactory.Token(
            SyntaxFactory.TriviaList(), SyntaxKind.InterpolatedStringTextToken, _insertText, _insertText, SyntaxFactory.TriviaList());
        newContents.Add(SyntaxFactory.InterpolatedStringText(textToken));
        foreach (var content in node.Contents)
            newContents.Add(content);
        return newContents;
    }

    private List<InterpolatedStringContentSyntax> InsertAtEnd(InterpolatedStringExpressionSyntax node)
    {
        var newContents = new List<InterpolatedStringContentSyntax>();
        foreach (var content in node.Contents)
            newContents.Add(content);
        var textToken = SyntaxFactory.Token(
            SyntaxFactory.TriviaList(), SyntaxKind.InterpolatedStringTextToken, _insertText, _insertText, SyntaxFactory.TriviaList());
        newContents.Add(SyntaxFactory.InterpolatedStringText(textToken));
        return newContents;
    }

    private List<InterpolatedStringContentSyntax>? InsertAtMiddle(InterpolatedStringExpressionSyntax node, int targetPosition)
    {
        var newContents = new List<InterpolatedStringContentSyntax>();
        var currentOffset = 0;
        var inserted = false;

        foreach (var content in node.Contents)
        {
            if (inserted)
            {
                newContents.Add(content);
                continue;
            }

            if (content is InterpolatedStringTextSyntax textPart)
            {
                var textValue = textPart.TextToken.ValueText;
                var textLength = textValue.Length;

                if (currentOffset + textLength > targetPosition)
                {
                    var localPos = targetPosition - currentOffset;
                    var beforeText = textValue[..localPos];
                    var afterText = textValue[localPos..];

                    if (beforeText.Length > 0)
                    {
                        var beforeToken = SyntaxFactory.Token(
                            SyntaxFactory.TriviaList(), SyntaxKind.InterpolatedStringTextToken, beforeText, beforeText, SyntaxFactory.TriviaList());
                        newContents.Add(SyntaxFactory.InterpolatedStringText(beforeToken));
                    }

                    var insertToken = SyntaxFactory.Token(
                        SyntaxFactory.TriviaList(), SyntaxKind.InterpolatedStringTextToken, _insertText, _insertText, SyntaxFactory.TriviaList());
                    newContents.Add(SyntaxFactory.InterpolatedStringText(insertToken));

                    if (afterText.Length > 0)
                    {
                        var afterToken = SyntaxFactory.Token(
                            SyntaxFactory.TriviaList(), SyntaxKind.InterpolatedStringTextToken, afterText, afterText, SyntaxFactory.TriviaList());
                        newContents.Add(SyntaxFactory.InterpolatedStringText(afterToken));
                    }

                    inserted = true;
                }
                else
                {
                    newContents.Add(content);
                }

                currentOffset += textLength;
            }
            else if (content is InterpolationSyntax)
            {
                newContents.Add(content);
                currentOffset = targetPosition;
                inserted = true;
            }
        }

        return newContents;
    }

    private static string GetInterpolatedValue(InterpolatedStringExpressionSyntax node)
    {
        var sb = new StringBuilder();
        foreach (var content in node.Contents)
        {
            if (content is InterpolatedStringTextSyntax textPart)
                sb.Append(textPart.TextToken.ValueText);
            else if (content is InterpolationSyntax)
                sb.Append("{}");
        }
        return sb.ToString();
    }
}

file static class TestHelper
{
    public static string GetStringKind(SyntaxToken token)
    {
        var text = token.Text;
        if (text.StartsWith("\"\"\"", StringComparison.Ordinal))
            return "Raw";
        if (text.StartsWith("@\"", StringComparison.Ordinal))
            return "Verbatim";
        return "Regular";
    }

    public static string GetInterpolatedStringKind(InterpolatedStringExpressionSyntax node)
    {
        var startToken = node.StringStartToken.Text;
        if (startToken.Contains("\"\"\"", StringComparison.Ordinal))
            return "InterpolatedRaw";
        if (startToken.Contains('@'))
            return "VerbatimInterpolated";
        return "Interpolated";
    }

    public static SyntaxToken CreateVerbatimLiteral(string value)
    {
        var escaped = value.Replace("\"", "\"\"");
        var literalText = "@\"" + escaped + "\"";
        return SyntaxFactory.Literal(
            SyntaxFactory.TriviaList(),
            literalText,
            value,
            SyntaxFactory.TriviaList());
    }

    public static SyntaxToken CreateRawLiteral(SyntaxToken originalToken, string newValue)
    {
        var originalText = originalToken.Text;
        var delimiterEnd = originalText.IndexOf('"');
        var delimiterStart = originalText.LastIndexOf('"');

        if (delimiterEnd < 0 || delimiterStart <= delimiterEnd)
            return SyntaxFactory.Literal(newValue);

        var delimiter = originalText[delimiterEnd..(delimiterStart + 1)];
        var rawText = delimiter + newValue + delimiter;

        return SyntaxFactory.Literal(
            SyntaxFactory.TriviaList(),
            rawText,
            newValue,
            SyntaxFactory.TriviaList());
    }
}
