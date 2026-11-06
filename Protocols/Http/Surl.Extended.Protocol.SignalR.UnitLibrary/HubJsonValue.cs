namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// One JSON value of a hub message - an argument, a stream item or a result - kept as the UTF-8
/// bytes the client sent, so the server can send it back byte for byte (ADR-0119, decision 1).
/// </summary>
/// <param name="Utf8Json">The value's JSON text, exactly as it appeared in the record.</param>
public readonly record struct HubJsonValue(ReadOnlyMemory<byte> Utf8Json);
