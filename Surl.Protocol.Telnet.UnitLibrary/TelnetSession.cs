using System.Buffers;
using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Telnet.TelnetCodes;

namespace Surl.Protocol.Telnet;

/// <summary>
/// One TELNET session, with no transport: takes the bytes the client sends and collects the
/// bytes the server answers, so <see cref="TelnetProtocolServer"/> only moves bytes.
/// <see cref="TelnetProtocolServer"/>'s remarks describe what it answers.
/// </summary>
/// <remarks>Not safe for concurrent calls.</remarks>
internal sealed class TelnetSession
{
    private static readonly byte[] Banner =
        Encoding.ASCII.GetBytes("surl TELNET server: each line is echoed back; quit ends the session.\r\n");

    private static readonly byte[] LineEnding = "\r\n"u8.ToArray();
    private static readonly byte[] Bye = "bye\r\n"u8.ToArray();
    private static readonly byte[] LineTooLong = "line too long\r\n"u8.ToArray();

    private readonly IExchangeLog log;
    private readonly long maxLineBytes;
    private readonly TelnetInboundParser parser;
    private readonly TelnetLineAssembler lines;
    private readonly TelnetOptionNegotiator negotiator = new();
    private readonly bool[] reportAwaited = new bool[256];
    private readonly ArrayBufferWriter<byte> outbound = new();
    private int unansweredSends;
    private bool quitReceived;
    private bool cutShort;

    /// <summary>
    /// Starts a session: the opening negotiation and the banner wait in <see cref="TakeOutbound"/>.
    /// </summary>
    /// <param name="log">Where the session's notes go.</param>
    /// <param name="maxLineBytes">The most bytes a line or a subnegotiation may hold; 0 means no limit.</param>
    public TelnetSession(IExchangeLog log, long maxLineBytes)
    {
        this.log = log;
        this.maxLineBytes = maxLineBytes;
        parser = new TelnetInboundParser(maxLineBytes);
        lines = new TelnetLineAssembler(maxLineBytes);
        negotiator.WriteOpening(outbound);
        outbound.Write(Banner);
    }

    /// <summary>
    /// Whether the session is over, so the server closes the connection: the client sent
    /// <c>quit</c> and has answered every <c>SEND</c> the server made, or a line or
    /// subnegotiation was too long.
    /// </summary>
    public bool IsOver => cutShort || (quitReceived && unansweredSends == 0);

    /// <summary>
    /// Takes bytes the client sent, stopping as soon as the session is over.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    public void Receive(ReadOnlySpan<byte> bytes)
    {
        for (var index = 0; index < bytes.Length && !IsOver; index++)
        {
            Answer(parser.Take(bytes[index]));
        }
    }

    /// <summary>
    /// Notes that the client closed the connection, and whether it cut a command short.
    /// </summary>
    public void ReceiveClose()
    {
        if (parser.IsPartWayThroughCommand)
        {
            log.Note("The client closed the connection part way through a TELNET command.");
        }
    }

    /// <summary>
    /// The bytes the server answered since the last call, IAC doubled in data.
    /// </summary>
    /// <returns>The bytes; empty when there are none.</returns>
    public byte[] TakeOutbound()
    {
        var bytes = outbound.WrittenSpan.ToArray();
        outbound.ResetWrittenCount();

        return bytes;
    }

    private void Answer(TelnetInbound inbound)
    {
        switch (inbound.Kind)
        {
            case TelnetInboundKind.Data:
                AnswerData(inbound.Value);
                break;
            case TelnetInboundKind.Negotiation:
                AnswerNegotiation(inbound.Value, inbound.Option);
                break;
            case TelnetInboundKind.Subnegotiation:
                ReportSubnegotiation(inbound.Option, inbound.Payload.Span);
                break;
            case TelnetInboundKind.SubnegotiationTooLong:
                log.Note($"A subnegotiation for option {inbound.Option} was longer than {maxLineBytes} bytes; closed.");
                cutShort = true;
                break;
        }
    }

    private void AnswerData(byte value)
    {
        if (quitReceived)
        {
            return;
        }

        switch (lines.Take(value))
        {
            case TelnetLineStatus.Completed:
                AnswerLine(lines.CompletedLine);
                break;
            case TelnetLineStatus.TooLong:
                log.Note($"A line was longer than {maxLineBytes} bytes; answered line too long and closed.");
                outbound.Write(LineTooLong);
                cutShort = true;
                break;
        }
    }

    private void AnswerLine(byte[] line)
    {
        if (Ascii.EqualsIgnoreCase(line, "quit"u8))
        {
            quitReceived = true;
            outbound.Write(Bye);

            return;
        }

        WriteEscapedData(line);
        outbound.Write(LineEnding);
    }

    private void AnswerNegotiation(byte verb, byte option)
    {
        switch (negotiator.Answer(verb, option, outbound))
        {
            case TelnetClientOptionChange.TurnedOn:
                AwaitReport(option);
                break;
            case TelnetClientOptionChange.TurnedOff:
                StopAwaitingReport(option);
                break;
        }
    }

    private void AwaitReport(byte option)
    {
        if (!AsksForValue(option))
        {
            reportAwaited[option] = option == WindowSizeOption;

            return;
        }

        if (!quitReceived)
        {
            reportAwaited[option] = true;
            unansweredSends++;
            outbound.Write([InterpretAsCommand, SubnegotiationBegin, option, Send, InterpretAsCommand, SubnegotiationEnd]);
        }
    }

    private void StopAwaitingReport(byte option)
    {
        if (reportAwaited[option])
        {
            reportAwaited[option] = false;
            unansweredSends -= AsksForValue(option) ? 1 : 0;
        }
    }

    private void ReportSubnegotiation(byte option, ReadOnlySpan<byte> payload)
    {
        if (reportAwaited[option])
        {
            StopAwaitingReport(option);
            log.Note(TelnetSubnegotiationNote.Describe(option, payload));
        }
    }

    private void WriteEscapedData(ReadOnlySpan<byte> data)
    {
        foreach (var value in data)
        {
            outbound.Write(value == InterpretAsCommand ? [InterpretAsCommand, InterpretAsCommand] : [value]);
        }
    }

    private static bool AsksForValue(byte option) =>
        option is TerminalTypeOption or DisplayLocationOption or NewEnvironmentOption;
}
