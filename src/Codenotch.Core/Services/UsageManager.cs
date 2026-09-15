using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Codenotch.Core.Interfaces;
using Codenotch.Core.Models;
using Codenotch.Core.Providers;

namespace Codenotch.Core.Services;

public class UsageManager : IDisposable
{
    public const int MaximumSelectedProviders = 5;
    private readonly List<IUsageProvider> _providers = new();
    private readonly HashSet<string> _selectedProviderIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _selectionGate = new();
    private readonly bool _persistSettings;
    private readonly Timer _pollTimer;
    private readonly HashSet<string> _notifiedCrossings = new();
    private int _isRefreshing;

    public event Action<IReadOnlyList<UsageRecord>>? UsageUpdated;
    public event Action<string, string>? ThresholdAlertTriggered; // providerName, message

    public IReadOnlyList<UsageRecord> CurrentRecords { get; private set; } = Array.Empty<UsageRecord>();

    public IReadOnlyList<UsageProviderOption> AvailableProviders => _providers
        .Select(provider => new UsageProviderOption
        {
            Id = provider.Id,
            DisplayName = provider.DisplayName,
            Glyph = provider.Glyph
        })
        .OrderBy(provider => GetProviderPriority(provider.Id))
        .ToList();

    public IReadOnlyList<string> SelectedProviderIds
    {
        get
        {
            lock (_selectionGate)
                return _selectedProviderIds.ToList();
        }
    }

    public UsageManager()
        : this(new IUsageProvider[]
        {
            new CodexProvider(),
            new AntigravityProvider(),
            new ClaudeProvider()
        })
    {
    }

    public UsageManager(IEnumerable<IUsageProvider> providers, bool startPolling = true, bool persistSettings = true)
    {
        _providers.AddRange(providers);
        _persistSettings = persistSettings;
        InitializeSelectedProviders(persistSettings ? DockSettingsStore.Load().SelectedProviderIds : Array.Empty<string>());

        _pollTimer = new Timer(
            async _ => await RefreshAllAsync(),
            null,
            startPolling ? TimeSpan.FromMilliseconds(300) : Timeout.InfiniteTimeSpan,
            startPolling ? TimeSpan.FromSeconds(15) : Timeout.InfiniteTimeSpan);
    }

    public async Task RefreshAllAsync()
    {
        if (Interlocked.Exchange(ref _isRefreshing, 1) != 0) return;

        try
        {
            var selectedProviderIds = SelectedProviderIds;
            var activeProviders = await Task.WhenAll(_providers
                .Where(provider => selectedProviderIds.Contains(provider.Id, StringComparer.OrdinalIgnoreCase))
                .Select(async provider => new
            {
                Provider = provider,
                IsRunning = await provider.IsAgentRunningAsync()
            }));

            var tasks = activeProviders
                .Where(candidate => candidate.IsRunning)
                .Select(candidate => candidate.Provider.FetchUsageAsync())
                .ToList();
            var results = await Task.WhenAll(tasks);

            // 항상 지정된 순서대로 정렬 (코덱스 -> 안티그래비티 -> 클로드)
            var sorted = results
                .OrderBy(r => GetProviderPriority(r.ProviderId))
                .ToList();

            CurrentRecords = sorted;
            UsageUpdated?.Invoke(sorted);

            // 80%, 100% 임계치 도달 알림 검사
            foreach (var rec in sorted.Where(r => r.IsConnected))
            {
                CheckThresholds(rec);
            }
        }
        catch { }
        finally
        {
            Volatile.Write(ref _isRefreshing, 0);
        }
    }

    public void SetSelectedProviderIds(IEnumerable<string> providerIds)
    {
        var selected = NormalizeProviderIds(providerIds);
        if (selected.Count == 0)
            throw new ArgumentException("At least one provider must be selected.", nameof(providerIds));

        lock (_selectionGate)
        {
            _selectedProviderIds.Clear();
            foreach (var providerId in selected)
                _selectedProviderIds.Add(providerId);
        }

        if (_persistSettings)
            DockSettingsStore.Save(selected);
    }

    private void InitializeSelectedProviders(IEnumerable<string> providerIds)
    {
        var selected = NormalizeProviderIds(providerIds);
        if (selected.Count == 0)
            selected = _providers.Take(MaximumSelectedProviders).Select(provider => provider.Id).ToList();

        foreach (var providerId in selected)
            _selectedProviderIds.Add(providerId);
    }

    private List<string> NormalizeProviderIds(IEnumerable<string> providerIds)
    {
        var availableIds = new HashSet<string>(_providers.Select(provider => provider.Id), StringComparer.OrdinalIgnoreCase);
        return providerIds
            .Where(providerId => !string.IsNullOrWhiteSpace(providerId) && availableIds.Contains(providerId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaximumSelectedProviders)
            .ToList();
    }

    private void CheckThresholds(UsageRecord record)
    {
        var key80 = $"{record.ProviderId}_80";
        var key100 = $"{record.ProviderId}_100";

        if (record.PrimaryUsedPercentage >= 100.0)
        {
            if (!_notifiedCrossings.Contains(key100))
            {
                _notifiedCrossings.Add(key100);
                ThresholdAlertTriggered?.Invoke(record.DisplayName, $"{record.DisplayName}의 할당량 한도에 도달했습니다! (100% 사용)");
            }
        }
        else if (record.PrimaryUsedPercentage >= 80.0)
        {
            if (!_notifiedCrossings.Contains(key80))
            {
                _notifiedCrossings.Add(key80);
                ThresholdAlertTriggered?.Invoke(record.DisplayName, $"{record.DisplayName} 사용량이 80%를 초과했습니다 ({record.PrimaryUsedPercentage:F0}% 사용됨)");
            }
        }
    }

    private int GetProviderPriority(string id)
    {
        return id switch
        {
            "codex" => 1,
            "antigravity" => 2,
            "claude" => 3,
            _ => 99
        };
    }

    public void Dispose()
    {
        _pollTimer.Dispose();
    }
}
