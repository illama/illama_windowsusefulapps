using System.Diagnostics;

namespace SystemManagerPro.Services;

/// <summary>Exécute des commandes externes (powershell.exe, ipconfig, netsh...) et capture leur sortie.</summary>
public static class ProcessRunner
{
    public record RunResult(int ExitCode, string StdOut, string StdErr);

    public static RunResult Run(string fileName, string arguments, int timeoutMs = 30000)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Impossible de lancer {fileName}");
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(timeoutMs))
        {
            try { process.Kill(true); } catch { /* ignore */ }
        }
        return new RunResult(process.ExitCode, stdout, stderr);
    }

    public static RunResult RunPowerShell(string script, int timeoutMs = 30000)
    {
        string encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
        return Run("powershell.exe", $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}", timeoutMs);
    }
}
