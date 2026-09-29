namespace Surl.Protocol.Abstractions;

/// <summary>
/// What <c>surl</c> was asked to listen on: the scheme that picks the protocol server, the
/// host to bind and the port asked for, and, once a listener has bound, the port it got
/// (ADR-0004, section 1).
/// </summary>
/// <param name="Scheme">The scheme, lower-cased by <c>Surl.Cli</c> (<c>http</c>, <c>https</c>, <c>tftp</c>).</param>
/// <param name="Host">
/// The host to bind, as written, with IPv6 brackets removed: an IPv4 literal, an IPv6
/// literal, or a host name.
/// </param>
/// <param name="Port">The port asked for, 0 to 65535; 0 asks for an ephemeral port.</param>
public sealed record ListenUrl(string Scheme, string Host, int Port)
{
    /// <summary>
    /// The port a listener actually bound, or <see langword="null"/> until one has. Equal to
    /// <see cref="Port"/> unless <see cref="Port"/> was 0.
    /// </summary>
    public int? BoundPort { get; init; }

    /// <summary>
    /// Returns a copy of this listen URL with <see cref="BoundPort"/> set. The listener calls
    /// it once it has bound.
    /// </summary>
    /// <param name="boundPort">The port the listener bound, 1 to 65535.</param>
    /// <returns>The same listen URL with <see cref="BoundPort"/> set to <paramref name="boundPort"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="boundPort"/> is outside 1 to 65535.</exception>
    public ListenUrl WithBoundPort(int boundPort)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(boundPort, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(boundPort, 65535);

        return this with { BoundPort = boundPort };
    }
}
