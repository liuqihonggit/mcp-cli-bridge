using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace CliCommandGenerator;

[Generator]
public sealed class CliCommandSourceGenerator : IIncrementalGenerator
{
    private const string HandlerAttributeFullName = "Common.Contracts.Attributes.CliCommandHandlerAttribute";
    private const string CommandAttributeFullName = "Common.Contracts.Attributes.CliCommandAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var commandHandlers = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                HandlerAttributeFullName,
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, ct) => TransformCommandHandler(ctx, ct))
            .Where(static x => x is not null)
            .Select(static (x, _) => x!);

        context.RegisterSourceOutput(commandHandlers, static (ctx, handler) => GenerateSource(ctx, handler));
    }

    private static CommandHandlerInfo? TransformCommandHandler(GeneratorAttributeSyntaxContext context, CancellationToken ct)
    {
        var classSymbol = context.TargetSymbol as INamedTypeSymbol;
        if (classSymbol == null) return null;

        var handlerAttribute = context.Attributes[0];

        var pluginName = handlerAttribute.ConstructorArguments.ElementAtOrDefault(0).Value as string ?? "";
        var description = handlerAttribute.ConstructorArguments.ElementAtOrDefault(1).Value as string ?? "";

        var category = "general";
        var toolNamePrefix = "";
        var hasDocumentation = false;

        foreach (var namedArg in handlerAttribute.NamedArguments)
        {
            switch (namedArg.Key)
            {
                case "Category":
                    category = namedArg.Value.Value as string ?? "general";
                    break;
                case "ToolNamePrefix":
                    toolNamePrefix = namedArg.Value.Value as string ?? "";
                    break;
                case "HasDocumentation":
                    hasDocumentation = namedArg.Value.Value is bool b && b;
                    break;
            }
        }

        var commands = new List<CommandInfo>();
        string? requestType = null;
        var hasInstanceMethod = false;

        foreach (var member in classSymbol.GetMembers())
        {
            ct.ThrowIfCancellationRequested();

            if (member is not IMethodSymbol method) continue;

            var cmdAttribute = method.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == CommandAttributeFullName);

            if (cmdAttribute == null) continue;

            var commandName = cmdAttribute.ConstructorArguments.ElementAtOrDefault(0).Value as string;
            if (commandName == null) continue;

            var cmdDescription = "";
            var cmdCategory = "general";
            string? schemaTypeFullName = null;

            foreach (var namedArg in cmdAttribute.NamedArguments)
            {
                switch (namedArg.Key)
                {
                    case "Description":
                        cmdDescription = namedArg.Value.Value as string ?? "";
                        break;
                    case "Category":
                        cmdCategory = namedArg.Value.Value as string ?? "general";
                        break;
                    case "SchemaType":
                        if (namedArg.Value.Value is INamedTypeSymbol schemaType)
                            schemaTypeFullName = schemaType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        break;
                }
            }

            var isStatic = method.IsStatic;
            if (!isStatic) hasInstanceMethod = true;

            var hasRequestParam = method.Parameters.Length > 0;
            if (hasRequestParam && requestType == null)
            {
                requestType = method.Parameters[0].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }

            var isAsync = method.ReturnType.Name == "Task" ||
                          (method.ReturnType is INamedTypeSymbol { IsGenericType: true } gt && gt.Name == "Task");

            commands.Add(new CommandInfo(
                CommandName: commandName,
                MethodName: method.Name,
                Description: cmdDescription,
                Category: cmdCategory,
                SchemaTypeFullName: schemaTypeFullName,
                IsStatic: isStatic,
                HasRequestParam: hasRequestParam,
                IsAsync: isAsync
            ));
        }

        if (commands.Count == 0) return null;

        return new CommandHandlerInfo(
            Namespace: classSymbol.ContainingNamespace.ToDisplayString(),
            ClassName: classSymbol.Name,
            PluginName: pluginName,
            Description: description,
            Category: category,
            ToolNamePrefix: toolNamePrefix,
            HasDocumentation: hasDocumentation,
            HasInstanceMethod: hasInstanceMethod,
            RequestType: requestType ?? "object",
            Commands: commands
        );
    }

    private static void GenerateSource(SourceProductionContext context, CommandHandlerInfo handler)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine();
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Text.Json;");
        sb.AppendLine("using Common.Contracts.Models;");
        sb.AppendLine("using Common.Json;");
        sb.AppendLine("using McpProtocol.Contracts;");
        sb.AppendLine();
        sb.AppendLine($"namespace {handler.Namespace};");
        sb.AppendLine();
        sb.AppendLine($"partial class {handler.ClassName}");
        sb.AppendLine("{");

        GenerateExecuteAsync(sb, handler);
        sb.AppendLine();
        GenerateListTools(sb, handler);
        sb.AppendLine();
        GenerateListCommands(sb, handler);

        sb.AppendLine("}");

        context.AddSource($"{handler.ClassName}.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private static void GenerateExecuteAsync(StringBuilder sb, CommandHandlerInfo handler)
    {
        var staticModifier = handler.HasInstanceMethod ? "" : " static";

        sb.AppendLine($"    public{staticModifier} async global::System.Threading.Tasks.Task<global::Common.Contracts.OperationResult<JsonElement>> ExecuteAsync({handler.RequestType} request)");
        sb.AppendLine("    {");
        sb.AppendLine("        return request.Command?.ToLowerInvariant() switch");
        sb.AppendLine("        {");

        foreach (var cmd in handler.Commands)
        {
            var call = cmd.HasRequestParam ? $"{cmd.MethodName}(request)" : $"{cmd.MethodName}()";
            var awaitKeyword = cmd.IsAsync ? "await " : "";
            sb.AppendLine($"            \"{cmd.CommandName}\" => {awaitKeyword}{call},");
        }

        sb.AppendLine("            \"list_tools\" => ListTools(),");
        sb.AppendLine("            \"list_commands\" => ListCommands(),");
        sb.AppendLine("            _ => Fail($\"Unknown command: {request.Command}\")");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
    }

    private static void GenerateListTools(StringBuilder sb, CommandHandlerInfo handler)
    {
        sb.AppendLine("    private static global::Common.Contracts.OperationResult<JsonElement> ListTools()");
        sb.AppendLine("    {");
        sb.AppendLine("        var pluginDescriptor = new PluginDescriptor");
        sb.AppendLine("        {");
        sb.AppendLine($"            Name = \"{handler.PluginName}\",");
        sb.AppendLine($"            Description = \"{EscapeString(handler.Description)}\",");
        sb.AppendLine($"            Category = \"{handler.Category}\",");
        sb.AppendLine($"            CommandCount = {handler.Commands.Count},");
        sb.AppendLine($"            HasDocumentation = {(handler.HasDocumentation ? "true" : "false")}");
        sb.AppendLine("        };");
        sb.AppendLine();
        sb.AppendLine("        return Ok(pluginDescriptor, \"\", CommonJsonContext.Default.PluginDescriptor);");
        sb.AppendLine("    }");
    }

    private static void GenerateListCommands(StringBuilder sb, CommandHandlerInfo handler)
    {
        sb.AppendLine("    private static global::Common.Contracts.OperationResult<JsonElement> ListCommands()");
        sb.AppendLine("    {");
        sb.AppendLine("        var tools = new List<ToolDefinition>");
        sb.AppendLine("        {");

        foreach (var cmd in handler.Commands)
        {
            var toolName = $"{handler.ToolNamePrefix}{cmd.CommandName}";
            var schemaExpr = cmd.SchemaTypeFullName != null
                ? $"{cmd.SchemaTypeFullName}.GetSchema()"
                : "default";

            sb.AppendLine("            new()");
            sb.AppendLine("            {");
            sb.AppendLine($"                Name = \"{toolName}\",");
            sb.AppendLine($"                Description = \"{EscapeString(cmd.Description)}\",");
            sb.AppendLine($"                Category = \"{cmd.Category}\",");
            sb.AppendLine($"                InputSchema = {schemaExpr}");
            sb.AppendLine("            },");
        }

        sb.AppendLine("        };");
        sb.AppendLine();
        sb.AppendLine("        return Ok(tools, \"\", CommonJsonContext.Default.ListToolDefinition);");
        sb.AppendLine("    }");
    }

    private static string EscapeString(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private sealed record CommandHandlerInfo(
        string Namespace,
        string ClassName,
        string PluginName,
        string Description,
        string Category,
        string ToolNamePrefix,
        bool HasDocumentation,
        bool HasInstanceMethod,
        string RequestType,
        List<CommandInfo> Commands
    );

    private sealed record CommandInfo(
        string CommandName,
        string MethodName,
        string Description,
        string Category,
        string? SchemaTypeFullName,
        bool IsStatic,
        bool HasRequestParam,
        bool IsAsync
    );
}
