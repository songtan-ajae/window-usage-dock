using System;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Management;

namespace Codenotch.Core.Services;

public static class AgentProcessMonitor
{
    public static bool IsRunning(params string[] processNames)
    {
        foreach (var name in processNames)
        {
            var processes = Process.GetProcessesByName(name);
            try
            {
                if (processes.Length > 0) return true;
            }
            finally
            {
                foreach (var process in processes) process.Dispose();
            }
        }

        return false;
    }

    public static bool IsClaudeCodeRunning()
    {
        var processes = Process.GetProcessesByName("claude");
        try
        {
            foreach (var process in processes)
            {
                try
                {
                    // Claude Desktop runs from WindowsApps. It doesn't expose
                    // Claude Code quota data, so it must not create a dock ring.
                    var executablePath = process.MainModule?.FileName ?? string.Empty;
                    if (!executablePath.Contains("\\WindowsApps\\Claude_", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                catch
                {
                    // Access to another process can be denied; don't treat it as CLI.
                }
            }
        }
        finally
        {
            foreach (var process in processes) process.Dispose();
        }

        return false;
    }

    /// <summary>
    /// The Windows Codex launcher can host the CLI in another executable, so an
    /// exact process-name check alone is not reliable. A recently written
    /// session transcript is the local, privacy-preserving activity signal.
    /// </summary>
    public static bool IsCodexCliRunning()
    {
        if (IsRunning("codex", "codex-cli")) return true;

        var codexHome = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
        return HasRecentWrite(Path.Combine(codexHome, "history.jsonl"), TimeSpan.FromMinutes(20))
            || HasRecentWrite(Path.Combine(codexHome, "sessions", DateTime.Now.ToString("yyyy"), DateTime.Now.ToString("MM"), DateTime.Now.ToString("dd")), TimeSpan.FromMinutes(20));
    }

    /// <summary>
    /// A selected provider belongs in the dock only while its app or CLI is
    /// running. Persisted session directories and recent logs are not proof of
    /// a live process after the app has closed.
    /// </summary>
    public static bool IsAntigravityOrGeminiRunning()
    {
        return IsRunning("antigravity", "agy", "gemini", "gemini-cli", "language_server")
            || IsHostedCliRunning();
    }

    private static bool IsHostedCliRunning()
    {
        if (!OperatingSystem.IsWindows()) return false;

        try
        {
            // Read command lines locally only to classify generic Node hosts;
            // never retain or emit their arguments.
            using var searcher = new ManagementObjectSearcher(
                "SELECT CommandLine FROM Win32_Process WHERE Name = 'node.exe' OR Name = 'nodejs.exe'");
            using var processes = searcher.Get();
            foreach (ManagementObject process in processes)
            {
                using (process)
                {
                    if (IsHostedAntigravityCliCommandLine(process["CommandLine"] as string))
                        return true;
                }
            }
        }
        catch
        {
            // WMI may be unavailable or access to a process may be denied.
        }

        return false;
    }

    internal static bool IsHostedAntigravityCliCommandLine(string? commandLine)
    {
        return commandLine?.Contains("antigravity-cli", StringComparison.OrdinalIgnoreCase) == true
            || commandLine?.Contains("gemini-cli", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool HasRecentWrite(string path, TimeSpan maxAge)
    {
        try
        {
            DateTime lastWrite;
            if (File.Exists(path))
                lastWrite = File.GetLastWriteTime(path);
            else if (Directory.Exists(path))
                lastWrite = Directory.GetLastWriteTime(path);
            else
                return false;

            return DateTime.Now - lastWrite <= maxAge;
        }
        catch
        {
            return false;
        }
    }
}
