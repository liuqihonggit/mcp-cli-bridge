namespace AstCli.E2E;

using System.Diagnostics;
using System.Text.Json;

public sealed class AstCliRunner
{
    private readonly string _astCliPath;

    public AstCliRunner(string astCliPath)
    {
        _astCliPath = astCliPath;
    }

    public AstCliResult Execute(Dictionary<string, object> parameters)
    {
        var json = JsonSerializer.Serialize(parameters);
        var b64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));

        using var process = new Process();
        process.StartInfo.FileName = _astCliPath;
        process.StartInfo.Arguments = $"--json-input {b64}";
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;

        process.Start();
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new AstCliResult(process.ExitCode, stdout, stderr);
    }

    public static string FindAstCliExecutable()
    {
        var publishDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "publish", "Plugins", "AstCli"));

        var exePath = Path.Combine(publishDir, "AstCli.exe");
        if (File.Exists(exePath))
            return exePath;

        throw new FileNotFoundException($"AstCli.exe not found at: {exePath}. Run build.ps1 first.");
    }
}

public sealed class AstCliResult
{
    public int ExitCode { get; }
    public string Stdout { get; }
    public string Stderr { get; }

    public AstCliResult(int exitCode, string stdout, string stderr)
    {
        ExitCode = exitCode;
        Stdout = stdout;
        Stderr = stderr;
    }

    public JsonElement ParseJson()
    {
        var root = JsonDocument.Parse(Stdout).RootElement;
        if (root.TryGetProperty("data", out var data))
            return data;
        return root;
    }

    public bool IsSuccess => ExitCode == 0;

    public override string ToString() => $"ExitCode={ExitCode}, Stdout={Stdout}, Stderr={Stderr}";
}
