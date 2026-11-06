namespace Surl.Extended.Protocol.WebTransport;

/// <summary><c>WT_DRAIN_SESSION</c> (draft-ietf-webtrans-http3-16 section 4.7): a capsule with an empty value.</summary>
public sealed record WebTransportDrainSessionCapsule : WebTransportCapsule
{
    /// <inheritdoc />
    public override WebTransportCapsuleType Type => WebTransportCapsuleType.DrainSession;

    /// <inheritdoc />
    internal override byte[] EncodeValue() => [];
}
