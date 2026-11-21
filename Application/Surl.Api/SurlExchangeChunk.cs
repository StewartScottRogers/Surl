using Surl.Protocol.Abstractions;

namespace Surl.Api;

/// <summary>
/// The bytes one read or one write of an exchange carried, as the serving engine reported them
/// (ADR-0099, decision 7): a stream read or write, or one whole datagram.
/// </summary>
/// <param name="Direction">Which way the bytes crossed.</param>
/// <param name="Bytes">The bytes, a copy the record owns.</param>
/// <param name="Layer">Which layer the bytes belong to (ADR-0169 decision 2); <see cref="ExchangeBytesLayer.Wire"/> by default.</param>
/// <param name="StreamId">The QUIC stream the bytes belong to, or <see langword="null"/> when they belong to none.</param>
/// <param name="At">When the bytes crossed, as the <see cref="ExchangeBytesEvent"/> carried it from the instance's <see cref="TimeProvider"/>; default when built by hand.</param>
public sealed record SurlExchangeChunk(
    SurlExchangeDirection Direction,
    ReadOnlyMemory<byte> Bytes,
    ExchangeBytesLayer Layer = ExchangeBytesLayer.Wire,
    long? StreamId = null,
    DateTimeOffset At = default);
