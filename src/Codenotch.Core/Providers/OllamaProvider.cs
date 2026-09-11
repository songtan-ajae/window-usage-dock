using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Codenotch.Core.Interfaces;
using Codenotch.Core.Models;

namespace Codenotch.Core.Providers;

public class OllamaProvider : IUsageProvider
{
    private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };

    public string Id => "ollama";
    public string DisplayName => "Ollama";
    public string BrandColor => "#FFFFFF"; // Clean White / Gray
    public string Glyph => "OL";
    public int Priority => 6;

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var resp = await _httpClient.GetAsync("http://127.0.0.1:11434/api/tags", cancellationToken);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<UsageRecord> FetchUsageAsync(CancellationToken cancellationToken = default)
    {
        var record = new UsageRecord
        {
            ProviderId = Id,
            DisplayName = DisplayName,
            BrandColor = BrandColor,
            Glyph = Glyph,
            PlanName = "Local Models",
            IsConnected = false,
            ConnectionHint = "Start Ollama to monitor your local models."
        };

        try
        {
            var resp = await _httpClient.GetAsync("http://127.0.0.1:11434/api/tags", cancellationToken);
            if (resp.IsSuccessStatusCode)
            {
                record.IsConnected = true;
                var content = await resp.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(content);
                int modelCount = 0;
                if (doc.RootElement.TryGetProperty("models", out var modelsElem) && modelsElem.ValueKind == JsonValueKind.Array)
                {
                    modelCount = modelsElem.GetArrayLength();
                }

                // Check running models
                int runningCount = 0;
                string runningModelName = string.Empty;
                try
                {
                    var psResp = await _httpClient.GetAsync("http://127.0.0.1:11434/api/ps", cancellationToken);
                    if (psResp.IsSuccessStatusCode)
                    {
                        var psContent = await psResp.Content.ReadAsStringAsync(cancellationToken);
                        using var psDoc = JsonDocument.Parse(psContent);
                        if (psDoc.RootElement.TryGetProperty("models", out var rModels) && rModels.ValueKind == JsonValueKind.Array)
                        {
                            runningCount = rModels.GetArrayLength();
                            if (runningCount > 0)
                            {
                                runningModelName = rModels[0].GetProperty("name").GetString() ?? "";
                            }
                        }
                    }
                }
                catch { }

                record.PrimaryUsedPercentage = runningCount > 0 ? 80.0 : 0.0;
                record.PrimaryStatusText = runningCount > 0 ? $"Running: {runningModelName}" : $"{modelCount} models installed";
                record.ResetTimeText = "Local Server";
                record.Status = runningCount > 0 ? SessionStatus.Working : SessionStatus.Idle;

                return record;
            }
        }
        catch { }

        record.PrimaryStatusText = "Ollama Offline";
        return record;
    }
}
