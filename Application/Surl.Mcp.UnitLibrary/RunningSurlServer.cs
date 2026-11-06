using Surl.Api;

namespace Surl.Mcp;

/// <summary>One server <c>start_server</c> started and <c>stop_server</c> has not stopped.</summary>
/// <param name="ServerId">The ID the tools name it by: <c>s</c> and a decimal counter from 1, never reused.</param>
/// <param name="ListenUrls">The listen URLs it was asked for, as given.</param>
/// <param name="Server">The running server.</param>
/// <param name="StartedAt">When it started, by the host's clock.</param>
public sealed record RunningSurlServer(string ServerId, IReadOnlyList<string> ListenUrls, SurlServer Server, DateTimeOffset StartedAt)
{
    /// <summary>The snapshots <c>snapshot_server</c> took of it, newest <see cref="ServerSnapshots.MaxSnapshots"/> kept.</summary>
    public ServerSnapshots Snapshots { get; } = new();
}
