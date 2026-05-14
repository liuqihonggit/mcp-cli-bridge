using Common.Contracts.Schema;
using Common.Json.Schema;

namespace AstCli.Schemas;

internal static class AstSchemas
{
    internal sealed class SymbolQuery : ICliSchemaProvider
    {
        public static JsonElement GetSchema()
        {
            var schema = new JsonSchemaBuilder()
                .WithProperty("command", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithConst("symbol_query").Build())
                .WithProperty("projectPath", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Root path of the project to analyze").Build())
                .WithProperty("symbolName", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Symbol name to search for").Build())
                .WithProperty("scope", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Search scope: 'project' (all files) or 'file' (top-level only)").Build())
                .WithProperty("language", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Programming language of the project (default: csharp). Unsupported languages will return an error").Build())
                .WithRequired("command", "projectPath", "symbolName")
                .Build();
            return JsonSchemaBuilder.SerializeToJsonElement(schema);
        }
    }

    internal sealed class ReferenceFind : ICliSchemaProvider
    {
        public static JsonElement GetSchema()
        {
            var schema = new JsonSchemaBuilder()
                .WithProperty("command", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithConst("reference_find").Build())
                .WithProperty("projectPath", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Root path of the project to analyze").Build())
                .WithProperty("symbolName", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Symbol name to find references for").Build())
                .WithProperty("language", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Programming language of the project (default: csharp). Unsupported languages will return an error").Build())
                .WithRequired("command", "projectPath", "symbolName")
                .Build();
            return JsonSchemaBuilder.SerializeToJsonElement(schema);
        }
    }

    internal sealed class SymbolRename : ICliSchemaProvider
    {
        public static JsonElement GetSchema()
        {
            var schema = new JsonSchemaBuilder()
                .WithProperty("command", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithConst("symbol_rename").Build())
                .WithProperty("projectPath", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Root path of the project to analyze").Build())
                .WithProperty("symbolName", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Current symbol name").Build())
                .WithProperty("newName", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("New symbol name").Build())
                .WithProperty("language", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Programming language of the project (default: csharp). Unsupported languages will return an error").Build())
                .WithRequired("command", "projectPath", "symbolName", "newName")
                .Build();
            return JsonSchemaBuilder.SerializeToJsonElement(schema);
        }
    }

    internal sealed class SymbolReplace : ICliSchemaProvider
    {
        public static JsonElement GetSchema()
        {
            var schema = new JsonSchemaBuilder()
                .WithProperty("command", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithConst("symbol_replace").Build())
                .WithProperty("projectPath", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Root path of the project to analyze").Build())
                .WithProperty("symbolName", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Old symbol name to replace").Build())
                .WithProperty("newName", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("New symbol name").Build())
                .WithProperty("language", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Programming language of the project (default: csharp). Unsupported languages will return an error").Build())
                .WithRequired("command", "projectPath", "symbolName", "newName")
                .Build();
            return JsonSchemaBuilder.SerializeToJsonElement(schema);
        }
    }

    internal sealed class SymbolInfo : ICliSchemaProvider
    {
        public static JsonElement GetSchema()
        {
            var schema = new JsonSchemaBuilder()
                .WithProperty("command", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithConst("symbol_info").Build())
                .WithProperty("filePath", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Path to the C# source file").Build())
                .WithProperty("lineNumber", new JsonSchemaPropertyBuilder()
                    .WithType("integer").WithDescription("Line number (0-based)").Build())
                .WithProperty("columnNumber", new JsonSchemaPropertyBuilder()
                    .WithType("integer").WithDescription("Column number (0-based)").Build())
                .WithProperty("language", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Programming language of the project (default: csharp). Unsupported languages will return an error").Build())
                .WithRequired("command", "filePath", "lineNumber", "columnNumber")
                .Build();
            return JsonSchemaBuilder.SerializeToJsonElement(schema);
        }
    }

    internal sealed class WorkspaceOverview : ICliSchemaProvider
    {
        public static JsonElement GetSchema()
        {
            var schema = new JsonSchemaBuilder()
                .WithProperty("command", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithConst("workspace_overview").Build())
                .WithProperty("projectPath", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Root path of the project to analyze").Build())
                .WithProperty("language", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Programming language of the project (default: csharp). Unsupported languages will return an error").Build())
                .WithRequired("command", "projectPath")
                .Build();
            return JsonSchemaBuilder.SerializeToJsonElement(schema);
        }
    }

    internal sealed class FileContext : ICliSchemaProvider
    {
        public static JsonElement GetSchema()
        {
            var schema = new JsonSchemaBuilder()
                .WithProperty("command", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithConst("file_context").Build())
                .WithProperty("projectPath", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Root path of the project to analyze").Build())
                .WithProperty("filePath", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Path to the C# source file to analyze").Build())
                .WithProperty("language", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Programming language of the project (default: csharp). Unsupported languages will return an error").Build())
                .WithRequired("command", "projectPath", "filePath")
                .Build();
            return JsonSchemaBuilder.SerializeToJsonElement(schema);
        }
    }

    internal sealed class Diagnostics : ICliSchemaProvider
    {
        public static JsonElement GetSchema()
        {
            var schema = new JsonSchemaBuilder()
                .WithProperty("command", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithConst("diagnostics").Build())
                .WithProperty("projectPath", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Root path of the project to analyze").Build())
                .WithProperty("filePath", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Optional: specific file to check (scans entire project if omitted)").Build())
                .WithProperty("language", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Programming language of the project (default: csharp). Unsupported languages will return an error").Build())
                .WithRequired("command", "projectPath")
                .Build();
            return JsonSchemaBuilder.SerializeToJsonElement(schema);
        }
    }

    internal sealed class SymbolOutline : ICliSchemaProvider
    {
        public static JsonElement GetSchema()
        {
            var schema = new JsonSchemaBuilder()
                .WithProperty("command", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithConst("symbol_outline").Build())
                .WithProperty("filePath", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Path to the source file").Build())
                .WithProperty("language", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Programming language of the project (default: csharp). Unsupported languages will return an error").Build())
                .WithRequired("command", "filePath")
                .Build();
            return JsonSchemaBuilder.SerializeToJsonElement(schema);
        }
    }

    internal sealed class StringQuery : ICliSchemaProvider
    {
        public static JsonElement GetSchema()
        {
            var schema = new JsonSchemaBuilder()
                .WithProperty("command", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithConst("string_query").Build())
                .WithProperty("projectPath", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Root path of the project to analyze").Build())
                .WithProperty("filePath", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Optional: specific file to scan (scans entire project if omitted)").Build())
                .WithProperty("prefix", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Filter strings by prefix (e.g. 'MCP' returns only strings starting with 'MCP')").Build())
                .WithProperty("filter", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Filter strings by content (substring match)").Build())
                .WithProperty("language", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Programming language of the project (default: csharp). Unsupported languages will return an error").Build())
                .WithRequired("command", "projectPath")
                .Build();
            return JsonSchemaBuilder.SerializeToJsonElement(schema);
        }
    }

    internal sealed class StringPrefix : ICliSchemaProvider
    {
        public static JsonElement GetSchema()
        {
            var schema = new JsonSchemaBuilder()
                .WithProperty("command", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithConst("string_prefix").Build())
                .WithProperty("projectPath", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Root path of the project to analyze").Build())
                .WithProperty("filePath", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Optional: specific file to modify (modifies entire project if omitted)").Build())
                .WithProperty("insertText", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Text to insert as prefix at position 0 of each string").Build())
                .WithProperty("filter", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Only modify strings containing this substring").Build())
                .WithProperty("dryRun", new JsonSchemaPropertyBuilder()
                    .WithType("boolean").WithDescription("Preview mode: do not actually modify files (default: false)").Build())
                .WithProperty("language", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Programming language of the project (default: csharp). Unsupported languages will return an error").Build())
                .WithRequired("command", "projectPath", "insertText")
                .Build();
            return JsonSchemaBuilder.SerializeToJsonElement(schema);
        }
    }

    internal sealed class StringSuffix : ICliSchemaProvider
    {
        public static JsonElement GetSchema()
        {
            var schema = new JsonSchemaBuilder()
                .WithProperty("command", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithConst("string_suffix").Build())
                .WithProperty("projectPath", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Root path of the project to analyze").Build())
                .WithProperty("filePath", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Optional: specific file to modify (modifies entire project if omitted)").Build())
                .WithProperty("insertText", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Text to insert as suffix at the end of each string").Build())
                .WithProperty("filter", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Only modify strings containing this substring").Build())
                .WithProperty("dryRun", new JsonSchemaPropertyBuilder()
                    .WithType("boolean").WithDescription("Preview mode: do not actually modify files (default: false)").Build())
                .WithProperty("language", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Programming language of the project (default: csharp). Unsupported languages will return an error").Build())
                .WithRequired("command", "projectPath", "insertText")
                .Build();
            return JsonSchemaBuilder.SerializeToJsonElement(schema);
        }
    }

    internal sealed class StringInsert : ICliSchemaProvider
    {
        public static JsonElement GetSchema()
        {
            var schema = new JsonSchemaBuilder()
                .WithProperty("command", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithConst("string_insert").Build())
                .WithProperty("projectPath", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Root path of the project to analyze").Build())
                .WithProperty("filePath", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Optional: specific file to modify (modifies entire project if omitted)").Build())
                .WithProperty("insertText", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Text to insert at the specified position").Build())
                .WithProperty("position", new JsonSchemaPropertyBuilder()
                    .WithType("integer").WithDescription("Character position to insert at (0=beginning, equals string length=end)").Build())
                .WithProperty("filter", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Only modify strings containing this substring").Build())
                .WithProperty("dryRun", new JsonSchemaPropertyBuilder()
                    .WithType("boolean").WithDescription("Preview mode: do not actually modify files (default: false)").Build())
                .WithProperty("language", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Programming language of the project (default: csharp). Unsupported languages will return an error").Build())
                .WithRequired("command", "projectPath", "insertText", "position")
                .Build();
            return JsonSchemaBuilder.SerializeToJsonElement(schema);
        }
    }

    internal sealed class StringReplace : ICliSchemaProvider
    {
        public static JsonElement GetSchema()
        {
            var schema = new JsonSchemaBuilder()
                .WithProperty("command", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithConst("string_replace").Build())
                .WithProperty("projectPath", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Root path of the project to analyze").Build())
                .WithProperty("filePath", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Optional: specific file to modify (modifies entire project if omitted)").Build())
                .WithProperty("pattern", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Pattern to search for (regex if useRegex is true, otherwise literal string)").Build())
                .WithProperty("replacement", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Replacement text. Supports $1, $2, etc. for regex capture groups when useRegex is true").Build())
                .WithProperty("useRegex", new JsonSchemaPropertyBuilder()
                    .WithType("boolean").WithDescription("Whether to use regex pattern matching (default: false)").Build())
                .WithProperty("filter", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Only modify strings containing this substring").Build())
                .WithProperty("dryRun", new JsonSchemaPropertyBuilder()
                    .WithType("boolean").WithDescription("Preview mode: do not actually modify files (default: false)").Build())
                .WithProperty("language", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Programming language of the project (default: csharp). Unsupported languages will return an error").Build())
                .WithRequired("command", "projectPath", "pattern", "replacement")
                .Build();
            return JsonSchemaBuilder.SerializeToJsonElement(schema);
        }
    }

    internal sealed class AsyncRename : ICliSchemaProvider
    {
        public static JsonElement GetSchema()
        {
            var schema = new JsonSchemaBuilder()
                .WithProperty("command", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithConst("async_rename").Build())
                .WithProperty("projectPath", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Root path of the project to modify").Build())
                .WithProperty("filePath", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Optional: specific file to modify (modifies entire project if omitted)").Build())
                .WithProperty("symbolName", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Current method name (e.g. SendLog)").Build())
                .WithProperty("newName", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("New method name (e.g. SendLogAsync)").Build())
                .WithProperty("dryRun", new JsonSchemaPropertyBuilder()
                    .WithType("boolean").WithDescription("Preview mode: do not actually modify files (default: false)").Build())
                .WithProperty("language", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Programming language (default: csharp)").Build())
                .WithRequired("command", "projectPath", "symbolName", "newName")
                .Build();
            return JsonSchemaBuilder.SerializeToJsonElement(schema);
        }
    }

    internal sealed class AsyncAddModifier : ICliSchemaProvider
    {
        public static JsonElement GetSchema()
        {
            var schema = new JsonSchemaBuilder()
                .WithProperty("command", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithConst("async_add_modifier").Build())
                .WithProperty("projectPath", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Root path of the project to modify").Build())
                .WithProperty("filePath", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Optional: specific file to modify (modifies entire project if omitted)").Build())
                .WithProperty("symbolName", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Method name to add async modifier to").Build())
                .WithProperty("dryRun", new JsonSchemaPropertyBuilder()
                    .WithType("boolean").WithDescription("Preview mode: do not actually modify files (default: false)").Build())
                .WithProperty("language", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Programming language (default: csharp)").Build())
                .WithRequired("command", "projectPath", "symbolName")
                .Build();
            return JsonSchemaBuilder.SerializeToJsonElement(schema);
        }
    }

    internal sealed class AsyncReturnType : ICliSchemaProvider
    {
        public static JsonElement GetSchema()
        {
            var schema = new JsonSchemaBuilder()
                .WithProperty("command", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithConst("async_return_type").Build())
                .WithProperty("projectPath", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Root path of the project to modify").Build())
                .WithProperty("filePath", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Optional: specific file to modify (modifies entire project if omitted)").Build())
                .WithProperty("symbolName", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Method name to change return type for").Build())
                .WithProperty("dryRun", new JsonSchemaPropertyBuilder()
                    .WithType("boolean").WithDescription("Preview mode: do not actually modify files (default: false)").Build())
                .WithProperty("language", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Programming language (default: csharp)").Build())
                .WithRequired("command", "projectPath", "symbolName")
                .Build();
            return JsonSchemaBuilder.SerializeToJsonElement(schema);
        }
    }

    internal sealed class AsyncAddAwait : ICliSchemaProvider
    {
        public static JsonElement GetSchema()
        {
            var schema = new JsonSchemaBuilder()
                .WithProperty("command", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithConst("async_add_await").Build())
                .WithProperty("projectPath", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Root path of the project to modify").Build())
                .WithProperty("filePath", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Optional: specific file to modify (modifies entire project if omitted)").Build())
                .WithProperty("symbolName", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Method name whose invocations should be awaited").Build())
                .WithProperty("addConfigureAwait", new JsonSchemaPropertyBuilder()
                    .WithType("boolean").WithDescription("Whether to append .ConfigureAwait(false) after await (default: false)").Build())
                .WithProperty("dryRun", new JsonSchemaPropertyBuilder()
                    .WithType("boolean").WithDescription("Preview mode: do not actually modify files (default: false)").Build())
                .WithProperty("language", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Programming language (default: csharp)").Build())
                .WithRequired("command", "projectPath", "symbolName")
                .Build();
            return JsonSchemaBuilder.SerializeToJsonElement(schema);
        }
    }

    internal sealed class AsyncParamAdd : ICliSchemaProvider
    {
        public static JsonElement GetSchema()
        {
            var schema = new JsonSchemaBuilder()
                .WithProperty("command", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithConst("async_param_add").Build())
                .WithProperty("projectPath", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Root path of the project to modify").Build())
                .WithProperty("filePath", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Optional: specific file to modify (modifies entire project if omitted)").Build())
                .WithProperty("symbolName", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Method name to add parameter to").Build())
                .WithProperty("paramType", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Parameter type (e.g. CancellationToken)").Build())
                .WithProperty("paramName", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Parameter name (e.g. ct)").Build())
                .WithProperty("dryRun", new JsonSchemaPropertyBuilder()
                    .WithType("boolean").WithDescription("Preview mode: do not actually modify files (default: false)").Build())
                .WithProperty("language", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Programming language (default: csharp)").Build())
                .WithRequired("command", "projectPath", "symbolName", "paramType", "paramName")
                .Build();
            return JsonSchemaBuilder.SerializeToJsonElement(schema);
        }
    }

    internal sealed class SyncRemoveModifier : ICliSchemaProvider
    {
        public static JsonElement GetSchema()
        {
            var schema = new JsonSchemaBuilder()
                .WithProperty("command", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithConst("sync_remove_modifier").Build())
                .WithProperty("projectPath", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Root path of the project to modify").Build())
                .WithProperty("filePath", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Optional: specific file to modify (modifies entire project if omitted)").Build())
                .WithProperty("symbolName", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Method name to remove async modifier from").Build())
                .WithProperty("dryRun", new JsonSchemaPropertyBuilder()
                    .WithType("boolean").WithDescription("Preview mode: do not actually modify files (default: false)").Build())
                .WithProperty("language", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Programming language (default: csharp)").Build())
                .WithRequired("command", "projectPath", "symbolName")
                .Build();
            return JsonSchemaBuilder.SerializeToJsonElement(schema);
        }
    }

    internal sealed class SyncReturnType : ICliSchemaProvider
    {
        public static JsonElement GetSchema()
        {
            var schema = new JsonSchemaBuilder()
                .WithProperty("command", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithConst("sync_return_type").Build())
                .WithProperty("projectPath", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Root path of the project to modify").Build())
                .WithProperty("filePath", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Optional: specific file to modify (modifies entire project if omitted)").Build())
                .WithProperty("symbolName", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Method name to unwrap return type for").Build())
                .WithProperty("dryRun", new JsonSchemaPropertyBuilder()
                    .WithType("boolean").WithDescription("Preview mode: do not actually modify files (default: false)").Build())
                .WithProperty("language", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Programming language (default: csharp)").Build())
                .WithRequired("command", "projectPath", "symbolName")
                .Build();
            return JsonSchemaBuilder.SerializeToJsonElement(schema);
        }
    }

    internal sealed class SyncRemoveAwait : ICliSchemaProvider
    {
        public static JsonElement GetSchema()
        {
            var schema = new JsonSchemaBuilder()
                .WithProperty("command", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithConst("sync_remove_await").Build())
                .WithProperty("projectPath", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Root path of the project to modify").Build())
                .WithProperty("filePath", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Optional: specific file to modify (modifies entire project if omitted)").Build())
                .WithProperty("symbolName", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Method name whose invocations should have await removed").Build())
                .WithProperty("dryRun", new JsonSchemaPropertyBuilder()
                    .WithType("boolean").WithDescription("Preview mode: do not actually modify files (default: false)").Build())
                .WithProperty("language", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Programming language (default: csharp)").Build())
                .WithRequired("command", "projectPath", "symbolName")
                .Build();
            return JsonSchemaBuilder.SerializeToJsonElement(schema);
        }
    }

    internal sealed class SyncParamRemove : ICliSchemaProvider
    {
        public static JsonElement GetSchema()
        {
            var schema = new JsonSchemaBuilder()
                .WithProperty("command", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithConst("sync_param_remove").Build())
                .WithProperty("projectPath", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Root path of the project to modify").Build())
                .WithProperty("filePath", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Optional: specific file to modify (modifies entire project if omitted)").Build())
                .WithProperty("symbolName", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Method name to remove parameter from").Build())
                .WithProperty("paramName", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Parameter name to remove (e.g. ct)").Build())
                .WithProperty("dryRun", new JsonSchemaPropertyBuilder()
                    .WithType("boolean").WithDescription("Preview mode: do not actually modify files (default: false)").Build())
                .WithProperty("language", new JsonSchemaPropertyBuilder()
                    .WithType("string").WithDescription("Programming language (default: csharp)").Build())
                .WithRequired("command", "projectPath", "symbolName", "paramName")
                .Build();
            return JsonSchemaBuilder.SerializeToJsonElement(schema);
        }
    }
}
