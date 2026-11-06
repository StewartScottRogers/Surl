namespace Surl.Dtls;

/// <summary>
/// Looks a PSK identity up (ADR-0087 decision 3): the caller's bridge to its accounts, which this
/// library does not know. The CoAP server composes it from <c>--user</c> and <c>--user-file</c>.
/// </summary>
/// <param name="identity">The identity the client's <c>ClientKeyExchange</c> names (RFC 4279 section 2).</param>
/// <returns>
/// The identity's key, or <see langword="null"/> when no such identity exists; an empty key counts
/// as none, since it authenticates nothing.
/// </returns>
public delegate byte[]? DtlsPreSharedKeyLookup(ReadOnlySpan<byte> identity);
