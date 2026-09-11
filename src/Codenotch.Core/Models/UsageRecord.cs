using System;
using System.Collections.Generic;

namespace Codenotch.Core.Models;

public enum SessionStatus
{
    Idle,
    Working,
    Waiting,
    Error
}

public class UsageQuotaWindow
{
    public string Label { get; set; } = string.Empty; // e.g. "5-hour", "Monthly", "Weekly"
    public double UsedPercentage { get; set; }        // 0.0 ~ 100.0
    public double RemainingPercentage => Math.Max(0, 100.0 - UsedPercentage);
    public string UsedText { get; set; } = string.Empty; // e.g. "72 / 100 requests" or "$14.20"
    public string ResetsInText { get; set; } = string.Empty; // e.g. "Resets in 2h 15m" or "28 Sep"
}

public class UsageRecord
{
    public string ProviderId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PlanName { get; set; } = "Active";
    public string AccountEmail { get; set; } = string.Empty;
    public bool IsConnected { get; set; } = true;
    public string ConnectionHint { get; set; } = string.Empty;
    
    // Primary gauge (0.0 to 100.0)
    public double PrimaryUsedPercentage { get; set; }
    public double PrimaryRemainingPercentage => Math.Max(0, 100.0 - PrimaryUsedPercentage);
    
    public string PrimaryStatusText { get; set; } = string.Empty;
    public string ResetTimeText { get; set; } = string.Empty; // e.g. "Resets in 3h 20m"
    public SessionStatus Status { get; set; } = SessionStatus.Idle;
    
    public string BrandColor { get; set; } = "#3B82F6"; // Default Blue
    public string Glyph { get; set; } = "AI";

    public List<UsageQuotaWindow> SubWindows { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.Now;
}
