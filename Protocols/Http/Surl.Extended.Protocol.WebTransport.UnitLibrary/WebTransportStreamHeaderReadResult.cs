namespace Surl.Extended.Protocol.WebTransport;

/// <summary>The outcome of <see cref="WebTransportStreamHeader.Read"/>.</summary>
/// <param name="Status">Whether a header was read.</param>
/// <param name="Header">The header when <paramref name="Status"/> is <see cref="WebTransportReadStatus.Complete"/>; otherwise <see langword="null"/>.</param>
/// <param name="BytesRead">The bytes the header took; for a malformed one, the bytes of the value that is neither <c>0x41</c> nor <c>0x54</c>; 0 when incomplete.</param>
public sealed record WebTransportStreamHeaderReadResult(WebTransportReadStatus Status, WebTransportStreamHeader? Header, int BytesRead);
