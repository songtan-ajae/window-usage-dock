using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Codenotch.Core.Interfaces;
using Codenotch.Core.Models;

namespace Codenotch.Core.Providers;

public class OpenCodeProvider : IUsageProvider
{
    public string Id => "opencode";
    public string DisplayName => "OpenCode";
    public string BrandColor => "#10B981"; // Emerald Green
    public string Glyph => "OC";
    public int Priority => 7;

    private string GetAuthPath()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userProfile, ".local", "share", "opencode", "auth.json");
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(File.Exists(GetAuthPath()));
    }

    public Task<UsageRecord> FetchUsageAsync(CancellationToken cancellationToken = default)
    {
        var record = new UsageRecord
        {
            ProviderId = Id,
            DisplayName = DisplayName,
            BrandColor = BrandColor,
            Glyph = Glyph,
            PlanName = "Go Plan",
            IsConnected = File.Exists(GetAuthPath()),
            PrimaryUsedPercentage = 0,
            PrimaryStatusText = File.Exists(GetAuthPath()) ? "Connected" : "Not connected",
            ConnectionHint = "Run `opencode auth login` to connect Go plan."
        };
        return Task.FromResult(record);
    }
}

public class CommandCodeProvider : IUsageProvider
{
    public string Id => "commandcode";
    public string DisplayName => "Command Code";
    public string BrandColor => "#8B5CF6"; // Purple
    public string Glyph => "CD";
    public int Priority => 8;

    private string GetAuthPath()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userProfile, ".commandcode", "auth.json");
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(File.Exists(GetAuthPath()) || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("COMMAND_CODE_API_KEY")));
    }

    public Task<UsageRecord> FetchUsageAsync(CancellationToken cancellationToken = default)
    {
        var record = new UsageRecord
        {
            ProviderId = Id,
            DisplayName = DisplayName,
            BrandColor = BrandColor,
            Glyph = Glyph,
            PlanName = "Pro",
            IsConnected = File.Exists(GetAuthPath()),
            PrimaryUsedPercentage = 0,
            PrimaryStatusText = "Sign in to Command Code",
            ConnectionHint = "Install Command Code and sign in."
        };
        return Task.FromResult(record);
    }
}

public class GlmProvider : IUsageProvider
{
    public string Id => "glm";
    public string DisplayName => "GLM";
    public string BrandColor => "#EC4899"; // Pink
    public string Glyph => "GL";
    public int Priority => 9;

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public Task<UsageRecord> FetchUsageAsync(CancellationToken cancellationToken = default)
    {
        var record = new UsageRecord
        {
            ProviderId = Id,
            DisplayName = DisplayName,
            BrandColor = BrandColor,
            Glyph = Glyph,
            PlanName = "Coding Plan",
            IsConnected = false,
            PrimaryUsedPercentage = 0,
            PrimaryStatusText = "Set up GLM Key",
            ConnectionHint = "Configure GLM Key in settings."
        };
        return Task.FromResult(record);
    }
}
