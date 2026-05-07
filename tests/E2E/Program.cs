namespace MyMemoryServer.E2E;

class Program
{
    private static readonly string ServerName = "McpHost";

    static async Task Main(string[] args)
    {
        Console.WriteLine($"=== {ServerName} E2E Test ===");
        Console.WriteLine();

        var testResults = new List<TestResult>();

        // 查找服务器可执行文件
        var serverPath = FindServerExecutable();
        if (string.IsNullOrEmpty(serverPath))
        {
            Console.WriteLine("服务器未构建，请先运行: dotnet build src/McpHost/McpHost.csproj -c Release");
            return;
        }

        // 查找 MemoryCli 可执行文件
        var memoryCliPath = FindMemoryCliExecutable();
        if (string.IsNullOrEmpty(memoryCliPath))
        {
            Console.WriteLine("MemoryCli 未构建，请先运行: dotnet build src/Plugins/MemoryCli/MemoryCli.csproj -c Release");
            return;
        }

        // 查找 FileReaderCli 可执行文件
        var fileReaderCliPath = FindFileReaderCliExecutable();
        if (string.IsNullOrEmpty(fileReaderCliPath))
        {
            Console.WriteLine("FileReaderCli 未构建，请先运行: dotnet build src/FileReaderCli/FileReaderCli.csproj -c Release");
            return;
        }

        // 查找 AstCli 可执行文件
        var astCliPath = FindAstCliExecutable();
        if (string.IsNullOrEmpty(astCliPath))
        {
            Console.WriteLine("AstCli 未构建，请先运行: dotnet build src/Plugins/AstCli/AstCli.csproj -c Release");
            return;
        }

        // 使用临时测试目录
        var testBaseDir = Path.Combine(Path.GetTempPath(), $"McpHost_E2E_{DateTime.Now:yyyyMMdd_HHmmss}");
        var testTimestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var testMemoryPath = Path.Combine(testBaseDir, "memory.jsonl");
        var testRelationPath = Path.Combine(testBaseDir, "memory_relations.jsonl");

        // 设置环境变量让 MemoryCli 使用测试目录
        var environmentVariables = new Dictionary<string, string>
        {
            ["MCP_MEMORY_PATH"] = testBaseDir
        };

        using var process = new Process();
        process.StartInfo.FileName = serverPath;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardInput = true;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;

        foreach (var env in environmentVariables)
        {
            process.StartInfo.EnvironmentVariables[env.Key] = env.Value;
        }

        var outputBuffer = new List<string>();
        var responseTcs = new TaskCompletionSource<string>();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                outputBuffer.Add(e.Data);
                Console.WriteLine($"[SERVER] {e.Data}");
                if (!responseTcs.Task.IsCompleted)
                {
                    responseTcs.TrySetResult(e.Data);
                }
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                Console.WriteLine($"[ERROR] {e.Data}");
            }
        };

        Console.WriteLine($"Starting server: {serverPath}");
        Console.WriteLine($"Test memory directory: {testBaseDir}");
        Console.WriteLine();

        EnsureDirectory(testBaseDir);
        CreateTestMemoryFiles(testBaseDir, testTimestamp);

        DeployAstCliToServer(serverPath, astCliPath);
        DeployCliToServer(serverPath, memoryCliPath, "MemoryCli");
        DeployCliToServer(serverPath, fileReaderCliPath, "FileReaderCli");

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var writer = process.StandardInput;

        // 等待服务器启动
        await Task.Delay(500);

        try
        {
            // ============================================
            // MCP Protocol Tests
            // ============================================

            // Test 1: Initialize
            testResults.Add(await RunTestAsync("Initialize", async () =>
            {
                var request = new JsonRpcRequest
                {
                    Id = 1,
                    Method = "initialize",
                    Params = new InitializeRequestParams
                    {
                        ProtocolVersion = "2024-11-05",
                        Capabilities = new { },
                        ClientInfo = new Implementation { Name = "E2ETest", Version = "1.0" }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(5));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                AssertEqual(1, root.GetProperty("id").GetInt32(), "Response ID should match");
                AssertEqual("2024-11-05", root.GetProperty("result").GetProperty("protocolVersion").GetString(), "Protocol version should match");
                AssertEqual(ServerName, root.GetProperty("result").GetProperty("serverInfo").GetProperty("name").GetString(), "Server name should match");

                return true;
            }));

            // Test 2: Tools/List - Should return MCP layer tools
            testResults.Add(await RunTestAsync("Tools/List - MCP Tools", async () =>
            {
                var request = new JsonRpcRequest
                {
                    Id = 2,
                    Method = "tools/list",
                    Params = new { }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(5));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;
                var tools = root.GetProperty("result").GetProperty("tools").EnumerateArray().ToList();

                AssertTrue(tools.Count > 0, "Should have at least one tool");

                var toolNames = tools.Select(t => t.GetProperty("name").GetString()).ToList();
                AssertTrue(toolNames.Contains("tool_search"), "Should have tool_search");
                AssertTrue(toolNames.Contains("tool_execute"), "Should have tool_execute");
                AssertTrue(toolNames.Contains("tool_describe"), "Should have tool_describe");
                AssertTrue(toolNames.Contains("package_status"), "Should have package_status");

                return true;
            }));

            // Test 2.5: Tools/List - InputSchema should NOT be empty (regression test for InputSchema bug)
            testResults.Add(await RunTestAsync("Tools/List - InputSchema Not Empty", async () =>
            {
                var request = new JsonRpcRequest
                {
                    Id = 2,
                    Method = "tools/list",
                    Params = new { }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(5));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;
                var tools = root.GetProperty("result").GetProperty("tools").EnumerateArray().ToList();

                foreach (var tool in tools)
                {
                    var toolName = tool.GetProperty("name").GetString();
                    AssertTrue(tool.TryGetProperty("inputSchema", out var schema), $"Tool {toolName} should have inputSchema");
                    AssertTrue(schema.GetProperty("type").GetString() == "object", $"Tool {toolName} inputSchema type should be 'object'");
                    AssertTrue(schema.TryGetProperty("properties", out var props), $"Tool {toolName} inputSchema should have properties");
                }

                var toolExecute = tools.First(t => t.GetProperty("name").GetString() == "tool_execute");
                var executeSchema = toolExecute.GetProperty("inputSchema");
                var executeProps = executeSchema.GetProperty("properties");
                AssertTrue(executeProps.TryGetProperty("tool", out _), "tool_execute should have 'tool' property in schema");
                AssertTrue(executeProps.TryGetProperty("parameters", out _), "tool_execute should have 'parameters' property in schema");
                var executeRequired = executeSchema.GetProperty("required").EnumerateArray().Select(r => r.GetString()).ToList();
                AssertTrue(executeRequired.Contains("tool"), "tool_execute should require 'tool'");
                AssertTrue(executeRequired.Contains("parameters"), "tool_execute should require 'parameters'");

                var toolDescribe = tools.First(t => t.GetProperty("name").GetString() == "tool_describe");
                var describeSchema = toolDescribe.GetProperty("inputSchema");
                var describeProps = describeSchema.GetProperty("properties");
                AssertTrue(describeProps.TryGetProperty("pluginName", out _), "tool_describe should have 'pluginName' property in schema");

                return true;
            }));

            // ============================================
            // MCP Layer Tool Tests
            // ============================================

            // Test 3: tool_search
            testResults.Add(await RunTestAsync("MCP Tool - tool_search", async () =>
            {
                var request = new JsonRpcRequest
                {
                    Id = 3,
                    Method = "tools/call",
                    Params = new CallToolRequestParams
                    {
                        Name = "tool_search",
                        Arguments = new { query = "memory" }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(5));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                AssertEqual(3, root.GetProperty("id").GetInt32(), "Response ID should match");
                AssertFalse(root.GetProperty("result").GetProperty("isError").GetBoolean(), "Should not be error");

                var content = root.GetProperty("result").GetProperty("content").EnumerateArray().First();
                var text = content.GetProperty("text").GetString();
                
                var searchResults = JsonSerializer.Deserialize(text!, CommonJsonContext.Default.ToolListResult);
                AssertTrue(searchResults?.Plugins.Count > 0, "Should find memory-related tools");
                AssertTrue(searchResults!.Plugins.Any(p => p.Name.Contains("memory", StringComparison.OrdinalIgnoreCase)), "Should find memory_cli plugin");

                return true;
            }));

            // Test 4: tool_describe
            testResults.Add(await RunTestAsync("MCP Tool - tool_describe", async () =>
            {
                var request = new JsonRpcRequest
                {
                    Id = 4,
                    Method = "tools/call",
                    Params = new CallToolRequestParams
                    {
                        Name = "tool_describe",
                        Arguments = new { pluginName = "memory_cli" }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(5));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                AssertEqual(4, root.GetProperty("id").GetInt32(), "Response ID should match");
                AssertFalse(root.GetProperty("result").GetProperty("isError").GetBoolean(), "Should not be error");

                var content = root.GetProperty("result").GetProperty("content").EnumerateArray().First();
                var text = content.GetProperty("text").GetString();
                AssertTrue(text?.Contains("memory_create_entities") ?? false, "Should return tool details");

                return true;
            }));

            // Test 5: package_status
            testResults.Add(await RunTestAsync("MCP Tool - package_status", async () =>
            {
                var request = new JsonRpcRequest
                {
                    Id = 5,
                    Method = "tools/call",
                    Params = new CallToolRequestParams
                    {
                        Name = "package_status",
                        Arguments = new { }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(5));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                AssertEqual(5, root.GetProperty("id").GetInt32(), "Response ID should match");
                AssertFalse(root.GetProperty("result").GetProperty("isError").GetBoolean(), "Should not be error");

                var content = root.GetProperty("result").GetProperty("content").EnumerateArray().First();
                var text = content.GetProperty("text").GetString();
                AssertTrue(text?.Contains("isInstalled") ?? false, "Should return package status");

                return true;
            }));

            // Test 6: package_install - nonexistent package (error handling)
            testResults.Add(await RunTestAsync("MCP Tool - package_install (nonexistent)", async () =>
            {
                var request = new JsonRpcRequest
                {
                    Id = 6,
                    Method = "tools/call",
                    Params = new CallToolRequestParams
                    {
                        Name = "package_install",
                        Arguments = new
                        {
                            packageName = "nonexistent-test-package-xyz-12345"
                        }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(30));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                AssertEqual(6, root.GetProperty("id").GetInt32(), "Response ID should match");
                AssertFalse(root.GetProperty("result").GetProperty("isError").GetBoolean(), "Should not be MCP error");

                var content = root.GetProperty("result").GetProperty("content").EnumerateArray().First();
                var text = content.GetProperty("text").GetString();
                AssertTrue(text?.Contains("Failed to install") ?? false, "Should report installation failure for nonexistent package");

                return true;
            }));

            // ============================================
            // CLI Layer Tool Tests (via tool_execute)
            // ============================================

            // Test 7: tool_execute - memory_create_entities
            testResults.Add(await RunTestAsync("CLI Tool - memory_create_entities", async () =>
            {
                var request = new JsonRpcRequest
                {
                    Id = 7,
                    Method = "tools/call",
                    Params = new CallToolRequestParams
                    {
                        Name = "tool_execute",
                        Arguments = new
                        {
                            tool = "memory_create_entities",
                            parameters = new
                            {
                                command = "create_entities",
                                entities = new[]
                                {
                                    new { name = "E2ETestEntity1", entityType = "test", observations = new[] { "Test observation" } }
                                }
                            }
                        }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(10));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                AssertEqual(7, root.GetProperty("id").GetInt32(), "Response ID should match");
                AssertFalse(root.GetProperty("result").GetProperty("isError").GetBoolean(), "Should not be error");

                return true;
            }));

            // Test 8: tool_execute - memory_search_nodes
            testResults.Add(await RunTestAsync("CLI Tool - memory_search_nodes", async () =>
            {
                var request = new JsonRpcRequest
                {
                    Id = 8,
                    Method = "tools/call",
                    Params = new CallToolRequestParams
                    {
                        Name = "tool_execute",
                        Arguments = new
                        {
                            tool = "memory_search_nodes",
                            parameters = new
                            {
                                command = "search_nodes",
                                query = "MainEntity"
                            }
                        }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(10));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                AssertEqual(8, root.GetProperty("id").GetInt32(), "Response ID should match");
                AssertFalse(root.GetProperty("result").GetProperty("isError").GetBoolean(), "Should not be error");

                var content = root.GetProperty("result").GetProperty("content").EnumerateArray().First();
                var text = content.GetProperty("text").GetString();
                AssertTrue(text?.Contains("MainEntity") ?? false, "Should find the pre-created entity from test data");

                return true;
            }));

            // Test 9: tool_execute - memory_read_graph
            testResults.Add(await RunTestAsync("CLI Tool - memory_read_graph", async () =>
            {
                var request = new JsonRpcRequest
                {
                    Id = 9,
                    Method = "tools/call",
                    Params = new CallToolRequestParams
                    {
                        Name = "tool_execute",
                        Arguments = new
                        {
                            tool = "memory_read_graph",
                            parameters = new
                            {
                                command = "read_graph"
                            }
                        }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(10));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                AssertEqual(9, root.GetProperty("id").GetInt32(), "Response ID should match");
                AssertFalse(root.GetProperty("result").GetProperty("isError").GetBoolean(), "Should not be error");

                return true;
            }));

            // Test 10: Knowledge Graph Triplet - Create Relations
            testResults.Add(await RunTestAsync("CLI Tool - Create Relations", async () =>
            {
                // 先创建第二个实体
                var createRequest = new JsonRpcRequest
                {
                    Id = 10,
                    Method = "tools/call",
                    Params = new CallToolRequestParams
                    {
                        Name = "tool_execute",
                        Arguments = new
                        {
                            tool = "memory_create_entities",
                            parameters = new
                            {
                                command = "create_entities",
                                entities = new[]
                                {
                                    new { name = "E2ETestEntity2", entityType = "test", observations = new[] { "Second entity" } }
                                }
                            }
                        }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, createRequest);
                var createResponse = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(10));
                var createDoc = JsonDocument.Parse(createResponse);
                AssertEqual(10, createDoc.RootElement.GetProperty("id").GetInt32(), "Create response ID should match");

                // 创建关系
                var relationRequest = new JsonRpcRequest
                {
                    Id = 11,
                    Method = "tools/call",
                    Params = new CallToolRequestParams
                    {
                        Name = "tool_execute",
                        Arguments = new
                        {
                            tool = "memory_create_relations",
                            parameters = new
                            {
                                command = "create_relations",
                                relations = new[]
                                {
                                    new { from = "E2ETestEntity1", to = "E2ETestEntity2", relationType = "related_to" }
                                }
                            }
                        }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, relationRequest);
                var relationResponse = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(10));

                var relationDoc = JsonDocument.Parse(relationResponse);
                var relationRoot = relationDoc.RootElement;

                AssertEqual(11, relationRoot.GetProperty("id").GetInt32(), "Relation response ID should match");
                AssertFalse(relationRoot.GetProperty("result").GetProperty("isError").GetBoolean(), "Should not be error");

                return true;
            }));

            // ============================================
            // ID Format Tests
            // ============================================

            // Test 11: ID as string
            testResults.Add(await RunTestAsync("ID Format - String ID", async () =>
            {
                var request = new JsonRpcRequest
                {
                    Id = "string-id-001",
                    Method = "initialize",
                    Params = new InitializeRequestParams
                    {
                        ProtocolVersion = "2024-11-05",
                        Capabilities = new { },
                        ClientInfo = new Implementation { Name = "E2ETest", Version = "1.0" }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(5));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                var responseId = root.GetProperty("id").GetString();
                AssertEqual("string-id-001", responseId, "Response ID should match string request ID");

                return true;
            }));

            // Test 12: ID as large number (long)
            testResults.Add(await RunTestAsync("ID Format - Large Number ID", async () =>
            {
                var request = new JsonRpcRequest
                {
                    Id = 9999999999L,
                    Method = "initialize",
                    Params = new InitializeRequestParams
                    {
                        ProtocolVersion = "2024-11-05",
                        Capabilities = new { },
                        ClientInfo = new Implementation { Name = "E2ETest", Version = "1.0" }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(5));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                var responseId = root.GetProperty("id").GetInt64();
                AssertEqual(9999999999L, responseId, "Response ID should match long request ID");

                return true;
            }));

            // Test 13: Notification message (no ID)
            testResults.Add(await RunTestAsync("ID Format - Notification (No ID)", async () =>
            {
                var request = new JsonRpcRequest
                {
                    Id = null,
                    Method = "initialized",
                    Params = new { }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);

                var delayTask = Task.Delay(1000);
                var completedTask = await Task.WhenAny(responseTcs.Task, delayTask);

                if (completedTask == delayTask)
                {
                    return true;
                }

                throw new AssertionException("Notifications should not receive responses");
            }));

            // ============================================
            // Error Handling Tests
            // ============================================

            // Test 14: Unknown tool
            testResults.Add(await RunTestAsync("Error Handling - Unknown Tool", async () =>
            {
                var request = new JsonRpcRequest
                {
                    Id = 100,
                    Method = "tools/call",
                    Params = new CallToolRequestParams
                    {
                        Name = "nonexistent_tool",
                        Arguments = new { }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(5));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                AssertEqual(100, root.GetProperty("id").GetInt32(), "Response ID should match");
                AssertTrue(root.GetProperty("result").GetProperty("isError").GetBoolean(), "Should be error");

                return true;
            }));

            // Test 15: JSON-RPC Error Response Format
            testResults.Add(await RunTestAsync("JSON-RPC Format - Error Response", async () =>
            {
                var request = new JsonRpcRequest
                {
                    Id = 101,
                    Method = "nonexistent_method",
                    Params = new { }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(5));

                AssertFalse(response.Contains("\"result\":null"), "Error response should not contain 'result':null");
                AssertTrue(response.Contains("\"error\""), "Error response should contain 'error' field");

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                AssertEqual(101, root.GetProperty("id").GetInt32(), "Response ID should match");
                AssertTrue(root.TryGetProperty("error", out var errorProp), "Error response should have 'error' property");
                AssertTrue(errorProp.TryGetProperty("code", out _), "Error should have 'code' property");
                AssertTrue(errorProp.TryGetProperty("message", out _), "Error should have 'message' property");

                return true;
            }));

            // Test 16: Malformed JSON-RPC
            testResults.Add(await RunTestAsync("Error Handling - Malformed JSON", async () =>
            {
                await writer.WriteLineAsync("{ invalid json }");

                responseTcs = new TaskCompletionSource<string>();
                var delayTask = Task.Delay(3000);
                var completedTask = await Task.WhenAny(responseTcs.Task, delayTask);

                if (completedTask == delayTask)
                {
                    return true;
                }

                var response = await responseTcs.Task;
                AssertTrue(response.Contains("error"), "Malformed JSON should return error response");

                return true;
            }));

            // Test 17: Invalid Parameters
            testResults.Add(await RunTestAsync("Error Handling - Invalid Parameters", async () =>
            {
                var request = new JsonRpcRequest
                {
                    Id = 102,
                    Method = "tools/call",
                    Params = new CallToolRequestParams
                    {
                        Name = "tool_execute",
                        Arguments = new
                        {
                            tool = "memory_create_entities",
                            parameters = new
                            {
                                command = "create_entities",
                                entities = "invalid_type"
                            }
                        }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(10));

                AssertTrue(response.Length > 0, "Should return a response even for invalid parameters");

                return true;
            }));

            // Test 18: Timeout Handling
            testResults.Add(await RunTestAsync("Error Handling - Timeout Graceful", async () =>
            {
                responseTcs = new TaskCompletionSource<string>();

                try
                {
                    await WaitForResponseAsync(responseTcs, TimeSpan.FromMilliseconds(10));
                }
                catch (OperationCanceledException)
                {
                    return true;
                }

                return true;
            }));

            // ============================================
            // FileReaderCli Tool Tests (via tool_execute)
            // ============================================

            // Test 19: FileReaderCli - tool_search should find file_reader tools
            testResults.Add(await RunTestAsync("FileReaderCli - tool_search", async () =>
            {
                var request = new JsonRpcRequest
                {
                    Id = 19,
                    Method = "tools/call",
                    Params = new CallToolRequestParams
                    {
                        Name = "tool_search",
                        Arguments = new { query = "file" }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(5));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                AssertEqual(19, root.GetProperty("id").GetInt32(), "Response ID should match");
                AssertFalse(root.GetProperty("result").GetProperty("isError").GetBoolean(), "Should not be error");

                var content = root.GetProperty("result").GetProperty("content").EnumerateArray().First();
                var text = content.GetProperty("text").GetString();
                
                var searchResults = JsonSerializer.Deserialize(text!, CommonJsonContext.Default.ToolListResult);
                AssertTrue(searchResults?.Plugins.Count > 0, "Should find file-related tools");
                AssertTrue(searchResults!.Plugins.Any(p => p.Name.Contains("file_reader", StringComparison.OrdinalIgnoreCase)), "Should find file_reader_cli plugin");

                return true;
            }));

            // Test 20: FileReaderCli - file_reader_read_head
            testResults.Add(await RunTestAsync("FileReaderCli - read_head", async () =>
            {
                // 创建一个测试文件
                var testFilePath = Path.Combine(testBaseDir, "test_read_file.txt");
                File.WriteAllLines(testFilePath, new[]
                {
                    "Line 1: First line",
                    "Line 2: Second line",
                    "Line 3: Third line",
                    "Line 4: Fourth line",
                    "Line 5: Fifth line"
                });

                var request = new JsonRpcRequest
                {
                    Id = 20,
                    Method = "tools/call",
                    Params = new CallToolRequestParams
                    {
                        Name = "tool_execute",
                        Arguments = new
                        {
                            tool = "file_reader_read_head",
                            parameters = new
                            {
                                command = "read_head",
                                filePath = testFilePath,
                                lineCount = 3
                            }
                        }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(10));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                AssertEqual(20, root.GetProperty("id").GetInt32(), "Response ID should match");
                AssertFalse(root.GetProperty("result").GetProperty("isError").GetBoolean(), "Should not be error");

                var content = root.GetProperty("result").GetProperty("content").EnumerateArray().First();
                var text = content.GetProperty("text").GetString();
                AssertTrue(text?.Contains("Line 1") ?? false, "Should contain Line 1");
                AssertTrue(text?.Contains("Line 2") ?? false, "Should contain Line 2");
                AssertTrue(text?.Contains("Line 3") ?? false, "Should contain Line 3");
                AssertFalse(text?.Contains("Line 5") ?? false, "Should not contain Line 5 (only reading first 3 lines)");

                return true;
            }));

            // Test 21: FileReaderCli - file_reader_read_tail
            testResults.Add(await RunTestAsync("FileReaderCli - read_tail", async () =>
            {
                // 创建一个测试文件
                var testFilePath = Path.Combine(testBaseDir, "test_read_file.txt");
                // 文件已在上一个测试中创建

                var request = new JsonRpcRequest
                {
                    Id = 21,
                    Method = "tools/call",
                    Params = new CallToolRequestParams
                    {
                        Name = "tool_execute",
                        Arguments = new
                        {
                            tool = "file_reader_read_tail",
                            parameters = new
                            {
                                command = "read_tail",
                                filePath = testFilePath,
                                lineCount = 2
                            }
                        }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(10));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                AssertEqual(21, root.GetProperty("id").GetInt32(), "Response ID should match");
                AssertFalse(root.GetProperty("result").GetProperty("isError").GetBoolean(), "Should not be error");

                var content = root.GetProperty("result").GetProperty("content").EnumerateArray().First();
                var text = content.GetProperty("text").GetString();
                AssertTrue(text?.Contains("Line 4") ?? false, "Should contain Line 4");
                AssertTrue(text?.Contains("Line 5") ?? false, "Should contain Line 5");
                AssertFalse(text?.Contains("Line 1") ?? false, "Should not contain Line 1 (only reading last 2 lines)");

                return true;
            }));

            // Test 22: FileReaderCli - Error handling for nonexistent file
            testResults.Add(await RunTestAsync("FileReaderCli - Nonexistent File Error", async () =>
            {
                var request = new JsonRpcRequest
                {
                    Id = 22,
                    Method = "tools/call",
                    Params = new CallToolRequestParams
                    {
                        Name = "tool_execute",
                        Arguments = new
                        {
                            tool = "file_reader_read_head",
                            parameters = new
                            {
                                command = "read_head",
                                filePath = "C:\\Nonexistent\\Path\\File.txt",
                                lineCount = 10
                            }
                        }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(10));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                AssertEqual(22, root.GetProperty("id").GetInt32(), "Response ID should match");
                AssertFalse(root.GetProperty("result").GetProperty("isError").GetBoolean(), "Should not be MCP error (CLI handles the error)");

                var content = root.GetProperty("result").GetProperty("content").EnumerateArray().First();
                var text = content.GetProperty("text").GetString();
                AssertTrue(text?.Contains("success") ?? false, "Response should contain success field");
                AssertTrue(text?.Contains("false") ?? false, "Response should indicate failure");

                return true;
            }));

            // Test 23: Both CLI tools available via tool_list
            testResults.Add(await RunTestAsync("Both CLIs - tool_list", async () =>
            {
                var request = new JsonRpcRequest
                {
                    Id = 23,
                    Method = "tools/call",
                    Params = new CallToolRequestParams
                    {
                        Name = "tool_list",
                        Arguments = new { }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(5));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                AssertEqual(23, root.GetProperty("id").GetInt32(), "Response ID should match");
                AssertFalse(root.GetProperty("result").GetProperty("isError").GetBoolean(), "Should not be error");

                var content = root.GetProperty("result").GetProperty("content").EnumerateArray().First();
                var text = content.GetProperty("text").GetString();
                
                var listResult = JsonSerializer.Deserialize(text!, CommonJsonContext.Default.ToolListResult);
                AssertTrue(listResult?.TotalPlugins >= 3, "Should have at least 3 plugins");
                AssertTrue(listResult!.Plugins.Any(p => p.Name == "memory_cli"), "Should have memory_cli plugin");
                AssertTrue(listResult.Plugins.Any(p => p.Name == "file_reader_cli"), "Should have file_reader_cli plugin");
                AssertTrue(listResult.Plugins.Any(p => p.Name == "ast_cli"), "Should have ast_cli plugin");

                return true;
            }));

            // ============================================
            // MemoryCli - Missing Command Tests
            // ============================================

            // Test 24: memory_add_observations - add observations to existing entity
            testResults.Add(await RunTestAsync("MemoryCli - add_observations", async () =>
            {
                var request = new JsonRpcRequest
                {
                    Id = 24,
                    Method = "tools/call",
                    Params = new CallToolRequestParams
                    {
                        Name = "tool_execute",
                        Arguments = new
                        {
                            tool = "memory_add_observations",
                            parameters = new
                            {
                                command = "add_observations",
                                name = "E2ETestEntity1",
                                observations = new[] { "Added observation 1", "Added observation 2" }
                            }
                        }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(10));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                AssertEqual(24, root.GetProperty("id").GetInt32(), "Response ID should match");
                AssertFalse(root.GetProperty("result").GetProperty("isError").GetBoolean(), "Should not be error");

                var content = root.GetProperty("result").GetProperty("content").EnumerateArray().First();
                var text = content.GetProperty("text").GetString();
                AssertTrue(text?.Contains("success") ?? false, "Should contain success field");
                AssertTrue(text?.Contains("true") ?? false, "Should indicate success");

                return true;
            }));

            // Test 25: memory_add_observations - entity not found
            testResults.Add(await RunTestAsync("MemoryCli - add_observations (entity not found)", async () =>
            {
                var request = new JsonRpcRequest
                {
                    Id = 25,
                    Method = "tools/call",
                    Params = new CallToolRequestParams
                    {
                        Name = "tool_execute",
                        Arguments = new
                        {
                            tool = "memory_add_observations",
                            parameters = new
                            {
                                command = "add_observations",
                                name = "NonExistentEntity",
                                observations = new[] { "Should fail" }
                            }
                        }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(10));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                AssertEqual(25, root.GetProperty("id").GetInt32(), "Response ID should match");
                AssertFalse(root.GetProperty("result").GetProperty("isError").GetBoolean(), "Should not be MCP error (CLI handles the error)");

                var content = root.GetProperty("result").GetProperty("content").EnumerateArray().First();
                var text = content.GetProperty("text").GetString();
                AssertTrue(text?.Contains("false") ?? false, "Should indicate failure");
                AssertTrue(text?.Contains("not found") ?? false, "Should mention entity not found");

                return true;
            }));

            // Test 26: memory_open_nodes - get specific nodes by name
            testResults.Add(await RunTestAsync("MemoryCli - open_nodes", async () =>
            {
                var request = new JsonRpcRequest
                {
                    Id = 26,
                    Method = "tools/call",
                    Params = new CallToolRequestParams
                    {
                        Name = "tool_execute",
                        Arguments = new
                        {
                            tool = "memory_open_nodes",
                            parameters = new
                            {
                                command = "open_nodes",
                                names = new[] { "E2ETestEntity1", "E2ETestEntity2" }
                            }
                        }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(10));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                AssertEqual(26, root.GetProperty("id").GetInt32(), "Response ID should match");
                AssertFalse(root.GetProperty("result").GetProperty("isError").GetBoolean(), "Should not be error");

                var content = root.GetProperty("result").GetProperty("content").EnumerateArray().First();
                var text = content.GetProperty("text").GetString();
                AssertTrue(text?.Contains("E2ETestEntity1") ?? false, "Should find E2ETestEntity1");
                AssertTrue(text?.Contains("E2ETestEntity2") ?? false, "Should find E2ETestEntity2");

                return true;
            }));

            // Test 27: memory_open_nodes - empty names returns empty result
            testResults.Add(await RunTestAsync("MemoryCli - open_nodes (empty names)", async () =>
            {
                var request = new JsonRpcRequest
                {
                    Id = 27,
                    Method = "tools/call",
                    Params = new CallToolRequestParams
                    {
                        Name = "tool_execute",
                        Arguments = new
                        {
                            tool = "memory_open_nodes",
                            parameters = new
                            {
                                command = "open_nodes",
                                names = new string[] { }
                            }
                        }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(10));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                AssertEqual(27, root.GetProperty("id").GetInt32(), "Response ID should match");
                AssertFalse(root.GetProperty("result").GetProperty("isError").GetBoolean(), "Should not be error");

                var content = root.GetProperty("result").GetProperty("content").EnumerateArray().First();
                var text = content.GetProperty("text").GetString();
                AssertTrue(text?.Contains("success") ?? false, "Should contain success field");
                AssertTrue(text?.Contains("true") ?? false, "Should indicate success (empty is valid)");

                return true;
            }));

            // Test 28: memory_get_storage_info
            testResults.Add(await RunTestAsync("MemoryCli - get_storage_info", async () =>
            {
                var request = new JsonRpcRequest
                {
                    Id = 28,
                    Method = "tools/call",
                    Params = new CallToolRequestParams
                    {
                        Name = "tool_execute",
                        Arguments = new
                        {
                            tool = "memory_get_storage_info",
                            parameters = new
                            {
                                command = "get_storage_info"
                            }
                        }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(10));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                AssertEqual(28, root.GetProperty("id").GetInt32(), "Response ID should match");
                AssertFalse(root.GetProperty("result").GetProperty("isError").GetBoolean(), "Should not be error");

                var content = root.GetProperty("result").GetProperty("content").EnumerateArray().First();
                var text = content.GetProperty("text").GetString();
                AssertTrue(text?.Contains("baseDirectory") ?? false, "Should contain baseDirectory");
                AssertTrue(text?.Contains("memoryFilePath") ?? false, "Should contain memoryFilePath");
                AssertTrue(text?.Contains("relationsFilePath") ?? false, "Should contain relationsFilePath");
                AssertTrue(text?.Contains("MCP_MEMORY_PATH") ?? false, "Should contain environment variable name");

                return true;
            }));

            // Test 29: memory_delete_observations - delete specific observations from entity
            testResults.Add(await RunTestAsync("MemoryCli - delete_observations", async () =>
            {
                var request = new JsonRpcRequest
                {
                    Id = 29,
                    Method = "tools/call",
                    Params = new CallToolRequestParams
                    {
                        Name = "tool_execute",
                        Arguments = new
                        {
                            tool = "memory_delete_observations",
                            parameters = new
                            {
                                command = "delete_observations",
                                name = "E2ETestEntity1",
                                observations = new[] { "Added observation 1" }
                            }
                        }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(10));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                AssertEqual(29, root.GetProperty("id").GetInt32(), "Response ID should match");
                AssertFalse(root.GetProperty("result").GetProperty("isError").GetBoolean(), "Should not be error");

                var content = root.GetProperty("result").GetProperty("content").EnumerateArray().First();
                var text = content.GetProperty("text").GetString();
                AssertTrue(text?.Contains("success") ?? false, "Should contain success field");
                AssertTrue(text?.Contains("true") ?? false, "Should indicate success");

                return true;
            }));

            // Test 30: memory_delete_relations - delete a specific relation
            testResults.Add(await RunTestAsync("MemoryCli - delete_relations", async () =>
            {
                var request = new JsonRpcRequest
                {
                    Id = 30,
                    Method = "tools/call",
                    Params = new CallToolRequestParams
                    {
                        Name = "tool_execute",
                        Arguments = new
                        {
                            tool = "memory_delete_relations",
                            parameters = new
                            {
                                command = "delete_relations",
                                relations = new[]
                                {
                                    new { from = "E2ETestEntity1", to = "E2ETestEntity2", relationType = "related_to" }
                                }
                            }
                        }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(10));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                AssertEqual(30, root.GetProperty("id").GetInt32(), "Response ID should match");
                AssertFalse(root.GetProperty("result").GetProperty("isError").GetBoolean(), "Should not be error");

                var content = root.GetProperty("result").GetProperty("content").EnumerateArray().First();
                var text = content.GetProperty("text").GetString();
                AssertTrue(text?.Contains("success") ?? false, "Should contain success field");
                AssertTrue(text?.Contains("true") ?? false, "Should indicate success");
                AssertTrue(text?.Contains("deleted") ?? false, "Should mention deleted count");

                return true;
            }));

            // Test 31: memory_delete_entities - delete entity and cascade delete relations
            testResults.Add(await RunTestAsync("MemoryCli - delete_entities (cascade)", async () =>
            {
                // First create a new entity and relation for clean deletion test
                var createReq = new JsonRpcRequest
                {
                    Id = 31,
                    Method = "tools/call",
                    Params = new CallToolRequestParams
                    {
                        Name = "tool_execute",
                        Arguments = new
                        {
                            tool = "memory_create_entities",
                            parameters = new
                            {
                                command = "create_entities",
                                entities = new[]
                                {
                                    new { name = "ToDeleteEntity", entityType = "temporary", observations = new[] { "Will be deleted" } }
                                }
                            }
                        }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, createReq);
                await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(10));

                // Now delete the entity
                var deleteRequest = new JsonRpcRequest
                {
                    Id = 32,
                    Method = "tools/call",
                    Params = new CallToolRequestParams
                    {
                        Name = "tool_execute",
                        Arguments = new
                        {
                            tool = "memory_delete_entities",
                            parameters = new
                            {
                                command = "delete_entities",
                                names = new[] { "ToDeleteEntity" }
                            }
                        }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, deleteRequest);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(10));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                AssertEqual(32, root.GetProperty("id").GetInt32(), "Response ID should match");
                AssertFalse(root.GetProperty("result").GetProperty("isError").GetBoolean(), "Should not be error");

                var content = root.GetProperty("result").GetProperty("content").EnumerateArray().First();
                var text = content.GetProperty("text").GetString();
                AssertTrue(text?.Contains("success") ?? false, "Should contain success field");
                AssertTrue(text?.Contains("true") ?? false, "Should indicate success");
                AssertTrue(text?.Contains("deleted") ?? false, "Should mention deleted count");

                return true;
            }));

            // ============================================
            // MemoryCli - Validation Error Tests
            // ============================================

            // Test 33: memory_create_entities - invalid entity name (special characters)
            testResults.Add(await RunTestAsync("MemoryCli - create_entities (invalid name)", async () =>
            {
                var request = new JsonRpcRequest
                {
                    Id = 33,
                    Method = "tools/call",
                    Params = new CallToolRequestParams
                    {
                        Name = "tool_execute",
                        Arguments = new
                        {
                            tool = "memory_create_entities",
                            parameters = new
                            {
                                command = "create_entities",
                                entities = new[]
                                {
                                    new { name = "Invalid@Name#1", entityType = "test", observations = new[] { "test" } }
                                }
                            }
                        }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(10));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                AssertEqual(33, root.GetProperty("id").GetInt32(), "Response ID should match");
                AssertFalse(root.GetProperty("result").GetProperty("isError").GetBoolean(), "Should not be MCP error (CLI handles the error)");

                var content = root.GetProperty("result").GetProperty("content").EnumerateArray().First();
                var text = content.GetProperty("text").GetString();
                AssertTrue(text?.Contains("false") ?? false, "Should indicate failure for invalid name");

                return true;
            }));

            // Test 34: memory_create_entities - empty entities list
            testResults.Add(await RunTestAsync("MemoryCli - create_entities (empty list)", async () =>
            {
                var request = new JsonRpcRequest
                {
                    Id = 34,
                    Method = "tools/call",
                    Params = new CallToolRequestParams
                    {
                        Name = "tool_execute",
                        Arguments = new
                        {
                            tool = "memory_create_entities",
                            parameters = new
                            {
                                command = "create_entities",
                                entities = new object[] { }
                            }
                        }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(10));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                AssertEqual(34, root.GetProperty("id").GetInt32(), "Response ID should match");

                var content = root.GetProperty("result").GetProperty("content").EnumerateArray().First();
                var text = content.GetProperty("text").GetString();
                AssertTrue(text?.Contains("false") ?? false, "Should indicate failure for empty entities");

                return true;
            }));

            // Test 35: memory_create_relations - relation to nonexistent entity
            testResults.Add(await RunTestAsync("MemoryCli - create_relations (nonexistent entity)", async () =>
            {
                var request = new JsonRpcRequest
                {
                    Id = 35,
                    Method = "tools/call",
                    Params = new CallToolRequestParams
                    {
                        Name = "tool_execute",
                        Arguments = new
                        {
                            tool = "memory_create_relations",
                            parameters = new
                            {
                                command = "create_relations",
                                relations = new[]
                                {
                                    new { from = "GhostEntity", to = "E2ETestEntity1", relationType = "imagines" }
                                }
                            }
                        }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(10));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                AssertEqual(35, root.GetProperty("id").GetInt32(), "Response ID should match");

                var content = root.GetProperty("result").GetProperty("content").EnumerateArray().First();
                var text = content.GetProperty("text").GetString();
                AssertTrue(text?.Contains("false") ?? false, "Should indicate failure for nonexistent entity in relation");

                return true;
            }));

            // ============================================
            // FileReaderCli - Boundary Tests
            // ============================================

            // Test 36: FileReaderCli - read empty file
            testResults.Add(await RunTestAsync("FileReaderCli - read_head (empty file)", async () =>
            {
                var emptyFilePath = Path.Combine(testBaseDir, "empty_file.txt");
                File.WriteAllText(emptyFilePath, string.Empty);

                var request = new JsonRpcRequest
                {
                    Id = 36,
                    Method = "tools/call",
                    Params = new CallToolRequestParams
                    {
                        Name = "tool_execute",
                        Arguments = new
                        {
                            tool = "file_reader_read_head",
                            parameters = new
                            {
                                command = "read_head",
                                filePath = emptyFilePath,
                                lineCount = 10
                            }
                        }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(10));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                AssertEqual(36, root.GetProperty("id").GetInt32(), "Response ID should match");
                AssertFalse(root.GetProperty("result").GetProperty("isError").GetBoolean(), "Should not be MCP error");

                return true;
            }));

            // Test 37: FileReaderCli - read_head with lineCount exceeding file lines
            testResults.Add(await RunTestAsync("FileReaderCli - read_head (lineCount > total lines)", async () =>
            {
                var smallFilePath = Path.Combine(testBaseDir, "small_file.txt");
                File.WriteAllLines(smallFilePath, new[] { "Only line" });

                var request = new JsonRpcRequest
                {
                    Id = 37,
                    Method = "tools/call",
                    Params = new CallToolRequestParams
                    {
                        Name = "tool_execute",
                        Arguments = new
                        {
                            tool = "file_reader_read_head",
                            parameters = new
                            {
                                command = "read_head",
                                filePath = smallFilePath,
                                lineCount = 100
                            }
                        }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(10));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                AssertEqual(37, root.GetProperty("id").GetInt32(), "Response ID should match");
                AssertFalse(root.GetProperty("result").GetProperty("isError").GetBoolean(), "Should not be error");

                var content = root.GetProperty("result").GetProperty("content").EnumerateArray().First();
                var text = content.GetProperty("text").GetString();
                AssertTrue(text?.Contains("Only line") ?? false, "Should contain the single line");

                return true;
            }));

            // ============================================
            // AstCli Tool Tests (via tool_execute)
            // ============================================

            var astProjectDir = CreateTestAstProject(testBaseDir);

            // Test 38: AstCli - tool_search should find ast tools
            testResults.Add(await RunTestAsync("AstCli - tool_search", async () =>
            {
                var request = new JsonRpcRequest
                {
                    Id = 38,
                    Method = "tools/call",
                    Params = new CallToolRequestParams
                    {
                        Name = "tool_search",
                        Arguments = new { query = "ast" }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(5));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                AssertEqual(38, root.GetProperty("id").GetInt32(), "Response ID should match");
                AssertFalse(root.GetProperty("result").GetProperty("isError").GetBoolean(), "Should not be error");

                var content = root.GetProperty("result").GetProperty("content").EnumerateArray().First();
                var text = content.GetProperty("text").GetString();

                var searchResults = JsonSerializer.Deserialize(text!, CommonJsonContext.Default.ToolListResult);
                AssertTrue(searchResults?.Plugins.Count > 0, "Should find ast-related tools");
                AssertTrue(searchResults!.Plugins.Any(p => p.Name.Contains("ast", StringComparison.OrdinalIgnoreCase)), "Should find ast_cli plugin");

                return true;
            }));

            // Test 39: AstCli - ast_symbol_query
            testResults.Add(await RunTestAsync("AstCli - symbol_query", async () =>
            {
                var request = new JsonRpcRequest
                {
                    Id = 39,
                    Method = "tools/call",
                    Params = new CallToolRequestParams
                    {
                        Name = "tool_execute",
                        Arguments = new
                        {
                            tool = "ast_symbol_query",
                            parameters = new
                            {
                                command = "symbol_query",
                                projectPath = astProjectDir,
                                symbolName = "ServiceA"
                            }
                        }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(15));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                AssertEqual(39, root.GetProperty("id").GetInt32(), "Response ID should match");
                AssertFalse(root.GetProperty("result").GetProperty("isError").GetBoolean(), "Should not be error");

                var content = root.GetProperty("result").GetProperty("content").EnumerateArray().First();
                var text = content.GetProperty("text").GetString();
                AssertTrue(text?.Contains("ServiceA") ?? false, "Should find ServiceA symbol");
                AssertTrue(text?.Contains("Class") ?? false, "Should identify it as a Class");

                return true;
            }));

            // Test 40: AstCli - ast_reference_find
            testResults.Add(await RunTestAsync("AstCli - reference_find", async () =>
            {
                var request = new JsonRpcRequest
                {
                    Id = 40,
                    Method = "tools/call",
                    Params = new CallToolRequestParams
                    {
                        Name = "tool_execute",
                        Arguments = new
                        {
                            tool = "ast_reference_find",
                            parameters = new
                            {
                                command = "reference_find",
                                projectPath = astProjectDir,
                                symbolName = "ServiceA"
                            }
                        }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(15));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                AssertEqual(40, root.GetProperty("id").GetInt32(), "Response ID should match");
                AssertFalse(root.GetProperty("result").GetProperty("isError").GetBoolean(), "Should not be error");

                var content = root.GetProperty("result").GetProperty("content").EnumerateArray().First();
                var text = content.GetProperty("text").GetString();
                AssertTrue(text?.Contains("ServiceA") ?? false, "Should find ServiceA references");
                AssertTrue(text?.Contains("ServiceB") ?? false, "ServiceB references ServiceA");

                return true;
            }));

            // Test 41: AstCli - ast_symbol_info
            testResults.Add(await RunTestAsync("AstCli - symbol_info", async () =>
            {
                var serviceAPath = Path.Combine(astProjectDir, "ServiceA.cs");

                var request = new JsonRpcRequest
                {
                    Id = 41,
                    Method = "tools/call",
                    Params = new CallToolRequestParams
                    {
                        Name = "tool_execute",
                        Arguments = new
                        {
                            tool = "ast_symbol_info",
                            parameters = new
                            {
                                command = "symbol_info",
                                projectPath = astProjectDir,
                                filePath = serviceAPath,
                                lineNumber = 3,
                                columnNumber = 13
                            }
                        }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(15));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                AssertEqual(41, root.GetProperty("id").GetInt32(), "Response ID should match");
                AssertFalse(root.GetProperty("result").GetProperty("isError").GetBoolean(), "Should not be error");

                var content = root.GetProperty("result").GetProperty("content").EnumerateArray().First();
                var text = content.GetProperty("text").GetString();
                AssertTrue(text?.Contains("ServiceA") ?? false, "Should find ServiceA at the position");

                return true;
            }));

            // Test 42: AstCli - ast_symbol_rename
            testResults.Add(await RunTestAsync("AstCli - symbol_rename", async () =>
            {
                var renameProjectDir = CreateTestAstProject(Path.Combine(testBaseDir, "rename_test"));

                var request = new JsonRpcRequest
                {
                    Id = 42,
                    Method = "tools/call",
                    Params = new CallToolRequestParams
                    {
                        Name = "tool_execute",
                        Arguments = new
                        {
                            tool = "ast_symbol_rename",
                            parameters = new
                            {
                                command = "symbol_rename",
                                projectPath = renameProjectDir,
                                symbolName = "ServiceA",
                                newName = "RenamedServiceA"
                            }
                        }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(15));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                AssertEqual(42, root.GetProperty("id").GetInt32(), "Response ID should match");
                AssertFalse(root.GetProperty("result").GetProperty("isError").GetBoolean(), "Should not be error");

                var content = root.GetProperty("result").GetProperty("content").EnumerateArray().First();
                var text = content.GetProperty("text").GetString();
                AssertTrue(text?.Contains("RenamedServiceA") ?? false, "Should mention the new name");
                AssertTrue(text?.Contains("true") ?? false, "Should indicate success");

                var renamedContent = File.ReadAllText(Path.Combine(renameProjectDir, "ServiceA.cs"));
                AssertTrue(renamedContent.Contains("RenamedServiceA"), "File should contain the renamed symbol");
                AssertFalse(renamedContent.Contains("class ServiceA"), "File should not contain old class name");

                var refContent = File.ReadAllText(Path.Combine(renameProjectDir, "ServiceB.cs"));
                AssertTrue(refContent.Contains("RenamedServiceA"), "Referencing file should contain the renamed symbol");

                return true;
            }));

            // Test 43: AstCli - ast_symbol_replace
            testResults.Add(await RunTestAsync("AstCli - symbol_replace", async () =>
            {
                var replaceProjectDir = CreateTestAstProject(Path.Combine(testBaseDir, "replace_test"));

                var request = new JsonRpcRequest
                {
                    Id = 43,
                    Method = "tools/call",
                    Params = new CallToolRequestParams
                    {
                        Name = "tool_execute",
                        Arguments = new
                        {
                            tool = "ast_symbol_replace",
                            parameters = new
                            {
                                command = "symbol_replace",
                                projectPath = replaceProjectDir,
                                symbolName = "Execute",
                                newName = "Process"
                            }
                        }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(15));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                AssertEqual(43, root.GetProperty("id").GetInt32(), "Response ID should match");
                AssertFalse(root.GetProperty("result").GetProperty("isError").GetBoolean(), "Should not be error");

                var content = root.GetProperty("result").GetProperty("content").EnumerateArray().First();
                var text = content.GetProperty("text").GetString();
                AssertTrue(text?.Contains("Process") ?? false, "Should mention the new name");

                var replacedContent = File.ReadAllText(Path.Combine(replaceProjectDir, "ServiceA.cs"));
                AssertTrue(replacedContent.Contains("Process"), "File should contain the replaced symbol");

                return true;
            }));

            // Test 44: AstCli - nonexistent project path error
            testResults.Add(await RunTestAsync("AstCli - nonexistent project path", async () =>
            {
                var request = new JsonRpcRequest
                {
                    Id = 44,
                    Method = "tools/call",
                    Params = new CallToolRequestParams
                    {
                        Name = "tool_execute",
                        Arguments = new
                        {
                            tool = "ast_symbol_query",
                            parameters = new
                            {
                                command = "symbol_query",
                                projectPath = "C:\\Nonexistent\\Path\\Project",
                                symbolName = "Test"
                            }
                        }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(10));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                AssertEqual(44, root.GetProperty("id").GetInt32(), "Response ID should match");
                AssertFalse(root.GetProperty("result").GetProperty("isError").GetBoolean(), "Should not be MCP error (CLI handles the error)");

                var content = root.GetProperty("result").GetProperty("content").EnumerateArray().First();
                var text = content.GetProperty("text").GetString();
                AssertTrue(text?.Contains("false") ?? false, "Should indicate failure");
                AssertTrue(text?.Contains("not found") ?? false, "Should mention path not found");

                return true;
            }));

            // Test 45: AstCli - symbol_query with wildcard
            testResults.Add(await RunTestAsync("AstCli - symbol_query (wildcard)", async () =>
            {
                var request = new JsonRpcRequest
                {
                    Id = 45,
                    Method = "tools/call",
                    Params = new CallToolRequestParams
                    {
                        Name = "tool_execute",
                        Arguments = new
                        {
                            tool = "ast_symbol_query",
                            parameters = new
                            {
                                command = "symbol_query",
                                projectPath = astProjectDir,
                                symbolName = "*"
                            }
                        }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(15));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                AssertEqual(45, root.GetProperty("id").GetInt32(), "Response ID should match");
                AssertFalse(root.GetProperty("result").GetProperty("isError").GetBoolean(), "Should not be error");

                var content = root.GetProperty("result").GetProperty("content").EnumerateArray().First();
                var text = content.GetProperty("text").GetString();
                AssertTrue(text?.Contains("ServiceA") ?? false, "Should find ServiceA");
                AssertTrue(text?.Contains("ServiceB") ?? false, "Should find ServiceB");
                AssertTrue(text?.Contains("IEntity") ?? false, "Should find IEntity");

                return true;
            }));

            // Test 46: AstCli - ast_workspace_overview
            testResults.Add(await RunTestAsync("AstCli - workspace_overview", async () =>
            {
                var request = new JsonRpcRequest
                {
                    Id = 46,
                    Method = "tools/call",
                    Params = new CallToolRequestParams
                    {
                        Name = "tool_execute",
                        Arguments = new
                        {
                            tool = "ast_workspace_overview",
                            parameters = new
                            {
                                command = "workspace_overview",
                                projectPath = astProjectDir
                            }
                        }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(15));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                AssertEqual(46, root.GetProperty("id").GetInt32(), "Response ID should match");
                AssertFalse(root.GetProperty("result").GetProperty("isError").GetBoolean(), "Should not be error");

                var content = root.GetProperty("result").GetProperty("content").EnumerateArray().First();
                var text = content.GetProperty("text").GetString();
                AssertTrue(text?.Contains("AstTestProject") ?? false, "Should contain project namespace");
                AssertTrue(text?.Contains("totalFiles") ?? false, "Should contain file statistics");

                return true;
            }));

            // Test 47: AstCli - ast_file_context
            testResults.Add(await RunTestAsync("AstCli - file_context", async () =>
            {
                var serviceBPath = Path.Combine(astProjectDir, "ServiceB.cs");

                var request = new JsonRpcRequest
                {
                    Id = 47,
                    Method = "tools/call",
                    Params = new CallToolRequestParams
                    {
                        Name = "tool_execute",
                        Arguments = new
                        {
                            tool = "ast_file_context",
                            parameters = new
                            {
                                command = "file_context",
                                projectPath = astProjectDir,
                                filePath = serviceBPath
                            }
                        }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(15));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                AssertEqual(47, root.GetProperty("id").GetInt32(), "Response ID should match");
                AssertFalse(root.GetProperty("result").GetProperty("isError").GetBoolean(), "Should not be error");

                var content = root.GetProperty("result").GetProperty("content").EnumerateArray().First();
                var text = content.GetProperty("text").GetString();
                AssertTrue(text?.Contains("ServiceA") ?? false, "Should reference ServiceA");
                AssertTrue((text?.Contains("using") ?? false) || (text?.Contains("Using") ?? false), "Should contain using information");

                return true;
            }));

            // Test 48: AstCli - ast_diagnostics
            testResults.Add(await RunTestAsync("AstCli - diagnostics", async () =>
            {
                var errorProjectDir = CreateTestAstProject(Path.Combine(testBaseDir, "diagnostics_test"));
                var errorFilePath = Path.Combine(errorProjectDir, "Broken.cs");
                File.WriteAllText(errorFilePath, """
                    namespace Broken;
                    public class BrokenClass
                    {
                        public void Method(
                        {
                        }
                    }
                    """);

                var request = new JsonRpcRequest
                {
                    Id = 48,
                    Method = "tools/call",
                    Params = new CallToolRequestParams
                    {
                        Name = "tool_execute",
                        Arguments = new
                        {
                            tool = "ast_diagnostics",
                            parameters = new
                            {
                                command = "diagnostics",
                                projectPath = errorProjectDir,
                                filePath = errorFilePath
                            }
                        }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(15));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                AssertEqual(48, root.GetProperty("id").GetInt32(), "Response ID should match");
                AssertFalse(root.GetProperty("result").GetProperty("isError").GetBoolean(), "Should not be MCP error");

                var content = root.GetProperty("result").GetProperty("content").EnumerateArray().First();
                var text = content.GetProperty("text").GetString();
                AssertTrue((text?.Contains("error") ?? false) || (text?.Contains("Error") ?? false), "Should report syntax errors");

                return true;
            }));

            // Test 49: AstCli - ast_symbol_outline
            testResults.Add(await RunTestAsync("AstCli - symbol_outline", async () =>
            {
                var serviceAPath = Path.Combine(astProjectDir, "ServiceA.cs");

                var request = new JsonRpcRequest
                {
                    Id = 49,
                    Method = "tools/call",
                    Params = new CallToolRequestParams
                    {
                        Name = "tool_execute",
                        Arguments = new
                        {
                            tool = "ast_symbol_outline",
                            parameters = new
                            {
                                command = "symbol_outline",
                                filePath = serviceAPath
                            }
                        }
                    }
                };

                responseTcs = new TaskCompletionSource<string>();
                await SendRequestAsync(writer, request);
                var response = await WaitForResponseAsync(responseTcs, TimeSpan.FromSeconds(15));

                var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                AssertEqual(49, root.GetProperty("id").GetInt32(), "Response ID should match");
                AssertFalse(root.GetProperty("result").GetProperty("isError").GetBoolean(), "Should not be error");

                var content = root.GetProperty("result").GetProperty("content").EnumerateArray().First();
                var text = content.GetProperty("text").GetString();
                AssertTrue(text?.Contains("ServiceA") ?? false, "Should contain ServiceA class");
                AssertTrue(text?.Contains("Execute") ?? false, "Should contain Execute method");
                AssertTrue(text?.Contains("GetName") ?? false, "Should contain GetName method");

                return true;
            }));

        }
        finally
        {
            // 清理
            try
            {
                process.Kill(true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
            catch { }

            // 清理测试目录
            try
            {
                if (Directory.Exists(testBaseDir))
                {
                    Directory.Delete(testBaseDir, true);
                }
            }
            catch { }
        }

        // 输出测试结果摘要
        Console.WriteLine();
        Console.WriteLine("=== Test Results ===");
        foreach (var result in testResults)
        {
            var status = result.Passed ? "PASS" : "FAIL";
            var color = result.Passed ? ConsoleColor.Green : ConsoleColor.Red;
            Console.ForegroundColor = color;
            Console.WriteLine($"[{status}] {result.Name}");
            if (!result.Passed && result.Error != null)
            {
                Console.WriteLine($"  Error: {result.Error}");
            }
            Console.ResetColor();
        }

        var passedCount = testResults.Count(r => r.Passed);
        var totalCount = testResults.Count;
        Console.WriteLine();
        Console.WriteLine($"Total: {passedCount}/{totalCount} tests passed");

        Environment.Exit(passedCount == totalCount ? 0 : 1);
    }

    static void DeployCliToServer(string serverPath, string cliPath, string cliName)
    {
        var serverDir = Path.GetDirectoryName(serverPath);
        if (string.IsNullOrEmpty(serverDir)) return;

        var pluginsDir = Path.Combine(serverDir, "Plugins", cliName);
        if (!Directory.Exists(pluginsDir))
        {
            Directory.CreateDirectory(pluginsDir);
        }

        var cliDir = Path.GetDirectoryName(cliPath);
        if (string.IsNullOrEmpty(cliDir)) return;

        foreach (var file in Directory.GetFiles(cliDir, $"{cliName}.*"))
        {
            var fileName = Path.GetFileName(file);
            var destPath = Path.Combine(pluginsDir, fileName);
            if (!File.Exists(destPath) || File.GetLastWriteTimeUtc(file) > File.GetLastWriteTimeUtc(destPath))
            {
                File.Copy(file, destPath, overwrite: true);
            }
        }

        foreach (var file in Directory.GetFiles(cliDir, "*.dll"))
        {
            var destPath = Path.Combine(pluginsDir, Path.GetFileName(file));
            if (!File.Exists(destPath) || File.GetLastWriteTimeUtc(file) > File.GetLastWriteTimeUtc(destPath))
            {
                File.Copy(file, destPath, overwrite: true);
            }
        }

        foreach (var file in Directory.GetFiles(cliDir, "*.json"))
        {
            var destPath = Path.Combine(pluginsDir, Path.GetFileName(file));
            if (!File.Exists(destPath) || File.GetLastWriteTimeUtc(file) > File.GetLastWriteTimeUtc(destPath))
            {
                File.Copy(file, destPath, overwrite: true);
            }
        }

        Console.WriteLine($"Deployed {cliName} to: {pluginsDir}");
    }

    static void DeployAstCliToServer(string serverPath, string astCliPath)
    {
        var serverDir = Path.GetDirectoryName(serverPath);
        if (string.IsNullOrEmpty(serverDir)) return;

        var pluginsDir = Path.Combine(serverDir, "Plugins", "AstCli");
        if (!Directory.Exists(pluginsDir))
        {
            Directory.CreateDirectory(pluginsDir);
        }

        var destPath = Path.Combine(pluginsDir, "AstCli.exe");
        if (!File.Exists(destPath) || File.GetLastWriteTimeUtc(astCliPath) > File.GetLastWriteTimeUtc(destPath))
        {
            File.Copy(astCliPath, destPath, overwrite: true);
            Console.WriteLine($"Deployed AstCli to: {destPath}");
        }
    }

    static string? FindServerExecutable()
    {
        // 尝试多个可能的路径 - publish目录优先
        var possiblePaths = new[]
        {
            // 发布目录（优先）
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "publish", $"{ServerName}.exe")),
            // Debug 构建
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", ServerName, "bin", "Debug", "net10.0", $"{ServerName}.exe")),
            // Release 构建
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", ServerName, "bin", "Release", "net10.0", "win-x64", "publish", $"{ServerName}.exe")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", ServerName, "bin", "Release", "net10.0", $"{ServerName}.exe")),
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    static string? FindMemoryCliExecutable()
    {
        var possiblePaths = new[]
        {
            // Debug 构建（优先）
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Plugins", "MemoryCli", "bin", "Debug", "net10.0", "MemoryCli.exe")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "publish", "MemoryCli.exe")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Plugins", "MemoryCli", "bin", "Release", "net10.0", "win-x64", "publish", "MemoryCli.exe")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Plugins", "MemoryCli", "bin", "Release", "net10.0", "MemoryCli.exe")),
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    static string? FindFileReaderCliExecutable()
    {
        var possiblePaths = new[]
        {
            // Debug 构建（优先）
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Plugins", "FileReaderCli", "bin", "Debug", "net10.0", "FileReaderCli.exe")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "publish", "FileReaderCli.exe")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Plugins", "FileReaderCli", "bin", "Release", "net10.0", "win-x64", "publish", "FileReaderCli.exe")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Plugins", "FileReaderCli", "bin", "Release", "net10.0", "FileReaderCli.exe")),
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    static string? FindAstCliExecutable()
    {
        var possiblePaths = new[]
        {
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Plugins", "AstCli", "bin", "Release", "net10.0", "win-x64", "publish", "AstCli.exe")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Plugins", "AstCli", "bin", "Debug", "net10.0", "AstCli.exe")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "publish", "Plugins", "AstCli", "AstCli.exe")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Plugins", "AstCli", "bin", "Release", "net10.0", "AstCli.exe")),
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    static async Task SendRequestAsync(StreamWriter writer, JsonRpcRequest request)
    {
        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Console.WriteLine($"[CLIENT] {json}");
        await writer.WriteLineAsync(json);
    }

    public class JsonRpcRequest
    {
        [JsonPropertyName("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";

        [JsonPropertyName("id")]
        public object? Id { get; set; }

        [JsonPropertyName("method")]
        public string Method { get; set; } = string.Empty;

        [JsonPropertyName("params")]
        public object? Params { get; set; }
    }

    public class InitializeRequestParams
    {
        [JsonPropertyName("protocolVersion")]
        public string ProtocolVersion { get; set; } = "2024-11-05";

        [JsonPropertyName("capabilities")]
        public object Capabilities { get; set; } = new();

        [JsonPropertyName("clientInfo")]
        public Implementation ClientInfo { get; set; } = new();
    }

    public class Implementation
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;
    }

    public class CallToolRequestParams
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("arguments")]
        public object? Arguments { get; set; }
    }

    static async Task<string> WaitForResponseAsync(TaskCompletionSource<string> tcs, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        using var registration = cts.Token.Register(() => tcs.TrySetCanceled());

        await Task.Yield();
        return await tcs.Task.WaitAsync(timeout);
    }

    static async Task<TestResult> RunTestAsync(string name, Func<Task<bool>> test)
    {
        Console.WriteLine($"--- Running: {name} ---");
        try
        {
            var passed = await test();
            return new TestResult { Name = name, Passed = passed };
        }
        catch (Exception ex)
        {
            return new TestResult { Name = name, Passed = false, Error = ex.Message };
        }
    }

    static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new AssertionException($"{message}: Expected {expected}, got {actual}");
        }
    }

    static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new AssertionException($"{message}: Expected true, got false");
        }
    }

    static void AssertFalse(bool condition, string message)
    {
        if (condition)
        {
            throw new AssertionException($"{message}: Expected false, got true");
        }
    }

    static void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    static void CreateTestMemoryFiles(string baseDir, string testTimestamp)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        // 创建测试数据文件
        var mainFile = Path.Combine(baseDir, "memory.jsonl");
        var mainEntity = new TestEntity
        {
            Name = "MainEntity",
            EntityType = "Test",
            Observations = ["From main file"]
        };
        File.WriteAllText(mainFile, JsonSerializer.Serialize(mainEntity, options) + Environment.NewLine);

        var auxFile = Path.Combine(baseDir, "memory_aux.jsonl");
        var auxEntity = new TestEntity
        {
            Name = "AuxEntity",
            EntityType = "Test",
            Observations = ["From aux file"]
        };
        File.WriteAllText(auxFile, JsonSerializer.Serialize(auxEntity, options) + Environment.NewLine);

        var relationFile = Path.Combine(baseDir, "memory_relations.jsonl");
        var relation = new TestRelation
        {
            From = "MainEntity",
            To = "AuxEntity",
            RelationType = "connects_to"
        };
        File.WriteAllText(relationFile, JsonSerializer.Serialize(relation, options) + Environment.NewLine);
    }

    static string CreateTestAstProject(string baseDir)
    {
        var astProjectDir = Path.Combine(baseDir, "AstTestProject");
        Directory.CreateDirectory(astProjectDir);

        File.WriteAllText(Path.Combine(astProjectDir, "ServiceA.cs"), """
            namespace AstTestProject;

            public class ServiceA
            {
                public void Execute()
                {
                    Console.WriteLine("ServiceA executing");
                }

                public string GetName()
                {
                    return "ServiceA";
                }
            }
            """);

        File.WriteAllText(Path.Combine(astProjectDir, "ServiceB.cs"), """
            namespace AstTestProject;

            public class ServiceB
            {
                private ServiceA _serviceA = new();

                public void Run()
                {
                    _serviceA.Execute();
                    var name = _serviceA.GetName();
                }
            }
            """);

        File.WriteAllText(Path.Combine(astProjectDir, "Models.cs"), """
            namespace AstTestProject.Models;

            public interface IEntity
            {
                string Name { get; }
            }

            public class Entity : IEntity
            {
                public string Name { get; set; }
                public int Age { get; set; }
            }
            """);

        File.WriteAllText(Path.Combine(astProjectDir, "Program.cs"), """
            using AstTestProject;

            public class Program
            {
                public static void Main(string[] args)
                {
                    var service = new ServiceA();
                    service.Execute();
                }
            }
            """);

        File.WriteAllText(Path.Combine(astProjectDir, "AstTestProject.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="..\\Common\\Common.csproj" />
              </ItemGroup>
            </Project>
            """);

        return astProjectDir;
    }
}

class TestResult
{
    public string Name { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string? Error { get; set; }
}

class AssertionException : Exception
{
    public AssertionException(string message) : base(message) { }
}

/// <summary>
/// 测试实体模型
/// </summary>
class TestEntity
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("entityType")]
    public string EntityType { get; set; } = string.Empty;

    [JsonPropertyName("observations")]
    public List<string> Observations { get; set; } = [];
}

/// <summary>
/// 测试关系模型
/// </summary>
class TestRelation
{
    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;

    [JsonPropertyName("to")]
    public string To { get; set; } = string.Empty;

    [JsonPropertyName("relationType")]
    public string RelationType { get; set; } = string.Empty;
}
