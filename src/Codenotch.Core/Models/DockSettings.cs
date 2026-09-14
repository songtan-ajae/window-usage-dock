using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Codenotch.Core.Models;

public sealed class DockSettings
{
    public List<string> SelectedProviderIds { get; set; } = new() { "codex", "antigravity", "claude" };
}

public sealed class UsageProviderOption
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Glyph { get; init; } = string.Empty;
}

public static class DockSettingsStore
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WindowsUsageDock",
        "settings.json");

    public static DockSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var settings = JsonSerializer.Deserialize<DockSettings>(File.ReadAllText(SettingsPath));
                if (settings?.SelectedProviderIds?.Any() == true)
                    return settings;
            }
        }
        catch
        {
            // Keep the dock usable if a manually edited settings file is invalid.
        }

        return new DockSettings();
    }

    public static void Save(IEnumerable<string> selectedProviderIds)
    {
        var directory = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(directory);
        var settings = new DockSettings
        {
            SelectedProviderIds = selectedProviderIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        };
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }
}
