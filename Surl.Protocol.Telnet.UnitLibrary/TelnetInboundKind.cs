namespace Surl.Protocol.Telnet;

/// <summary>
/// What an inbound byte completed (<see cref="TelnetInbound.Kind"/>).
/// </summary>
internal enum TelnetInboundKind
{
    /// <summary>The byte is part of a command that is not complete yet.</summary>
    Nothing,

    /// <summary>A data byte, <c>IAC IAC</c> undone.</summary>
    Data,

    /// <summary><c>IAC WILL</c>, <c>WONT</c>, <c>DO</c> or <c>DONT</c> and its option.</summary>
    Negotiation,

    /// <summary><c>IAC SB</c> option payload <c>IAC SE</c>.</summary>
    Subnegotiation,

    /// <summary>A subnegotiation longer than the line limit; the session ends.</summary>
    SubnegotiationTooLong,

    /// <summary>Any other two-byte command, such as <c>IAC NOP</c> or <c>IAC AYT</c>.</summary>
    Command,
}
