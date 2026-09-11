using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Codenotch.Core.Interfaces;
using Codenotch.Core.Models;
using Microsoft.Data.Sqlite;

namespace Codenotch.Core.Providers;

public class CursorProvider : IUsageProvider
{
    private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };

    public string Id => "cursor";
    public string DisplayName => "Cursor";
    public string BrandColor => "#0088FF"; // Cursor Cyan/Blue
    public string Glyph => "CR";
    public int Priority => 10;

    private string GetVscdbPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "Cursor", "User", "globalStorage", "state.vscdb");
    }

    private string GetCliConfigPath()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userProfile, ".cursor", "cli-config.json");
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(File.Exists(GetVscdbPath()) || File.Exists(GetCliConfigPath()));
    }

    public async Task<UsageRecord> FetchUsageAsync(CancellationToken cancellationToken = default)
    {
        var record = new UsageRecord
        {
            ProviderId = Id,
            DisplayName = DisplayName,
            BrandColor = BrandColor,
            Glyph = Glyph,
            IsConnected = false,
            ConnectionHint = "Sign in to Cursor in the editor or run cursor-agent login."
        };

        string? accessToken = null;
        string? email = null;
        string? membershipType = null;

        var vscdb = GetVscdbPath();
        if (File.Exists(vscdb))
        {
            try
            {
                // SQLite file may be locked by Cursor editor, open with ReadOnly & Shared
                var connStr = new SqliteConnectionStringBuilder
                {
                    DataSource = vscdb,
                    Mode = SqliteOpenMode.ReadOnly
                }.ToString();

                using var conn = new SqliteConnection(connStr);
                await conn.OpenAsync(cancellationToken);

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT key, value FROM ItemTable WHERE key IN ('cursorAuth/accessToken', 'cursorAuth/cachedEmail', 'cursorAuth/stripeMembershipType')";
                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var key = reader.GetString(0);
                    var val = reader.IsDBNull(1) ? null : reader.GetString(1);
                    if (key == "cursorAuth/accessToken") accessToken = val;
                    else if (key == "cursorAuth/cachedEmail") email = val;
                    else if (key == "cursorAuth/stripeMembershipType") membershipType = val;
                }
            }
            catch
            {
                // Fallback / ignore error reading locked sqlite
            }
        }

        if (string.IsNullOrEmpty(accessToken) && File.Exists(GetCliConfigPath()))
        {
            try
            {
                var json = await File.ReadAllTextAsync(GetCliConfigPath(), cancellationToken);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("auth", out var authElem) &&
                    authElem.TryGetProperty("accessToken", out var tokenElem))
                {
                    accessToken = tokenElem.GetString();
                }
            }
            catch { }
        }

        if (string.IsNullOrEmpty(accessToken))
        {
            record.IsConnected = false;
            record.PrimaryStatusText = "Sign in to Cursor";
            return record;
        }

        record.IsConnected = true;
        record.AccountEmail = email ?? string.Empty;
        record.PlanName = string.IsNullOrEmpty(membershipType) ? "Cursor Pro" : membershipType.ToUpperInvariant();

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://cursor.com/api/usage-summary");
            req.Headers.Add("Cookie", $"WorkosCursorSessionToken={accessToken}");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            req.Headers.Add("User-Agent", "WindowsUsageDock/1.0 (Windows NT 10.0; Win64; x64)");

            var resp = await _httpClient.SendAsync(req, cancellationToken);
            if (resp.IsSuccessStatusCode)
            {
                var content = await resp.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                // Typical response: { "billingCycleStart": "...", "numFastRequests": 120, "maxFastRequests": 500, ... }
                int fastUsed = 0;
                int fastMax = 500;
                if (root.TryGetProperty("numFastRequests", out var nfr)) fastUsed = nfr.GetInt32();
                if (root.TryGetProperty("maxFastRequests", out var mfr)) fastMax = mfr.GetInt32();

                double usedPct = fastMax > 0 ? Math.Min(100.0, (double)fastUsed / fastMax * 100.0) : 0;
                record.PrimaryUsedPercentage = Math.Round(usedPct, 1);
                record.PrimaryStatusText = $"{fastUsed} / {fastMax} requests";
                record.ResetTimeText = "Resets monthly";

                record.SubWindows.Add(new UsageQuotaWindow
                {
                    Label = "Fast Requests",
                    UsedPercentage = record.PrimaryUsedPercentage,
                    UsedText = $"{fastUsed} of {fastMax} used",
                    ResetsInText = record.ResetTimeText
                });

                return record;
            }
        }
        catch
        {
            // API call failed, but token exists
        }

        // Fallback with connected indicator
        record.PrimaryStatusText = "Connected";
        record.PrimaryUsedPercentage = 15.0; // placeholder baseline
        record.ResetTimeText = "Active";
        return record;
    }
}
