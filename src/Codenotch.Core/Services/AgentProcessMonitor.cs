using System;
using System.Diagnostics;
using System.Linq;
using System.IO;

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
    /// Antigravity Desktop and Gemini CLI can expose their active quota service
    /// through different host processes. The local service/log heartbeat is a
    /// reliable fallback without inspecting process command lines or contents.
    /// </summary>
    public static bool IsAntigravityOrGeminiRunning()
    {
        if (IsRunning("antigravity", "gemini", "gemini-cli", "language_server")) return true;

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var antigravityLogs = Path.Combine(appData, "Antigravity", "logs");
        var geminiHome = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gemini");

        return HasRecentWrite(Path.Combine(antigravityLogs, "main.log"), TimeSpan.FromMinutes(20))
            || HasRecentWrite(Path.Combine(antigravityLogs, "language_server.log"), TimeSpan.FromMinutes(20))
            || HasRecentWrite(Path.Combine(geminiHome, "history.json"), TimeSpan.FromMinutes(20));
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
