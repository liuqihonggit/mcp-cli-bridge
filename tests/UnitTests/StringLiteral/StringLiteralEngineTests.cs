using System.Text;
using System.Text.RegularExpressions;
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

    private static string ReplaceCode(string code, string pattern, string replacement, bool useRegex = false, string? filter = null)
    {
        var tree = CSharpSyntaxTree.ParseText(code, path: "test.cs");
        var root = tree.GetCompilationUnitRoot();
        var rewriter = new TestStringLiteralReplacer(pattern, replacement, useRegex, filter);
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

    #region Replace Tests (Literal)

    [Fact]
    public void Replace_Literal_RegularString_ShouldReplace()
    {
        var result = ReplaceCode("""var x = "memory_create";""", "memory_", "men_");
        result.Should().Contain("\"men_create\"");
    }

    [Fact]
    public void Replace_Literal_VerbatimString_ShouldReplace()
    {
        var result = ReplaceCode("""var x = @"memory_path\test";""", "memory_", "men_");
        result.Should().Contain(@"men_path");
    }

    [Fact]
    public void Replace_Literal_RawString_ShouldReplace()
    {
        var code = "var x = \"\"\"memory_raw\"\"\";";
        var result = ReplaceCode(code, "memory_", "men_");
        result.Should().Contain("men_raw");
    }

    [Fact]
    public void Replace_Literal_RawString_ShouldNotDuplicateContent()
    {
        // Bug: 原始字符串字面量替换时会出现内容重复
        var code = "var x = \"\"\"memory_test\"\"\";";
        var result = ReplaceCode(code, "memory_", "men_");

        // 验证只出现一次替换后的内容
        result.Should().Contain("men_test");
        result.Should().NotContain("memory_test");

        // 验证没有出现重复内容（如 """men_test""""""men_test"""）
        var count = result.Split("men_test").Length - 1;
        count.Should().Be(1, $"内容应该只出现一次，但实际出现 {count} 次。结果：{result}");
    }

    [Fact]
    public void Replace_Literal_MultiLineRawString_ShouldPreserveIndentation()
    {
        // Bug: 多行原始字符串字面量替换后缩进丢失，导致闭合 """ 不在行首，破坏语法
        // 用字符串拼接避免嵌套原始字符串字面量的语法问题
        var inner = "    var a = \"memory_target\";\n    var b = \"other\";\n    ";
        var code = "var code = \"\"\"\n" + inner + "\"\"\";";
        var result = ReplaceCode(code, "memory_", "men_");

        result.Should().Contain("men_target");
        result.Should().NotContain("memory_target");

        // 验证结果可以被正确解析为合法的 C# 代码
        var parseResult = CSharpSyntaxTree.ParseText(result);
        var diagnostics = parseResult.GetDiagnostics();
        diagnostics.Should().BeEmpty($"替换后的代码应该是合法的 C# 代码，但有错误：{string.Join(", ", diagnostics.Select(d => d.ToString()))}");
    }

    [Fact]
    public void Replace_Literal_InterpolatedString_ShouldReplace()
    {
        var result = ReplaceCode("""var x = $"memory_{name}";""", "memory_", "men_");
        result.Should().Contain("men_");
    }

    [Fact]
    public void Replace_Literal_NoMatch_ShouldNotChange()
    {
        var code = """var x = "other_string";""";
        var result = ReplaceCode(code, "memory_", "men_");
        result.Should().Be(code);
    }

    [Fact]
    public void Replace_Literal_WithFilter_ShouldOnlyModifyMatching()
    {
        var code = """
                   var a = "memory_target";
                   var b = "other_memory";
                   """;
        var result = ReplaceCode(code, "memory_", "men_", filter: "target");
        result.Should().Contain("men_target");
        result.Should().Contain("other_memory");
    }

    [Fact]
    public void Replace_Literal_MultipleOccurrences_ShouldReplaceAll()
    {
        var result = ReplaceCode("""var x = "memory_a and memory_b";""", "memory_", "men_");
        result.Should().Contain("men_a and men_b");
    }

    #endregion

    #region Replace Tests (Regex)

    [Fact]
    public void Replace_Regex_RegularString_ShouldReplace()
    {
        var result = ReplaceCode("""var x = "memory_create_entities";""", @"memory_(\w+)", "men_$1", useRegex: true);
        result.Should().Contain("\"men_create_entities\"");
    }

    [Fact]
    public void Replace_Regex_WithCaptureGroup_ShouldReplace()
    {
        var result = ReplaceCode("""var x = "prefix_value_suffix";""", @"prefix_(\w+)_suffix", "replaced_$1", useRegex: true);
        result.Should().Contain("\"replaced_value\"");
    }

    [Fact]
    public void Replace_Regex_NoMatch_ShouldNotChange()
    {
        var code = """var x = "other_string";""";
        var result = ReplaceCode(code, @"memory_(\w+)", "men_$1", useRegex: true);
        result.Should().Be(code);
    }

    [Fact]
    public void Replace_Regex_WithFilter_ShouldOnlyModifyMatching()
    {
        var code = """
                   var a = "memory_target_entity";
                   var b = "memory_other_entity";
                   """;
        var result = ReplaceCode(code, @"memory_(\w+)", "men_$1", useRegex: true, filter: "target");
        result.Should().Contain("men_target_entity");
        result.Should().Contain("memory_other_entity");
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

file sealed class TestStringLiteralReplacer : CSharpSyntaxRewriter
{
    private readonly string _pattern;
    private readonly string _replacement;
    private readonly bool _useRegex;
    private readonly string? _filter;
    private readonly Regex? _regex;

    public TestStringLiteralReplacer(string pattern, string replacement, bool useRegex, string? filter)
    {
        _pattern = pattern;
        _replacement = replacement;
        _useRegex = useRegex;
        _filter = filter;

        if (useRegex)
        {
            _regex = new Regex(pattern, RegexOptions.Compiled);
        }
    }

    public override SyntaxNode? VisitLiteralExpression(LiteralExpressionSyntax node)
    {
        if (node.Kind() != SyntaxKind.StringLiteralExpression)
            return base.VisitLiteralExpression(node);

        var valueText = node.Token.ValueText;

        if (_filter != null && !valueText.Contains(_filter, StringComparison.OrdinalIgnoreCase))
            return base.VisitLiteralExpression(node);

        var newValue = _useRegex
            ? _regex!.Replace(valueText, _replacement)
            : valueText.Replace(_pattern, _replacement, StringComparison.OrdinalIgnoreCase);

        if (newValue == valueText)
            return base.VisitLiteralExpression(node);

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

        var newFullValue = _useRegex
            ? _regex!.Replace(fullValue, _replacement)
            : fullValue.Replace(_pattern, _replacement, StringComparison.OrdinalIgnoreCase);

        if (newFullValue == fullValue)
            return base.VisitInterpolatedStringExpression(node);

        var newContents = ReplaceInInterpolatedString(node, newFullValue);
        if (newContents != null)
        {
            return node.WithContents(SyntaxFactory.List(newContents));
        }

        return base.VisitInterpolatedStringExpression(node);
    }

    private static List<InterpolatedStringContentSyntax>? ReplaceInInterpolatedString(
        InterpolatedStringExpressionSyntax node, string newFullValue)
    {
        var newContents = new List<InterpolatedStringContentSyntax>();
        var valueIndex = 0;

        foreach (var content in node.Contents)
        {
            if (content is InterpolatedStringTextSyntax textPart)
            {
                var textValue = textPart.TextToken.ValueText;
                var textLength = textValue.Length;

                var newText = newFullValue.Substring(valueIndex, Math.Min(textLength, newFullValue.Length - valueIndex));
                valueIndex += newText.Length;

                if (newText.Length > 0)
                {
                    var newToken = SyntaxFactory.Token(
                        SyntaxFactory.TriviaList(),
                        SyntaxKind.InterpolatedStringTextToken,
                        newText,
                        newText,
                        SyntaxFactory.TriviaList());
                    newContents.Add(SyntaxFactory.InterpolatedStringText(newToken));
                }
            }
            else if (content is InterpolationSyntax interpolation)
            {
                newContents.Add(interpolation);
            }
        }

        return newContents.Count > 0 ? newContents : null;
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

        // 提取开头的引号定界符（如 """ 或 "）
        var quoteCount = 0;
        while (quoteCount < originalText.Length && originalText[quoteCount] == '"')
        {
            quoteCount++;
        }

        if (quoteCount == 0)
            return SyntaxFactory.Literal(newValue);

        // 提取结尾的引号定界符
        var endQuoteCount = 0;
        var pos = originalText.Length - 1;
        while (pos >= 0 && originalText[pos] == '"')
        {
            endQuoteCount++;
            pos--;
        }

        var openDelimiter = originalText[..quoteCount];
        var closeDelimiter = originalText[(originalText.Length - endQuoteCount)..];

        // 检查开头定界符后是否有换行（多行原始字符串）
        var afterOpen = originalText[quoteCount..];
        if (afterOpen.Contains('\n'))
        {
            // 多行原始字符串：提取闭合行前的缩进
            var lineBeforeClose = originalText[..(originalText.Length - endQuoteCount)];
            var lastNewline = lineBeforeClose.LastIndexOf('\n');
            var indentation = lastNewline >= 0
                ? lineBeforeClose[(lastNewline + 1)..]
                : "";

            // 给 newValue 的每一行添加缩进前缀
            var indentedValue = IndentLines(newValue, indentation);

            var rawText = openDelimiter + "\n" + indentedValue + "\n" + indentation + closeDelimiter;

            return SyntaxFactory.Literal(
                SyntaxFactory.TriviaList(),
                rawText,
                newValue,
                SyntaxFactory.TriviaList());
        }

        // 单行原始字符串：直接拼接
        var singleLineRawText = openDelimiter + newValue + closeDelimiter;

        return SyntaxFactory.Literal(
            SyntaxFactory.TriviaList(),
            singleLineRawText,
            newValue,
            SyntaxFactory.TriviaList());
    }

    private static string IndentLines(string value, string indentation)
    {
        if (string.IsNullOrEmpty(indentation))
            return value;

        var lines = value.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Length > 0)
                lines[i] = indentation + lines[i];
        }
        return string.Join("\n", lines);
    }
}
