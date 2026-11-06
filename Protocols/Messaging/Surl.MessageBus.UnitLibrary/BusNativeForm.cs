namespace Surl.MessageBus;

/// <summary>
/// A publisher's own encoding of a <see cref="BusMessage"/>, tagged with the format it is in
/// (ADR-0107 decision 6). The bus carries the bytes unchanged, through live fan-out, named
/// queues, topic logs and its files; a server reading messages in <see cref="Format"/> uses
/// them, and every other server ignores them.
/// </summary>
/// <param name="Format">The format the bytes are in: the lower-case scheme family name of the
/// server that wrote them, <c>amqp1</c> for the AMQP 1.0 container.</param>
/// <param name="Bytes">The encoded message.</param>
public sealed record BusNativeForm(string Format, ReadOnlyMemory<byte> Bytes);
