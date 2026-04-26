using Common.Tools;

namespace MemoryCli.Services;

internal sealed class RulesInstallerService
{
    private const string RulesFileName = "MyMemoryRules.md";

    public async Task InstallDefaultRulesAsync(CancellationToken cancellationToken = default)
    {
        var rulesPath = GetRulesPath();

        // 如果找不到项目根目录，不创建规则文件
        if (string.IsNullOrEmpty(rulesPath))
        {
            return;
        }

        if (File.Exists(rulesPath))
        {
            return;
        }

        await FileOperationHelper.WriteTextAsync(rulesPath, GetDefaultRulesContent(), cancellationToken);
    }

    private static string? GetRulesPath()
    {
        var baseDir = AppContext.BaseDirectory;
        var projectRoot = FindProjectRoot(baseDir);
        
        // 只有在找到真正的项目根目录时才创建规则文件
        if (string.IsNullOrEmpty(projectRoot))
        {
            return null;
        }
        
        return Path.Combine(projectRoot, ".trae", "rules", RulesFileName);
    }

    private static string? FindProjectRoot(string startDir)
    {
        var currentDir = new DirectoryInfo(startDir);
        
        // 向上查找，直到找到包含 .git 或解决方案文件的目录
        while (currentDir != null)
        {
            // 真正的项目根目录应该有 .git 或解决方案文件(.sln/.slnx)
            var hasGit = Directory.Exists(Path.Combine(currentDir.FullName, ".git"));
            var hasSln = Directory.GetFiles(currentDir.FullName, "*.sln").Length > 0;
            var hasSlnx = Directory.GetFiles(currentDir.FullName, "*.slnx").Length > 0;
            
            if (hasGit || hasSln || hasSlnx)
            {
                return currentDir.FullName;
            }
            
            currentDir = currentDir.Parent;
        }
        
        return null;
    }

    private static string GetDefaultRulesContent()
    {
        return """
# MyMemoryRules

## 经验复用

### 查记忆（先查后做）
- 搜同类问题、失败记录、解决方案
- 知识图谱：技术栈 → 问题 → 方案

### 写记忆（解决后记录）
- 问题场景、原因、方案、验证结果
- 标记：【成功经验】/【避坑指南】
""";
    }
}
