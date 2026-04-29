namespace Common.PluginManager;

using McpProtocol;

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

        public ToolHandlerAdapter(ToolMethodMetadata metadata, object instance, McpToolMethodInvoker invoker)
        {
            _metadata = metadata;
            _instance = instance;
            _invoker = invoker;
        }

        public string Name => _metadata.Name;
        public string Description => _metadata.Description;

        public async Task<object> ExecuteAsync(Dictionary<string, JsonElement> arguments)
        {
            var result = await _invoker.InvokeAsync(_instance, _metadata.Method, arguments);
            return result ?? "null";
        }
    }
}
