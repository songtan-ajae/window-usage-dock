using System.Threading.Tasks;
using System.Threading;
using Codenotch.Core.Interfaces;
using Codenotch.Core.Models;
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
        if (usage.IsConnected)
        {
            Assert.NotEmpty(usage.SubWindows);
            Assert.Equal("5시간 모델 한도", usage.SubWindows[0].Label);
            Assert.Equal(usage.PrimaryUsedPercentage, usage.SubWindows[0].UsedPercentage);
        }
        else
        {
            Assert.Empty(usage.SubWindows);
            Assert.Contains("불러오지 못", usage.PrimaryStatusText);
        }
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
        Assert.NotEmpty(usage.SubWindows);
        Assert.Equal("5시간 사용 제한", usage.SubWindows[0].Label);
        Assert.Equal(usage.PrimaryUsedPercentage, usage.SubWindows[0].UsedPercentage);
        System.Console.WriteLine($"CODEX USAGE: isConnected={usage.IsConnected}, used={usage.PrimaryUsedPercentage}, status={usage.PrimaryStatusText}, subwindows={usage.SubWindows.Count}");
    }

    [Fact]
    public async Task UsageManager_CollectsOnlyRunningProviders()
    {
        var running = new TestProvider("running", isRunning: true);
        var stopped = new TestProvider("stopped", isRunning: false);
        using var manager = new UsageManager(new IUsageProvider[] { running, stopped }, startPolling: false);
        await manager.RefreshAllAsync();

        var record = Assert.Single(manager.CurrentRecords);
        Assert.Equal("running", record.ProviderId);
        Assert.True(running.FetchCalled);
        Assert.False(stopped.FetchCalled);
    }

    [Fact]
    public async Task UsageManager_ShowsOnlySelectedProviders_AndLimitsSelectionToFive()
    {
        var providers = Enumerable.Range(1, 6)
            .Select(index => (IUsageProvider)new TestProvider($"provider-{index}", isRunning: true))
            .ToArray();
        using var manager = new UsageManager(providers, startPolling: false, persistSettings: false);

        manager.SetSelectedProviderIds(providers.Select(provider => provider.Id));
        await manager.RefreshAllAsync();

        Assert.Equal(UsageManager.MaximumSelectedProviders, manager.SelectedProviderIds.Count);
        Assert.Equal(UsageManager.MaximumSelectedProviders, manager.CurrentRecords.Count);
        Assert.DoesNotContain(manager.CurrentRecords, record => record.ProviderId == "provider-6");
    }

    [Fact]
    public void UsageManager_DefaultConfiguration_OffersFiveAgents()
    {
        using var manager = new UsageManager();

        Assert.Equal(5, manager.AvailableProviders.Count);
        Assert.Equal(
            new[] { "codex", "antigravity", "claude", "copilot", "cursor" },
            manager.AvailableProviders.Select(provider => provider.Id));
        Assert.Equal(5, new DockSettings().SelectedProviderIds.Count);
    }

    private sealed class TestProvider : IUsageProvider
    {
        private readonly bool _isRunning;

        public TestProvider(string id, bool isRunning)
        {
            Id = id;
            _isRunning = isRunning;
        }

        public string Id { get; }
        public string DisplayName => Id;
        public string BrandColor => "#000000";
        public string Glyph => "T";
        public int Priority => 99;
        public bool FetchCalled { get; private set; }

        public Task<bool> IsAgentRunningAsync(CancellationToken cancellationToken = default) => Task.FromResult(_isRunning);
        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<UsageRecord> FetchUsageAsync(CancellationToken cancellationToken = default)
        {
            FetchCalled = true;
            return Task.FromResult(new UsageRecord { ProviderId = Id, DisplayName = DisplayName });
        }
    }
}
