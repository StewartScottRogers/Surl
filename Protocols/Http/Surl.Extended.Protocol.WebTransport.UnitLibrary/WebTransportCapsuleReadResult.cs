namespace Surl.Extended.Protocol.WebTransport;

/// <summary>The outcome of <see cref="WebTransportCapsuleReader.Read"/>.</summary>
/// <param name="Status">Whether a capsule was read.</param>
/// <param name="Capsule">The capsule when <paramref name="Status"/> is <see cref="WebTransportReadStatus.Complete"/>; otherwise <see langword="null"/>.</param>
/// <param name="BytesRead">The bytes the capsule took, whole or malformed; 0 when incomplete.</param>
public sealed record WebTransportCapsuleReadResult(WebTransportReadStatus Status, WebTransportCapsule? Capsule, int BytesRead);
