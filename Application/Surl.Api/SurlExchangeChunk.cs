namespace Surl.Api;

/// <summary>
/// The bytes one read or one write of an exchange carried, as the serving engine reported them
/// (ADR-0099, decision 7): a stream read or write, or one whole datagram.
/// </summary>
/// <param name="Direction">Which way the bytes crossed.</param>
/// <param name="Bytes">The bytes, a copy the record owns.</param>
public sealed record SurlExchangeChunk(SurlExchangeDirection Direction, ReadOnlyMemory<byte> Bytes);
