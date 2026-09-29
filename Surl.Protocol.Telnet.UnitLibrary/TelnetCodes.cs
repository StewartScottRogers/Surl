namespace Surl.Protocol.Telnet;

/// <summary>
/// The TELNET command and option bytes the server reads and writes: RFC 854's commands, and
/// the option numbers of RFC 856 (BINARY), RFC 858 (SGA), RFC 1091 (TTYPE), RFC 1073 (NAWS),
/// RFC 1096 (XDISPLOC) and RFC 1572 (NEW-ENVIRON).
/// </summary>
internal static class TelnetCodes
{
    /// <summary>SE: the end of a subnegotiation.</summary>
    public const byte SubnegotiationEnd = 240;

    /// <summary>SB: the start of a subnegotiation.</summary>
    public const byte SubnegotiationBegin = 250;

    /// <summary>WILL: the sender offers, or agrees, to perform an option.</summary>
    public const byte Will = 251;

    /// <summary>WONT: the sender refuses, or stops, performing an option.</summary>
    public const byte Wont = 252;

    /// <summary>DO: the sender asks, or agrees, that the other side performs an option.</summary>
    public const byte Do = 253;

    /// <summary>DONT: the sender asks the other side not to perform an option.</summary>
    public const byte Dont = 254;

    /// <summary>IAC: "interpret as command"; doubled, it is the data byte 255.</summary>
    public const byte InterpretAsCommand = 255;

    /// <summary>Option 0, BINARY (RFC 856).</summary>
    public const byte BinaryOption = 0;

    /// <summary>Option 3, SUPPRESS-GO-AHEAD (RFC 858).</summary>
    public const byte SuppressGoAheadOption = 3;

    /// <summary>Option 24, TERMINAL-TYPE (RFC 1091).</summary>
    public const byte TerminalTypeOption = 24;

    /// <summary>Option 31, NAWS, the client's window size (RFC 1073).</summary>
    public const byte WindowSizeOption = 31;

    /// <summary>Option 35, X-DISPLAY-LOCATION (RFC 1096).</summary>
    public const byte DisplayLocationOption = 35;

    /// <summary>Option 39, NEW-ENVIRON (RFC 1572).</summary>
    public const byte NewEnvironmentOption = 39;

    /// <summary>IS: the first byte of a subnegotiation that answers SEND.</summary>
    public const byte Is = 0;

    /// <summary>SEND: the first byte of a subnegotiation that asks for a value.</summary>
    public const byte Send = 1;
}
