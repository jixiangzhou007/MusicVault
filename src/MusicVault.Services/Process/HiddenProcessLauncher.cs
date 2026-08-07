using System.Diagnostics;

namespace MusicVault.Services.Process;

/// <summary>启动控制台子进程时不弹出 CMD 窗口（Windows）。</summary>
public static class HiddenProcessLauncher
{
    public static System.Diagnostics.Process? Start(
        string fileName,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string>? environment = null) =>
        StartWithArguments(fileName, null, workingDirectory, environment);

    public static System.Diagnostics.Process? StartWithArguments(
        string fileName,
        string? arguments,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments ?? string.Empty,
                WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(fileName) ?? Environment.CurrentDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true
            };

            if (environment != null)
            {
                foreach (var (key, value) in environment)
                    psi.Environment[key] = value;
            }

            var process = System.Diagnostics.Process.Start(psi);
            if (process == null) return null;

            process.OutputDataReceived += static (_, _) => { };
            process.ErrorDataReceived += static (_, _) => { };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            return process;
        }
        catch
        {
            return null;
        }
    }
}
