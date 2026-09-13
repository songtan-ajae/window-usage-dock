using System;
using System.Diagnostics;
using System.Linq;

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
}
