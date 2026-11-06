using Surl.Quic;

namespace Surl.Extended.Protocol.WebTransport;

/// <summary>
/// One of the six flow-control capsules whose value is a single QUIC variable-length integer
/// (draft-ietf-webtrans-http3-16 sections 5.6.2 to 5.6.5): <c>WT_MAX_DATA</c>, both
/// <c>WT_MAX_STREAMS</c>, <c>WT_DATA_BLOCKED</c> and both <c>WT_STREAMS_BLOCKED</c>.
/// </summary>
public sealed record WebTransportLimitCapsule : WebTransportCapsule
{
    private static readonly HashSet<WebTransportCapsuleType> LimitTypes =
    [
        WebTransportCapsuleType.MaxData,
        WebTransportCapsuleType.MaxStreamsBidirectional,
        WebTransportCapsuleType.MaxStreamsUnidirectional,
        WebTransportCapsuleType.DataBlocked,
        WebTransportCapsuleType.StreamsBlockedBidirectional,
        WebTransportCapsuleType.StreamsBlockedUnidirectional,
    ];

    /// <summary>Creates the capsule.</summary>
    /// <param name="type">One of the six limit capsule types.</param>
    /// <param name="limit">The limit; at most <see cref="QuicVariableLengthInteger.MaxValue"/>.</param>
    /// <exception cref="ArgumentException"><paramref name="type"/> is not a limit capsule type.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is above <see cref="QuicVariableLengthInteger.MaxValue"/>.</exception>
    public WebTransportLimitCapsule(WebTransportCapsuleType type, ulong limit)
    {
        if (!IsLimitType(type))
        {
            throw new ArgumentException($"Capsule type 0x{(ulong)type:x} is not a limit capsule.", nameof(type));
        }

        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, QuicVariableLengthInteger.MaxValue);
        Type = type;
        Limit = limit;
    }

    /// <inheritdoc />
    public override WebTransportCapsuleType Type { get; }

    /// <summary>The limit the capsule carries: a count of bytes or of streams.</summary>
    public ulong Limit { get; }

    /// <summary>Says whether <paramref name="type"/> is one of the six limit capsule types.</summary>
    /// <param name="type">The capsule type.</param>
    /// <returns><see langword="true"/> for a limit capsule type.</returns>
    public static bool IsLimitType(WebTransportCapsuleType type) => LimitTypes.Contains(type);

    /// <inheritdoc />
    internal override byte[] EncodeValue() => QuicVariableLengthInteger.Encode(Limit);
}
