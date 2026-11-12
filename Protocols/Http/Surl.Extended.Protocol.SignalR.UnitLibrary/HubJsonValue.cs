namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// One value of a hub message - an argument, a stream item or a result - kept as the bytes the
/// client sent, so the server can send it back byte for byte (ADR-0119, decision 1). On a connection
/// that chose <c>messagepack</c> (BL-756) the bytes are one MessagePack value, not JSON text.
/// </summary>
/// <param name="Utf8Json">The value's JSON text, exactly as it appeared in the record, or its MessagePack bytes.</param>
public readonly record struct HubJsonValue(ReadOnlyMemory<byte> Utf8Json);
