namespace AstCli.Services;

public sealed class StringLiteralEngine
{
    private static readonly string[] s_excludedDirs = ["bin", "obj", ".git", "node_modules", ".vs"];
    private static readonly TimeSpan s_lockTimeout = TimeSpan.FromSeconds(5);

    public static async Task<StringQueryResultDto> QueryAsync(
        string projectPath, string? filePath, string? prefix, string? filter)
    {
        var files = filePath != null
            ? [filePath]
            : GetProjectFiles(projectPath);

        var strings = new List<StringLiteralInfoDto>();

        foreach (var file in files)
        {
            try
            {
                var content = await ReadFileWithLockAsync(file);
                var tree = CSharpSyntaxTree.ParseText(content, path: file);
                var root = tree.GetCompilationUnitRoot();

                var collector = new StringLiteralCollector(file, content, prefix, filter);
                collector.Visit(root);
                strings.AddRange(collector.Results);
            }
            catch
            {
            }
        }

        var countByKind = strings
            .GroupBy(s => s.Kind)
            .ToDictionary(g => g.Key, g => g.Count());

        return new StringQueryResultDto
        {
            ProjectPath = projectPath,
            Strings = strings,
            TotalCount = strings.Count,
            CountByKind = countByKind
        };
    }

    public static async Task<StringInsertResultDto> InsertAsync(
        string projectPath, string? filePath, string insertText,
        int position, string mode, string? filter, bool dryRun)
    {
        var files = filePath != null
            ? [filePath]
            : GetProjectFiles(projectPath);

        var modifiedFiles = new List<string>();
        var totalTransformed = 0;

        foreach (var file in files)
        {
            try
            {
                var content = await ReadFileWithLockAsync(file);
                var tree = CSharpSyntaxTree.ParseText(content, path: file);
                var root = tree.GetCompilationUnitRoot();

                var rewriter = new StringLiteralRewriter(insertText, position, filter);
                var newRoot = rewriter.Visit(root);

                if (rewriter.TransformedCount > 0)
                {
                    totalTransformed += rewriter.TransformedCount;

                    if (!dryRun)
                    {
                        var newContent = newRoot.ToFullString();
                        await WriteFileWithLockAsync(file, newContent);
                    }

                    modifiedFiles.Add(file);
                }
            }
            catch
            {
            }
        }

        return new StringInsertResultDto
        {
            InsertText = insertText,
            Position = position,
            Mode = mode,
            Success = totalTransformed > 0,
            TransformedCount = totalTransformed,
            ModifiedFiles = modifiedFiles,
            Message = totalTransformed > 0
                ? $"{mode}: inserted '{insertText}' at position {position} in {totalTransformed} string(s) across {modifiedFiles.Count} file(s)"
                : "No matching strings found"
        };
    }

    private static async Task<string> ReadFileWithLockAsync(string filePath)
    {
        var lockResult = await Common.FileLock.FileLockService.AcquireAsync(filePath, s_lockTimeout);
        if (!lockResult.Success || lockResult.Lock == null)
            throw new TimeoutException($"Failed to acquire lock for file: {filePath}");

        await using (lockResult.Lock)
        {
#pragma warning disable MCP001
            return await File.ReadAllTextAsync(filePath);
#pragma warning restore MCP001
        }
    }

    private static async Task WriteFileWithLockAsync(string filePath, string content)
    {
        var lockResult = await Common.FileLock.FileLockService.AcquireAsync(filePath, s_lockTimeout);
        if (!lockResult.Success || lockResult.Lock == null)
            throw new TimeoutException($"Failed to acquire lock for file: {filePath}");

        await using (lockResult.Lock)
        {
#pragma warning disable MCP001
            await File.WriteAllTextAsync(filePath, content);
#pragma warning restore MCP001
        }
    }

    internal static List<string> GetProjectFiles(string projectPath)
    {
#pragma warning disable MCP001
        if (!Directory.Exists(projectPath))
            return [];
#pragma warning restore MCP001

#pragma warning disable MCP001
        return Directory.GetFiles(projectPath, "*.cs", SearchOption.AllDirectories)
#pragma warning restore MCP001
            .Where(f => !IsExcluded(f))
            .ToList();
    }

    private static bool IsExcluded(string filePath)
    {
        foreach (var dir in s_excludedDirs)
        {
            if (filePath.Contains($"\\{dir}\\", StringComparison.OrdinalIgnoreCase) ||
                filePath.Contains($"/{dir}/", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    internal static string GetStringKind(SyntaxToken token)
    {
        var text = token.Text;
        if (text.StartsWith("\"\"\"", StringComparison.Ordinal))
            return "Raw";
        if (text.StartsWith("@\"", StringComparison.Ordinal))
            return "Verbatim";
        return "Regular";
    }

    internal static string GetInterpolatedStringKind(InterpolatedStringExpressionSyntax node)
    {
        var startToken = node.StringStartToken.Text;
        if (startToken.Contains("\"\"\"", StringComparison.Ordinal))
            return "InterpolatedRaw";
        if (startToken.Contains('@'))
            return "VerbatimInterpolated";
        return "Interpolated";
    }

    internal static SyntaxToken CreateVerbatimLiteral(string value)
    {
        var escaped = value.Replace("\"", "\"\"");
        var literalText = "@\"" + escaped + "\"";
        return SyntaxFactory.Literal(
            SyntaxFactory.TriviaList(),
            literalText,
            value,
            SyntaxFactory.TriviaList());
    }

    internal static SyntaxToken CreateRawLiteral(SyntaxToken originalToken, string newValue)
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

file sealed class StringLiteralCollector : CSharpSyntaxWalker
{
    private readonly string _filePath;
    private readonly string _sourceText;
    private readonly string? _prefix;
    private readonly string? _filter;

    public List<StringLiteralInfoDto> Results { get; } = [];

    public StringLiteralCollector(string filePath, string sourceText, string? prefix, string? filter)
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
                var line = lineSpan.StartLinePosition.Line;
                var context = GetLineContent(line);

                Results.Add(new StringLiteralInfoDto
                {
                    Value = valueText,
                    Kind = StringLiteralEngine.GetStringKind(node.Token),
                    FilePath = _filePath,
                    Line = line,
                    Column = lineSpan.StartLinePosition.Character,
                    Length = valueText.Length,
                    Context = context
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
            var line = lineSpan.StartLinePosition.Line;
            var context = GetLineContent(line);

            Results.Add(new StringLiteralInfoDto
            {
                Value = fullValue,
                Kind = StringLiteralEngine.GetInterpolatedStringKind(node),
                FilePath = _filePath,
                Line = line,
                Column = lineSpan.StartLinePosition.Character,
                Length = fullValue.Length,
                Context = context
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

    private string? GetLineContent(int lineIndex)
    {
        try
        {
            var lines = _sourceText.Split('\n');
            if (lineIndex >= 0 && lineIndex < lines.Length)
                return lines[lineIndex].Trim();
        }
        catch
        {
        }
        return null;
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

file sealed class StringLiteralRewriter : CSharpSyntaxRewriter
{
    private readonly string _insertText;
    private readonly int _position;
    private readonly string? _filter;

    public int TransformedCount { get; private set; }

    public StringLiteralRewriter(string insertText, int position, string? filter)
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
        var newValue = string.Concat(
            valueText.AsSpan(0, position),
            _insertText,
            valueText.AsSpan(position));

        var kind = StringLiteralEngine.GetStringKind(node.Token);
        SyntaxToken newToken = kind switch
        {
            "Verbatim" => StringLiteralEngine.CreateVerbatimLiteral(newValue),
            "Raw" => StringLiteralEngine.CreateRawLiteral(node.Token, newValue),
            _ => SyntaxFactory.Literal(newValue)
        };

        TransformedCount++;
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
            TransformedCount++;
            return node.WithContents(SyntaxFactory.List(InsertAtBeginning(node)));
        }

        if (position >= fullValue.Length)
        {
            TransformedCount++;
            return node.WithContents(SyntaxFactory.List(InsertAtEnd(node)));
        }

        var splitContents = InsertAtMiddle(node, position);
        if (splitContents != null)
        {
            TransformedCount++;
            return node.WithContents(SyntaxFactory.List(splitContents));
        }

        return base.VisitInterpolatedStringExpression(node);
    }

    private List<InterpolatedStringContentSyntax> InsertAtBeginning(InterpolatedStringExpressionSyntax node)
    {
        var newContents = new List<InterpolatedStringContentSyntax>();

        var textToken = SyntaxFactory.Token(
            SyntaxFactory.TriviaList(),
            SyntaxKind.InterpolatedStringTextToken,
            _insertText,
            _insertText,
            SyntaxFactory.TriviaList());
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
            SyntaxFactory.TriviaList(),
            SyntaxKind.InterpolatedStringTextToken,
            _insertText,
            _insertText,
            SyntaxFactory.TriviaList());
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
