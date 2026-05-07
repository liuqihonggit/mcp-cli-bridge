namespace Common.PluginManager;

using McpProtocol;
using Common.Json.Schema;

public class McpToolAdapter
{
    private readonly Dictionary<string, ToolHandlerAdapter> _handlers = new(StringComparer.Ordinal);
    private readonly ToolMethodRegistry _registry;
    private readonly McpToolMethodInvoker _invoker;

    public McpToolAdapter()
    {
        _registry = new ToolMethodRegistry(new MethodInvokerFactory());
        _invoker = new McpToolMethodInvoker();
    }

    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "工具类型在编译时已知")]
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "工具类型由TrimmerRootAssembly保留")]
    [UnconditionalSuppressMessage("Trimming", "IL2075:DynamicallyAccessedMembers", Justification = "工具类型由TrimmerRootAssembly保留")]
    public void RegisterTool<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] T>(T toolInstance) where T : class
    {
        ArgumentNullException.ThrowIfNull(toolInstance);
        _registry.RegisterToolInstance(toolInstance);

        var type = toolInstance.GetType();
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);

        foreach (var method in methods)
        {
            var attr = method.GetCustomAttribute<McpToolAttribute>();
            if (attr == null) continue;

            var toolName = attr.Name ?? method.Name;
            if (_registry.TryGetToolMethod(toolName, out var metadata))
            {
                _handlers[toolName] = new ToolHandlerAdapter(metadata, toolInstance, _invoker);
            }
        }
    }

    public IReadOnlyList<IToolHandler> GetHandlers()
    {
        return _handlers.Values.ToList().AsReadOnly();
    }

    private sealed class ToolHandlerAdapter : IToolHandler
    {
        private readonly ToolMethodMetadata _metadata;
        private readonly object _instance;
        private readonly McpToolMethodInvoker _invoker;
        private readonly JsonElement _inputSchema;

        public ToolHandlerAdapter(ToolMethodMetadata metadata, object instance, McpToolMethodInvoker invoker)
        {
            _metadata = metadata;
            _instance = instance;
            _invoker = invoker;
            _inputSchema = BuildInputSchema(metadata);
        }

        public string Name => _metadata.Name;
        public string Description => _metadata.Description;
        public JsonElement InputSchema => _inputSchema;

        public async Task<object> ExecuteAsync(Dictionary<string, JsonElement> arguments)
        {
            var result = await _invoker.InvokeAsync(_instance, _metadata.Method, arguments);
            return result ?? "null";
        }

        private static JsonElement BuildInputSchema(ToolMethodMetadata metadata)
        {
            if (metadata.Parameters.Count == 0)
            {
                return CreateEmptySchema();
            }

            var builder = new JsonSchemaBuilder();
            var required = new List<string>();

            foreach (var param in metadata.Parameters)
            {
                var propBuilder = new JsonSchemaPropertyBuilder()
                    .WithType(MapTypeToJsonSchema(param.Type));

                var description = metadata.ParameterDescriptions.GetValueOrDefault(param.Name);
                if (!string.IsNullOrEmpty(description))
                {
                    propBuilder.WithDescription(description);
                }

                builder.WithProperty(param.Name, propBuilder.Build());

                if (!param.IsOptional)
                {
                    required.Add(param.Name);
                }
            }

            if (required.Count > 0)
            {
                builder.WithRequired(required.ToArray());
            }

            return JsonSchemaBuilder.SerializeToJsonElement(builder.Build());
        }

        private static string MapTypeToJsonSchema(Type type)
        {
            var underlying = Nullable.GetUnderlyingType(type) ?? type;

            if (underlying == typeof(string))
                return "string";
            if (underlying == typeof(int) || underlying == typeof(long))
                return "integer";
            if (underlying == typeof(double) || underlying == typeof(float) || underlying == typeof(decimal))
                return "number";
            if (underlying == typeof(bool))
                return "boolean";
            if (underlying == typeof(Dictionary<string, JsonElement>))
                return "object";

            return "object";
        }

        private static JsonElement CreateEmptySchema()
        {
            var json = @"{""type"":""object"",""properties"":{},""required"":[]}";
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
    }
}
