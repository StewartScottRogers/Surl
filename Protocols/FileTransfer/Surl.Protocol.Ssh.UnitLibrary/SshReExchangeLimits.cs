namespace Surl.Protocol.Ssh;

/// <summary>
/// When the server starts a key re-exchange itself (RFC 4253 section 9, RFC 4344 section 3.1;
/// ADR-0051 decision 2.1): once either direction has carried <see cref="Bytes"/> under one set
/// of keys, or <see cref="Interval"/> has passed since the last exchange. Both are checked
/// between the client's packets. While the server waits for the client's <c>KEXINIT</c>, it holds
/// at most <see cref="HeldBytes"/> of the client's other messages to answer after its
/// <c>NEWKEYS</c> (ADR-0060).
/// </summary>
/// <param name="Bytes">The most bytes either direction carries under one set of keys.</param>
/// <param name="Interval">The longest one set of keys is used.</param>
/// <param name="HeldBytes">
/// The most payload bytes held between the server's <c>KEXINIT</c> and the client's in a
/// server-started re-exchange; more is <c>DISCONNECT</c> 2.
/// </param>
internal sealed record SshReExchangeLimits(long Bytes, TimeSpan Interval, long HeldBytes = SshReExchangeLimits.DefaultHeldBytes)
{
    /// <summary>
    /// What a client may rightly send before it sees the server's <c>KEXINIT</c>: every open
    /// channel's whole window of data, and a mebibyte for the messages around it.
    /// </summary>
    public const long DefaultHeldBytes = (SshConnectionProtocol.MaxOpenChannels * (long)SshSessionChannel.WindowBytes) + (1L << 20);

    /// <summary>
    /// ADR-0051's limits: 1 GiB in either direction, or one hour.
    /// </summary>
    public static SshReExchangeLimits Default { get; } = new(1L << 30, TimeSpan.FromHours(1));
}
