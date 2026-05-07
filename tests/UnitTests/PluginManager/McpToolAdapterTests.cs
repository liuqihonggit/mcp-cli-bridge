using Common.Json;
using Common.Json.Schema;
using Common.Tools;
using McpProtocol;
using McpProtocol.Contracts;

namespace MyMemoryServer.UnitTests.PluginManager;

public sealed class McpToolAdapterTests
{
    #region InputSchema 生成测试

    [Fact]
    public void RegisterTool_ShouldGenerateInputSchemaFromMethodParameters()
    {
        var adapter = new McpToolAdapter();
        var tool = new TestToolWithParameters();
        adapter.RegisterTool(tool);

        var handlers = adapter.GetHandlers();
        var handler = handlers.Should().ContainSingle(h => h.Name == "test_tool_with_params").Subject;

        handler.InputSchema.ValueKind.Should().Be(JsonValueKind.Object);

        var schema = JsonSerializer.Deserialize<JsonSchema>(handler.InputSchema.GetRawText(), CommonJsonContext.Default.JsonSchema);
        schema.Should().NotBeNull();
        schema!.Properties.Should().ContainKey("name");
        schema.Properties.Should().ContainKey("count");
        schema.Properties["name"].Type.Should().Be("string");
        schema.Properties["count"].Type.Should().Be("integer");
        schema.Required.Should().BeEquivalentTo("name", "count");
    }

    [Fact]
    public void RegisterTool_WithOptionalParameter_ShouldNotIncludeInRequired()
    {
        var adapter = new McpToolAdapter();
        var tool = new TestToolWithOptionalParam();
        adapter.RegisterTool(tool);

        var handlers = adapter.GetHandlers();
        var handler = handlers.Should().ContainSingle(h => h.Name == "test_tool_optional").Subject;

        var schema = JsonSerializer.Deserialize<JsonSchema>(handler.InputSchema.GetRawText(), CommonJsonContext.Default.JsonSchema);
        schema.Should().NotBeNull();
        schema!.Properties.Should().ContainKey("name");
        schema.Properties.Should().ContainKey("limit");
        schema.Required.Should().NotContain("limit");
        schema.Required.Should().Contain("name");
    }

    [Fact]
    public void RegisterTool_WithNoParameters_ShouldReturnEmptySchema()
    {
        var adapter = new McpToolAdapter();
        var tool = new TestToolNoParams();
        adapter.RegisterTool(tool);

        var handlers = adapter.GetHandlers();
        var handler = handlers.Should().ContainSingle(h => h.Name == "test_tool_no_params").Subject;

        var schema = JsonSerializer.Deserialize<JsonSchema>(handler.InputSchema.GetRawText(), CommonJsonContext.Default.JsonSchema);
        schema.Should().NotBeNull();
        schema!.Properties.Should().BeEmpty();
        schema.Required.Should().BeEmpty();
    }

    [Fact]
    public void RegisterTool_WithDictionaryParameter_ShouldMapToObjectType()
    {
        var adapter = new McpToolAdapter();
        var tool = new TestToolWithDictParam();
        adapter.RegisterTool(tool);

        var handlers = adapter.GetHandlers();
        var handler = handlers.Should().ContainSingle(h => h.Name == "test_tool_dict").Subject;

        var schema = JsonSerializer.Deserialize<JsonSchema>(handler.InputSchema.GetRawText(), CommonJsonContext.Default.JsonSchema);
        schema.Should().NotBeNull();
        schema!.Properties.Should().ContainKey("tool");
        schema.Properties.Should().ContainKey("parameters");
        schema.Properties["tool"].Type.Should().Be("string");
        schema.Properties["parameters"].Type.Should().Be("object");
    }

    [Fact]
    public void RegisterTool_ParameterDescription_ShouldBeIncludedInSchema()
    {
        var adapter = new McpToolAdapter();
        var tool = new TestToolWithDescriptions();
        adapter.RegisterTool(tool);

        var handlers = adapter.GetHandlers();
        var handler = handlers.Should().ContainSingle(h => h.Name == "test_tool_desc").Subject;

        var schema = JsonSerializer.Deserialize<JsonSchema>(handler.InputSchema.GetRawText(), CommonJsonContext.Default.JsonSchema);
        schema.Should().NotBeNull();
        schema!.Properties["pluginName"].Description.Should().Be("Plugin name to describe");
    }

    [Fact]
    public void RegisterTool_BoolParameter_ShouldMapToBooleanType()
    {
        var adapter = new McpToolAdapter();
        var tool = new TestToolWithBoolParam();
        adapter.RegisterTool(tool);

        var handlers = adapter.GetHandlers();
        var handler = handlers.Should().ContainSingle(h => h.Name == "test_tool_bool").Subject;

        var schema = JsonSerializer.Deserialize<JsonSchema>(handler.InputSchema.GetRawText(), CommonJsonContext.Default.JsonSchema);
        schema.Should().NotBeNull();
        schema!.Properties["enabled"].Type.Should().Be("boolean");
    }

    [Fact]
    public void RegisterTool_DoubleParameter_ShouldMapToNumberType()
    {
        var adapter = new McpToolAdapter();
        var tool = new TestToolWithDoubleParam();
        adapter.RegisterTool(tool);

        var handlers = adapter.GetHandlers();
        var handler = handlers.Should().ContainSingle(h => h.Name == "test_tool_double").Subject;

        var schema = JsonSerializer.Deserialize<JsonSchema>(handler.InputSchema.GetRawText(), CommonJsonContext.Default.JsonSchema);
        schema.Should().NotBeNull();
        schema!.Properties["value"].Type.Should().Be("number");
    }

    [Fact]
    public void RegisterTool_MultipleTools_ShouldGenerateDistinctSchemas()
    {
        var adapter = new McpToolAdapter();
        var tool = new TestToolMultiple();
        adapter.RegisterTool(tool);

        var handlers = adapter.GetHandlers();
        handlers.Should().HaveCount(2);

        var handler1 = handlers.First(h => h.Name == "tool_a");
        var handler2 = handlers.First(h => h.Name == "tool_b");

        var schema1 = JsonSerializer.Deserialize<JsonSchema>(handler1.InputSchema.GetRawText(), CommonJsonContext.Default.JsonSchema);
        var schema2 = JsonSerializer.Deserialize<JsonSchema>(handler2.InputSchema.GetRawText(), CommonJsonContext.Default.JsonSchema);

        schema1!.Properties.Should().ContainKey("query");
        schema1.Properties.Should().NotContainKey("packageName");
        schema2!.Properties.Should().ContainKey("packageName");
        schema2.Properties.Should().NotContainKey("query");
    }

    #endregion

    #region 二级调用传参测试

    [Fact]
    public async Task ExecuteAsync_WithCorrectArguments_ShouldPassParametersToMethod()
    {
        var adapter = new McpToolAdapter();
        var tool = new TestToolWithParameters();
        adapter.RegisterTool(tool);

        var handlers = adapter.GetHandlers();
        var handler = handlers.First(h => h.Name == "test_tool_with_params");

        var args = new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement("test-name"),
            ["count"] = JsonSerializer.SerializeToElement(42)
        };

        var result = await handler.ExecuteAsync(args);

        result.Should().NotBeNull();
        tool.LastName.Should().Be("test-name");
        tool.LastCount.Should().Be(42);
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingRequiredArgument_ShouldUseDefault()
    {
        var adapter = new McpToolAdapter();
        var tool = new TestToolWithParameters();
        adapter.RegisterTool(tool);

        var handlers = adapter.GetHandlers();
        var handler = handlers.First(h => h.Name == "test_tool_with_params");

        var args = new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement("test-name")
        };

        var result = await handler.ExecuteAsync(args);

        result.Should().NotBeNull();
        tool.LastCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_WithOptionalArgument_ShouldUseProvidedOrDefault()
    {
        var adapter = new McpToolAdapter();
        var tool = new TestToolWithOptionalParam();
        adapter.RegisterTool(tool);

        var handlers = adapter.GetHandlers();
        var handler = handlers.First(h => h.Name == "test_tool_optional");

        var argsWithLimit = new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement("test"),
            ["limit"] = JsonSerializer.SerializeToElement(5)
        };

        await handler.ExecuteAsync(argsWithLimit);
        tool.LastLimit.Should().Be(5);

        var argsWithoutLimit = new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement("test2")
        };

        await handler.ExecuteAsync(argsWithoutLimit);
        tool.LastLimit.Should().Be(10);
    }

    [Fact]
    public async Task ExecuteAsync_WithDictionaryArgument_ShouldDeserializeCorrectly()
    {
        var adapter = new McpToolAdapter();
        var tool = new TestToolWithDictParam();
        adapter.RegisterTool(tool);

        var handlers = adapter.GetHandlers();
        var handler = handlers.First(h => h.Name == "test_tool_dict");

        var innerParams = new Dictionary<string, JsonElement>
        {
            ["command"] = JsonSerializer.SerializeToElement("test_cmd"),
            ["value"] = JsonSerializer.SerializeToElement("hello")
        };

        var args = new Dictionary<string, JsonElement>
        {
            ["tool"] = JsonSerializer.SerializeToElement("test_tool"),
            ["parameters"] = JsonSerializer.SerializeToElement(innerParams)
        };

        var result = await handler.ExecuteAsync(args);

        result.Should().NotBeNull();
        tool.LastTool.Should().Be("test_tool");
        tool.LastParameters.Should().NotBeNull();
        tool.LastParameters!.Should().ContainKey("command");
        tool.LastParameters!["command"].GetString().Should().Be("test_cmd");
    }

    #endregion

    #region Test Tool Stubs

    private sealed class TestToolWithParameters
    {
        public string? LastName { get; private set; }
        public int LastCount { get; private set; }

        [McpTool("test_tool_with_params", "Test tool with parameters")]
        public string Execute(
            [McpParameter("The name")] string name,
            [McpParameter("The count")] int count)
        {
            LastName = name;
            LastCount = count;
            return $"name={name}, count={count}";
        }
    }

    private sealed class TestToolNoParams
    {
        [McpTool("test_tool_no_params", "Test tool with no parameters")]
        public string Execute() => "ok";
    }

    private sealed class TestToolWithOptionalParam
    {
        public string? LastName { get; private set; }
        public int LastLimit { get; private set; }

        [McpTool("test_tool_optional", "Test tool with optional param")]
        public string Execute(
            [McpParameter("The name")] string name,
            [McpParameter("The limit")] int limit = 10)
        {
            LastName = name;
            LastLimit = limit;
            return $"name={name}, limit={limit}";
        }
    }

    private sealed class TestToolWithDictParam
    {
        public string? LastTool { get; private set; }
        public Dictionary<string, JsonElement>? LastParameters { get; private set; }

        [McpTool("test_tool_dict", "Test tool with dict param")]
        public string Execute(
            [McpParameter("Tool name")] string tool,
            [McpParameter("Tool parameters")] Dictionary<string, JsonElement> parameters)
        {
            LastTool = tool;
            LastParameters = parameters;
            return $"tool={tool}";
        }
    }

    private sealed class TestToolWithDescriptions
    {
        [McpTool("test_tool_desc", "Test tool with param descriptions")]
        public string Execute(
            [McpParameter("Plugin name to describe")] string pluginName)
        {
            return $"plugin={pluginName}";
        }
    }

    private sealed class TestToolWithBoolParam
    {
        [McpTool("test_tool_bool", "Test tool with bool param")]
        public string Execute(
            [McpParameter("Enabled flag")] bool enabled)
        {
            return $"enabled={enabled}";
        }
    }

    private sealed class TestToolWithDoubleParam
    {
        [McpTool("test_tool_double", "Test tool with double param")]
        public string Execute(
            [McpParameter("The value")] double value)
        {
            return $"value={value}";
        }
    }

    private sealed class TestToolMultiple
    {
        [McpTool("tool_a", "Tool A")]
        public string ExecuteA(
            [McpParameter("Search query")] string query)
        {
            return $"query={query}";
        }

        [McpTool("tool_b", "Tool B")]
        public string ExecuteB(
            [McpParameter("Package name")] string packageName)
        {
            return $"package={packageName}";
        }
    }

    #endregion
}
