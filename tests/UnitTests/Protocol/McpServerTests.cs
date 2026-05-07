using System.Reflection;
using McpProtocol;
using McpProtocol.Contracts;

namespace MyMemoryServer.UnitTests.Protocol;

public sealed class McpServerTests
{
    #region HandleListTools - InputSchema 传播测试

    [Fact]
    public void HandleListTools_ShouldReturnInputSchemaFromHandler()
    {
        var server = new McpServer("TestServer");
        var schemaJson = @"{""type"":""object"",""properties"":{""query"":{""type"":""string"",""description"":""Search query""}},""required"":[""query""]}";
        using var doc = JsonDocument.Parse(schemaJson);
        var schema = doc.RootElement.Clone();

        var handler = new Mock<IToolHandler>();
        handler.SetupGet(h => h.Name).Returns("test_tool");
        handler.SetupGet(h => h.Description).Returns("A test tool");
        handler.SetupGet(h => h.InputSchema).Returns(schema);

        server.RegisterToolHandler(handler.Object);

        var result = InvokeHandleListTools(server);

        result.Tools.Should().HaveCount(1);
        var tool = result.Tools[0];
        tool.Name.Should().Be("test_tool");

        tool.InputSchema.ValueKind.Should().Be(JsonValueKind.Object);
        tool.InputSchema.GetProperty("properties").GetProperty("query").GetProperty("type").GetString().Should().Be("string");
        tool.InputSchema.GetProperty("required").EnumerateArray().First().GetString().Should().Be("query");
    }

    [Fact]
    public void HandleListTools_WithMultipleHandlers_ShouldReturnDistinctSchemas()
    {
        var server = new McpServer("TestServer");

        var schema1Json = @"{""type"":""object"",""properties"":{""query"":{""type"":""string""}},""required"":[""query""]}";
        var schema2Json = @"{""type"":""object"",""properties"":{""pluginName"":{""type"":""string""},""limit"":{""type"":""integer""}},""required"":[""pluginName""]}";

        using var doc1 = JsonDocument.Parse(schema1Json);
        using var doc2 = JsonDocument.Parse(schema2Json);

        var handler1 = new Mock<IToolHandler>();
        handler1.SetupGet(h => h.Name).Returns("tool_search");
        handler1.SetupGet(h => h.Description).Returns("Search tools");
        handler1.SetupGet(h => h.InputSchema).Returns(doc1.RootElement.Clone());

        var handler2 = new Mock<IToolHandler>();
        handler2.SetupGet(h => h.Name).Returns("tool_describe");
        handler2.SetupGet(h => h.Description).Returns("Describe tool");
        handler2.SetupGet(h => h.InputSchema).Returns(doc2.RootElement.Clone());

        server.RegisterToolHandler(handler1.Object);
        server.RegisterToolHandler(handler2.Object);

        var result = InvokeHandleListTools(server);

        result.Tools.Should().HaveCount(2);

        var searchTool = result.Tools.First(t => t.Name == "tool_search");
        searchTool.InputSchema.GetProperty("properties").GetProperty("query").GetProperty("type").GetString().Should().Be("string");

        var describeTool = result.Tools.First(t => t.Name == "tool_describe");
        describeTool.InputSchema.GetProperty("properties").GetProperty("pluginName").GetProperty("type").GetString().Should().Be("string");
        describeTool.InputSchema.GetProperty("properties").GetProperty("limit").GetProperty("type").GetString().Should().Be("integer");
    }

    [Fact]
    public void HandleListTools_WithNoParametersTool_ShouldReturnEmptyPropertiesSchema()
    {
        var server = new McpServer("TestServer");

        var schemaJson = @"{""type"":""object"",""properties"":{},""required"":[]}";
        using var doc = JsonDocument.Parse(schemaJson);

        var handler = new Mock<IToolHandler>();
        handler.SetupGet(h => h.Name).Returns("tool_list");
        handler.SetupGet(h => h.Description).Returns("List tools");
        handler.SetupGet(h => h.InputSchema).Returns(doc.RootElement.Clone());

        server.RegisterToolHandler(handler.Object);

        var result = InvokeHandleListTools(server);

        result.Tools.Should().HaveCount(1);
        var tool = result.Tools[0];
        tool.InputSchema.GetProperty("properties").EnumerateObject().Should().BeEmpty();
        tool.InputSchema.GetProperty("required").EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public void HandleListTools_WithToolExecuteSchema_ShouldContainToolAndParameters()
    {
        var server = new McpServer("TestServer");

        var schemaJson = @"{""type"":""object"",""properties"":{""tool"":{""type"":""string"",""description"":""Tool/Command name""},""parameters"":{""type"":""object"",""description"":""Tool parameters as JSON object""}},""required"":[""tool"",""parameters""]}";
        using var doc = JsonDocument.Parse(schemaJson);

        var handler = new Mock<IToolHandler>();
        handler.SetupGet(h => h.Name).Returns("tool_execute");
        handler.SetupGet(h => h.Description).Returns("Execute a tool");
        handler.SetupGet(h => h.InputSchema).Returns(doc.RootElement.Clone());

        server.RegisterToolHandler(handler.Object);

        var result = InvokeHandleListTools(server);

        var tool = result.Tools[0];
        var props = tool.InputSchema.GetProperty("properties");

        props.GetProperty("tool").GetProperty("type").GetString().Should().Be("string");
        props.GetProperty("parameters").GetProperty("type").GetString().Should().Be("object");

        var required = tool.InputSchema.GetProperty("required").EnumerateArray().Select(r => r.GetString()).ToList();
        required.Should().BeEquivalentTo("tool", "parameters");
    }

    [Fact]
    public void HandleListTools_InputSchemaShouldNotBeEmptyObject()
    {
        var server = new McpServer("TestServer");

        var schemaJson = @"{""type"":""object"",""properties"":{""pluginName"":{""type"":""string"",""description"":""Plugin name""}},""required"":[""pluginName""]}";
        using var doc = JsonDocument.Parse(schemaJson);

        var handler = new Mock<IToolHandler>();
        handler.SetupGet(h => h.Name).Returns("tool_describe");
        handler.SetupGet(h => h.Description).Returns("Describe a plugin");
        handler.SetupGet(h => h.InputSchema).Returns(doc.RootElement.Clone());

        server.RegisterToolHandler(handler.Object);

        var result = InvokeHandleListTools(server);

        var tool = result.Tools[0];
        var props = tool.InputSchema.GetProperty("properties");
        props.EnumerateObject().Should().NotBeEmpty("InputSchema should not be empty - this was the bug where HandleListTools returned CreateEmptyInputSchema()");
    }

    #endregion

    private static ListToolsResult InvokeHandleListTools(McpServer server)
    {
        var method = typeof(McpServer).GetMethod("HandleListTools", BindingFlags.NonPublic | BindingFlags.Instance);
        method.Should().NotBeNull("HandleListTools method should exist on McpServer");
        return (ListToolsResult)method!.Invoke(server, null)!;
    }
}
