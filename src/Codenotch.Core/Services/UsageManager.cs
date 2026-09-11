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
    private readonly List<IUsageProvider> _providers = new();
    private readonly Timer _pollTimer;
    private readonly HashSet<string> _notifiedCrossings = new();
    private bool _isRefreshing = false;

    public event Action<IReadOnlyList<UsageRecord>>? UsageUpdated;
    public event Action<string, string>? ThresholdAlertTriggered; // providerName, message

    public IReadOnlyList<UsageRecord> CurrentRecords { get; private set; } = Array.Empty<UsageRecord>();

    public UsageManager()
    {
        // 사용자가 요청한 3대 핵심 AI 어시스턴트 순서: 코덱스 -> 안티그래비티 -> 클로드
        _providers.Add(new CodexProvider());
        _providers.Add(new AntigravityProvider());
        _providers.Add(new ClaudeProvider());

        // 15초마다 자동 새로고침
        _pollTimer = new Timer(async _ => await RefreshAllAsync(), null, TimeSpan.FromMilliseconds(300), TimeSpan.FromSeconds(15));
    }

    public async Task RefreshAllAsync()
    {
        if (_isRefreshing) return;
        _isRefreshing = true;

        try
        {
            var tasks = _providers.Select(p => p.FetchUsageAsync()).ToList();
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
            _isRefreshing = false;
        }
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
