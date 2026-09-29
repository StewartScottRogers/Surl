using static Surl.Protocol.Telnet.TelnetCodes;

namespace Surl.Protocol.Telnet;

/// <summary>
/// Splits the bytes a TELNET client sends into data, negotiations, subnegotiations and other
/// commands (RFC 854), one byte at a time, so a command split across reads is still whole.
/// </summary>
/// <remarks>
/// <c>IAC IAC</c> is the data byte 255, in data and inside a subnegotiation alike. Inside a
/// subnegotiation, <c>IAC</c> followed by anything but <c>SE</c> or <c>IAC</c> discards the
/// subnegotiation and is read as that command; so is <c>IAC SB IAC</c>, which names no
/// option. A
/// subnegotiation payload longer than the limit ends parsing with
/// <see cref="TelnetInboundKind.SubnegotiationTooLong"/> (ADR-0006, section 1). Not safe for
/// concurrent calls.
/// </remarks>
internal sealed class TelnetInboundParser
{
    private readonly long maxSubnegotiationBytes;
    private readonly List<byte> payload = [];
    private ParserState state = ParserState.Data;
    private byte verb;
    private byte subnegotiationOption;

    /// <summary>
    /// Creates a parser.
    /// </summary>
    /// <param name="maxSubnegotiationBytes">The most payload bytes a subnegotiation may hold; 0 means no limit.</param>
    public TelnetInboundParser(long maxSubnegotiationBytes)
    {
        this.maxSubnegotiationBytes = maxSubnegotiationBytes;
    }

    private enum ParserState
    {
        Data,
        AfterInterpretAsCommand,
        AfterVerb,
        AfterSubnegotiationBegin,
        InSubnegotiation,
        InSubnegotiationAfterInterpretAsCommand,
    }

    /// <summary>
    /// Whether the bytes so far end part way through a command, so a close now cuts one short.
    /// </summary>
    public bool IsPartWayThroughCommand => state != ParserState.Data;

    /// <summary>
    /// Takes the next inbound byte.
    /// </summary>
    /// <param name="value">The byte.</param>
    /// <returns>What the byte completed.</returns>
    public TelnetInbound Take(byte value) => state switch
    {
        ParserState.Data => TakeData(value),
        ParserState.AfterInterpretAsCommand => TakeAfterInterpretAsCommand(value),
        ParserState.AfterVerb => TakeOption(value),
        ParserState.AfterSubnegotiationBegin => TakeSubnegotiationOption(value),
        ParserState.InSubnegotiation => TakeSubnegotiationByte(value),
        _ => TakeInSubnegotiationAfterInterpretAsCommand(value),
    };

    private TelnetInbound TakeData(byte value)
    {
        if (value == InterpretAsCommand)
        {
            state = ParserState.AfterInterpretAsCommand;

            return TelnetInbound.Nothing;
        }

        return Data(value);
    }

    private TelnetInbound TakeAfterInterpretAsCommand(byte value)
    {
        state = value switch
        {
            InterpretAsCommand => ParserState.Data,
            >= Will and <= Dont => ParserState.AfterVerb,
            SubnegotiationBegin => ParserState.AfterSubnegotiationBegin,
            _ => ParserState.Data,
        };
        verb = value;

        return value switch
        {
            InterpretAsCommand => Data(value),
            >= SubnegotiationBegin and <= Dont => TelnetInbound.Nothing,
            _ => new TelnetInbound(TelnetInboundKind.Command, value, 0, ReadOnlyMemory<byte>.Empty),
        };
    }

    private TelnetInbound TakeOption(byte option)
    {
        state = ParserState.Data;

        return new TelnetInbound(TelnetInboundKind.Negotiation, verb, option, ReadOnlyMemory<byte>.Empty);
    }

    private TelnetInbound TakeSubnegotiationOption(byte option)
    {
        if (option == InterpretAsCommand)
        {
            state = ParserState.AfterInterpretAsCommand;

            return TelnetInbound.Nothing;
        }

        subnegotiationOption = option;
        payload.Clear();
        state = ParserState.InSubnegotiation;

        return TelnetInbound.Nothing;
    }

    private TelnetInbound TakeSubnegotiationByte(byte value)
    {
        if (value == InterpretAsCommand)
        {
            state = ParserState.InSubnegotiationAfterInterpretAsCommand;

            return TelnetInbound.Nothing;
        }

        return AppendToPayload(value);
    }

    private TelnetInbound TakeInSubnegotiationAfterInterpretAsCommand(byte value)
    {
        if (value == InterpretAsCommand)
        {
            state = ParserState.InSubnegotiation;

            return AppendToPayload(value);
        }

        if (value == SubnegotiationEnd)
        {
            state = ParserState.Data;

            return new TelnetInbound(TelnetInboundKind.Subnegotiation, 0, subnegotiationOption, payload.ToArray());
        }

        return TakeAfterInterpretAsCommand(value);
    }

    private TelnetInbound AppendToPayload(byte value)
    {
        if (maxSubnegotiationBytes > 0 && payload.Count >= maxSubnegotiationBytes)
        {
            return new TelnetInbound(TelnetInboundKind.SubnegotiationTooLong, 0, subnegotiationOption, ReadOnlyMemory<byte>.Empty);
        }

        payload.Add(value);

        return TelnetInbound.Nothing;
    }

    private static TelnetInbound Data(byte value) =>
        new(TelnetInboundKind.Data, value, 0, ReadOnlyMemory<byte>.Empty);
}
