using System.Threading;
using System.Threading.Tasks;
using Codenotch.Core.Models;

namespace Codenotch.Core.Interfaces;

public interface IUsageProvider
{
    string Id { get; }
    string DisplayName { get; }
    string BrandColor { get; }
    string Glyph { get; }
    int Priority { get; }

    Task<bool> IsAgentRunningAsync(CancellationToken cancellationToken = default)
        => IsAvailableAsync(cancellationToken);
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
    Task<UsageRecord> FetchUsageAsync(CancellationToken cancellationToken = default);
}
