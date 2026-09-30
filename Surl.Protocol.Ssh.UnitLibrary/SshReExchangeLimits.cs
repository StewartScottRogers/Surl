namespace Surl.Protocol.Ssh;

/// <summary>
/// When the server starts a key re-exchange itself (RFC 4253 section 9, RFC 4344 section 3.1;
/// ADR-0051 decision 2.1): once either direction has carried <see cref="Bytes"/> under one set
/// of keys, or <see cref="Interval"/> has passed since the last exchange. Both are checked
/// between the client's packets.
/// </summary>
/// <param name="Bytes">The most bytes either direction carries under one set of keys.</param>
/// <param name="Interval">The longest one set of keys is used.</param>
internal sealed record SshReExchangeLimits(long Bytes, TimeSpan Interval)
{
    /// <summary>
    /// ADR-0051's limits: 1 GiB in either direction, or one hour.
    /// </summary>
    public static SshReExchangeLimits Default { get; } = new(1L << 30, TimeSpan.FromHours(1));
}
