using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Codenotch.Core.Interfaces;
using Codenotch.Core.Models;

namespace Codenotch.Core.Providers;

public class ClaudeProvider : IUsageProvider
{
    public string Id => "claude";
    public string DisplayName => "클로드";
    public string BrandColor => "#D97706"; // Anthropic Amber/Terracotta
    public string Glyph => "CC";
    public int Priority => 3;

    private string GetClaudeDir()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userProfile, ".claude");
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Directory.Exists(GetClaudeDir()));
    }

    public async Task<UsageRecord> FetchUsageAsync(CancellationToken cancellationToken = default)
    {
        var record = new UsageRecord
        {
            ProviderId = Id,
            DisplayName = DisplayName,
            BrandColor = BrandColor,
            Glyph = Glyph,
            PlanName = "미연결",
            IsConnected = false,
            PrimaryUsedPercentage = 0.0,
            PrimaryStatusText = "활성 세션 없음",
            ResetTimeText = "로그인 대기",
            ConnectionHint = "터미널에서 `claude`를 실행하여 로그인하세요."
        };

        var claudeDir = GetClaudeDir();
        if (!Directory.Exists(claudeDir))
        {
            return record;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c claude /usage",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc != null)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
                var output = await proc.StandardOutput.ReadToEndAsync(cts.Token);
                await proc.WaitForExitAsync(cts.Token);

                if (!string.IsNullOrWhiteSpace(output))
                {
                    var match = Regex.Match(output, @"(\d+)%\s+of\s+(\w+)\s+limit", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        double pct = double.Parse(match.Groups[1].Value);
                        record.IsConnected = true;
                        record.PlanName = "Claude Pro";
                        record.PrimaryUsedPercentage = pct;
                        record.PrimaryStatusText = $"{100.0 - pct:F0}% 남음 · {pct:F0}% 사용됨";
                        record.ResetTimeText = "현재 윈도우 진행 중";
                        record.SubWindows.Add(new UsageQuotaWindow
                        {
                            Label = "5시간 세션",
                            UsedPercentage = pct,
                            UsedText = $"{100.0 - pct:F0}% 남음 ({pct:F0}% 사용됨)",
                            ResetsInText = "현재 세션"
                        });
                        return record;
                    }
                }
            }
        }
        catch { }

        return record;
    }
}
