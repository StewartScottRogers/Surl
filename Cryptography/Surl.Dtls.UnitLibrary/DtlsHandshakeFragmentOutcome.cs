namespace Surl.Dtls;

/// <summary>
/// What <see cref="DtlsHandshakeReassembler.Accept"/> did with one handshake fragment.
/// </summary>
public enum DtlsHandshakeFragmentOutcome
{
    /// <summary>The fragment's bytes were kept; its message still lacks some.</summary>
    Buffered,

    /// <summary>The fragment completed its message, which <see cref="DtlsHandshakeReassembler.TryTakeNext"/> hands over in turn.</summary>
    Completed,

    /// <summary>Every byte the fragment carries was already received; it was ignored.</summary>
    Duplicate,

    /// <summary>
    /// The fragment belongs to a message already handed over: the peer retransmitted its last
    /// flight (RFC 6347 section 4.2.4). It was ignored; the handshake may retransmit its own.
    /// </summary>
    Stale,

    /// <summary>The fragment is truncated or runs past its message's length: the fatal alert <c>decode_error</c>.</summary>
    Malformed,

    /// <summary>
    /// The fragment disagrees with an earlier one of the same <c>message_seq</c> about the message's
    /// type or length: the fatal alert <c>decode_error</c> (ADR-0087 decision 10).
    /// </summary>
    Contradictory,

    /// <summary>
    /// Keeping the fragment's message would pass the largest-message limit (<c>--max-message</c>,
    /// ADR-0087 decision 9): the fatal alert <c>handshake_failure</c>.
    /// </summary>
    OverLimit,
}
