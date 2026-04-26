namespace Common.Tools;

/// <summary>
/// 工具示例工厂 - 特性驱动版本
/// 提供获取工具示例的方法，支持从特性扫描获取
/// </summary>
public static class ToolExampleFactory
{
    /// <summary>
    /// 创建实体工具的示例
    /// </summary>
    public static IReadOnlyList<ToolExample> CreateEntitiesExamples()
    {
        return
        [
            new()
            {
                Title = "创建单个实体",
                Description = "创建一个名为 'John Doe' 的人员实体",
                JsonRequest = """
                {
                  "command": "create_entities",
                  "entities": [
                    {
                      "name": "John Doe",
                      "entityType": "person",
                      "observations": ["Software engineer with 10 years of experience"]
                    }
                  ]
                }
                """
            },
            new()
            {
                Title = "创建多个相关实体",
                Description = "同时创建项目和团队成员实体",
                JsonRequest = """
                {
                  "command": "create_entities",
                  "entities": [
                    {
                      "name": "Project Alpha",
                      "entityType": "project",
                      "observations": ["AI-powered knowledge management system"]
                    },
                    {
                      "name": "Development Team",
                      "entityType": "team",
                      "observations": ["Responsible for Project Alpha development"]
                    }
                  ]
                }
                """
            }
        ];
    }

    /// <summary>
    /// 创建关系工具的示例
    /// </summary>
    public static IReadOnlyList<ToolExample> CreateRelationsExamples()
    {
        return
        [
            new()
            {
                Title = "创建简单关系",
                Description = "建立人员与项目之间的工作关系",
                JsonRequest = """
                {
                  "command": "create_relations",
                  "relations": [
                    {
                      "from": "John Doe",
                      "to": "Project Alpha",
                      "relationType": "works_on"
                    }
                  ]
                }
                """
            },
            new()
            {
                Title = "创建多个关系",
                Description = "建立团队成员与项目的多种关系",
                JsonRequest = """
                {
                  "command": "create_relations",
                  "relations": [
                    {
                      "from": "Development Team",
                      "to": "Project Alpha",
                      "relationType": "manages"
                    },
                    {
                      "from": "Project Alpha",
                      "to": "Development Team",
                      "relationType": "managed_by"
                    }
                  ]
                }
                """
            }
        ];
    }

    /// <summary>
    /// 创建读取图谱工具的示例
    /// </summary>
    public static IReadOnlyList<ToolExample> ReadGraphExamples()
    {
        return
        [
            new()
            {
                Title = "读取完整图谱",
                Description = "获取所有实体和关系的完整视图",
                JsonRequest = """
                {
                  "command": "read_graph"
                }
                """
            }
        ];
    }

    /// <summary>
    /// 创建搜索节点工具的示例
    /// </summary>
    public static IReadOnlyList<ToolExample> SearchNodesExamples()
    {
        return
        [
            new()
            {
                Title = "按名称搜索",
                Description = "搜索包含特定关键词的实体",
                JsonRequest = """
                {
                  "command": "search_nodes",
                  "query": "Project Alpha"
                }
                """
            },
            new()
            {
                Title = "按类型搜索",
                Description = "搜索特定类型的所有实体",
                JsonRequest = """
                {
                  "command": "search_nodes",
                  "query": "person"
                }
                """
            }
        ];
    }

    /// <summary>
    /// 创建添加观察工具的示例
    /// </summary>
    public static IReadOnlyList<ToolExample> AddObservationsExamples()
    {
        return
        [
            new()
            {
                Title = "添加单个观察",
                Description = "为现有实体添加一条新信息",
                JsonRequest = """
                {
                  "command": "add_observations",
                  "name": "John Doe",
                  "observations": ["Promoted to Senior Engineer in 2024"]
                }
                """
            },
            new()
            {
                Title = "添加多个观察",
                Description = "为实体批量添加多条信息",
                JsonRequest = """
                {
                  "command": "add_observations",
                  "name": "Project Alpha",
                  "observations": [
                    "Launched in Q1 2024",
                    "Uses .NET 10 and AOT compilation",
                    "Team size: 5 developers"
                  ]
                }
                """
            }
        ];
    }

    /// <summary>
    /// 创建删除实体工具的示例
    /// </summary>
    public static IReadOnlyList<ToolExample> DeleteEntitiesExamples()
    {
        return
        [
            new()
            {
                Title = "删除单个实体",
                Description = "删除一个实体及其相关关系",
                JsonRequest = """
                {
                  "command": "delete_entities",
                  "names": ["Old Project Name"]
                }
                """
            },
            new()
            {
                Title = "批量删除实体",
                Description = "删除多个不再需要的实体",
                JsonRequest = """
                {
                  "command": "delete_entities",
                  "names": ["Temp Entity 1", "Temp Entity 2", "Test Data"]
                }
                """
            }
        ];
    }

    /// <summary>
    /// 创建打开节点工具的示例
    /// </summary>
    public static IReadOnlyList<ToolExample> OpenNodesExamples()
    {
        return
        [
            new()
            {
                Title = "获取单个节点",
                Description = "获取特定实体的详细信息和相关关系",
                JsonRequest = """
                {
                  "command": "open_nodes",
                  "names": ["John Doe"]
                }
                """
            },
            new()
            {
                Title = "获取多个节点",
                Description = "批量获取多个实体的信息",
                JsonRequest = """
                {
                  "command": "open_nodes",
                  "names": ["John Doe", "Project Alpha", "Development Team"]
                }
                """
            }
        ];
    }

    /// <summary>
    /// 创建读取文件头部工具的示例
    /// </summary>
    public static IReadOnlyList<ToolExample> ReadHeadExamples()
    {
        return
        [
            new()
            {
                Title = "读取文件前10行",
                Description = "读取文件开头的内容",
                JsonRequest = """
                {
                  "command": "read_head",
                  "filePath": "C:\\Users\\Example\\document.txt"
                }
                """
            },
            new()
            {
                Title = "读取文件前50行",
                Description = "读取文件开头指定行数的内容",
                JsonRequest = """
                {
                  "command": "read_head",
                  "filePath": "C:\\Users\\Example\\document.txt",
                  "lineCount": 50
                }
                """
            }
        ];
    }

    /// <summary>
    /// 创建读取文件尾部工具的示例
    /// </summary>
    public static IReadOnlyList<ToolExample> ReadTailExamples()
    {
        return
        [
            new()
            {
                Title = "读取文件后10行",
                Description = "读取文件结尾的内容",
                JsonRequest = """
                {
                  "command": "read_tail",
                  "filePath": "C:\\Users\\Example\\document.txt"
                }
                """
            },
            new()
            {
                Title = "读取文件后50行",
                Description = "读取文件结尾指定行数的内容",
                JsonRequest = """
                {
                  "command": "read_tail",
                  "filePath": "C:\\Users\\Example\\document.txt",
                  "lineCount": 50
                }
                """
            }
        ];
    }

    /// <summary>
    /// 根据命令名称获取示例列表
    /// </summary>
    /// <param name="commandName">命令名称</param>
    /// <returns>示例列表</returns>
    public static IReadOnlyList<ToolExample> GetExamplesByCommandName(string commandName)
    {
        ArgumentException.ThrowIfNullOrEmpty(commandName);

        return commandName.ToLowerInvariant() switch
        {
            "create_entities" => CreateEntitiesExamples(),
            "create_relations" => CreateRelationsExamples(),
            "read_graph" => ReadGraphExamples(),
            "search_nodes" => SearchNodesExamples(),
            "add_observations" => AddObservationsExamples(),
            "delete_entities" => DeleteEntitiesExamples(),
            "open_nodes" => OpenNodesExamples(),
            "read_head" => ReadHeadExamples(),
            "read_tail" => ReadTailExamples(),
            _ => []
        };
    }
}
