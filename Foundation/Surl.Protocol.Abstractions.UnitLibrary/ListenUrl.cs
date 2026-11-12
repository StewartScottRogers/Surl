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
    /// <c>https</c> proxy listener offers the ALPN protocol IDs <c>h2</c> then <c>http/1.1</c>, as an
    /// <c>https</c> origin listener does (ADR-0088, Amendment 9). <see langword="false"/> unless set.
    /// </summary>
    public bool IsProxy { get; init; }

    /// <summary>
    /// The one listener this listen URL binds, or <see langword="null"/> to bind every listener
    /// its scheme's servers take: the connection listener, the datagram listener, or both on one
    /// port for a scheme with a server of each (ADR-0082, decision 6). The composition sets it so
    /// one <c>dns</c> listen URL serves UDP alone while another serves TCP with TLS from the first
    /// byte (ADR-0092, Amendment 1). A listen URL naming a listener its scheme has no server for
    /// is refused before any listener binds. <see langword="null"/> unless set.
    /// </summary>
    public ListenTransport? OnlyTransport { get; init; }

    /// <summary>
    /// Whether this <c>dns</c> listen URL serves DNS over HTTPS (RFC 8484) rather than DNS over TLS,
    /// both of which are TLS from the first byte: the composition sets it for
    /// <c>--dns-transport https</c> (ADR-0092, decision 5 and Amendment 1), so the listener offers
    /// ALPN <c>h2</c> then <c>http/1.1</c> and the server answers HTTP. <see langword="false"/> unless set.
    /// </summary>
    public bool IsDnsOverHttps { get; init; }

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
