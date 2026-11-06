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
    /// The Unix domain socket to listen on instead of a TCP port, or <see langword="null"/> to
    /// listen on <see cref="Host"/> and <see cref="Port"/> (ADR-0094, decision 6). When it is set,
    /// <see cref="Host"/> and <see cref="Port"/> are only what the server expects in the requests
    /// it reads, nothing is bound on them, and <see cref="BoundPort"/> stays unset.
    /// </summary>
    public UnixSocketAddress? UnixSocket { get; init; }

    /// <summary>
    /// Whether this listen URL was named by <c>--proxy</c>, so its listener serves a proxy curl
    /// reaches with <c>-x</c> rather than an origin (ADR-0088, decisions 6 and 7). An
    /// <c>https</c> proxy listener offers exactly the ALPN protocol ID <c>http/1.1</c>, where an
    /// <c>https</c> origin listener offers <c>h2</c> first. <see langword="false"/> unless set.
    /// </summary>
    public bool IsProxy { get; init; }

    private readonly bool? isImplicitTls;

    /// <summary>
    /// Whether every connection to this listen URL starts with a TLS handshake (ADR-0010,
    /// section 2). Unless set, it is <see cref="TlsSchemes.IsImplicitTls"/> of
    /// <see cref="Scheme"/>; the composition sets it to <see langword="false"/> to serve a
    /// scheme that is TLS by default, such as <c>s3</c>, as plaintext on this listen URL alone,
    /// while another listen URL in the same process keeps the default (BL-1020).
    /// </summary>
    public bool IsImplicitTls
    {
        get => isImplicitTls ?? TlsSchemes.IsImplicitTls(Scheme);
        init => isImplicitTls = value;
    }

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
