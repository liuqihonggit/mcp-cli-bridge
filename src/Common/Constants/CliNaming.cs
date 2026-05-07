using System.Text;

namespace Common.Constants;

/// <summary>
/// CLI 名称规范化工具 - 将 PascalCase 转换为 snake_case
/// 统一命名约定：所有 CLI 插件名、命令名使用小写下划线格式
/// </summary>
public static class CliNaming
{
    /// <summary>
    /// 将 PascalCase 名称转换为 snake_case（小写下划线格式）
    /// 例：MemoryCli → memory_cli, FileReaderCli → file_reader_cli
    /// </summary>
    public static string ToSnakeCase(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var sb = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                {
                    sb.Append('_');
                }
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// 从 .exe 文件名提取并规范化为 CLI 插件名
    /// 例：MemoryCli.exe → memory_cli, AstCli.exe → ast_cli
    /// </summary>
    public static string NormalizePluginName(string exeFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exeFileName);

        var name = exeFileName;
        var dotIndex = name.IndexOf('.');
        if (dotIndex > 0)
        {
            name = name[..dotIndex];
        }

        return ToSnakeCase(name);
    }
}
