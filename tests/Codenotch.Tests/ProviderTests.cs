using System.Threading.Tasks;
using Codenotch.Core.Providers;
using Codenotch.Core.Services;
using Xunit;

namespace Codenotch.Tests;

public class ProviderTests
{
    [Fact]
    public async Task AntigravityProvider_FetchesUsageSuccessfully()
    {
        var provider = new AntigravityProvider();
        var usage = await provider.FetchUsageAsync();

        Assert.NotNull(usage);
        Assert.Equal("antigravity", usage.ProviderId);
        Assert.True(usage.PrimaryUsedPercentage >= 0);
        System.Console.WriteLine($"ANTIGRAVITY USAGE: 5h={usage.PrimaryRemainingPercentage}% 남음 ({usage.ResetTimeText}), SubWindows count={usage.SubWindows.Count}");
        foreach (var sw in usage.SubWindows)
        {
            System.Console.WriteLine($"  SubWindow: {sw.Label} -> {sw.RemainingPercentage}% 남음 ({sw.ResetsInText})");
        }
    }

    [Fact]
    public async Task CodexProvider_HandlesLocalState()
    {
        var provider = new CodexProvider();
        var usage = await provider.FetchUsageAsync();

        Assert.NotNull(usage);
        Assert.Equal("codex", usage.ProviderId);
        System.Console.WriteLine($"CODEX USAGE: isConnected={usage.IsConnected}, used={usage.PrimaryUsedPercentage}, status={usage.PrimaryStatusText}, subwindows={usage.SubWindows.Count}");
    }

    [Fact]
    public async Task UsageManager_CollectsAllProviders()
    {
        using var manager = new UsageManager();
        await manager.RefreshAllAsync();

        Assert.NotEmpty(manager.CurrentRecords);
        Assert.Contains(manager.CurrentRecords, r => r.ProviderId == "antigravity");
        Assert.Contains(manager.CurrentRecords, r => r.ProviderId == "codex");
    }
}
