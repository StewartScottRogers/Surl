namespace Surl.Protocol.Telnet;

/// <summary>
/// What one inbound byte completed, as <see cref="TelnetInboundParser"/> reports it.
/// </summary>
/// <param name="Kind">What was completed, or <see cref="TelnetInboundKind.Nothing"/>.</param>
/// <param name="Value">
/// The data byte, the negotiation verb (<c>WILL</c>, <c>WONT</c>, <c>DO</c> or <c>DONT</c>), or
/// the command byte; 0 otherwise.
/// </param>
/// <param name="Option">The option a negotiation or subnegotiation names; 0 otherwise.</param>
/// <param name="Payload">A subnegotiation's bytes after its option, with <c>IAC IAC</c> undone; empty otherwise.</param>
internal readonly record struct TelnetInbound(TelnetInboundKind Kind, byte Value, byte Option, ReadOnlyMemory<byte> Payload)
{
    public static TelnetInbound Nothing { get; } = new(TelnetInboundKind.Nothing, 0, 0, ReadOnlyMemory<byte>.Empty);
}
