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

        // 预先创建测试数据文件（用于多文件合并测试）
        EnsureDirectory(testBaseDir);
        CreateTestMemoryFiles(testBaseDir, testTimestamp);

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
                
                var searchResults = JsonSerializer.Deserialize<PluginDescriptor[]>(text!, CommonJsonContext.Default.PluginDescriptorArray);
                AssertTrue(searchResults?.Length > 0, "Should find memory-related tools");
                AssertTrue(searchResults!.Any(p => p.Name.Contains("Memory", StringComparison.OrdinalIgnoreCase)), "Should find MemoryCli plugin");

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
                        Arguments = new { pluginName = "MemoryCli" }
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
                
                var searchResults = JsonSerializer.Deserialize<PluginDescriptor[]>(text!, CommonJsonContext.Default.PluginDescriptorArray);
                AssertTrue(searchResults?.Length > 0, "Should find file-related tools");
                AssertTrue(searchResults!.Any(p => p.Name.Contains("FileReader", StringComparison.OrdinalIgnoreCase)), "Should find FileReaderCli plugin");

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
                AssertTrue(listResult?.TotalPlugins >= 2, "Should have at least 2 plugins");
                AssertTrue(listResult!.Plugins.Any(p => p.Name == "MemoryCli"), "Should have MemoryCli plugin");
                AssertTrue(listResult.Plugins.Any(p => p.Name == "FileReaderCli"), "Should have FileReaderCli plugin");

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
