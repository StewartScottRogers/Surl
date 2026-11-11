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
    private readonly bool echoesVerbatim;
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
    /// <param name="echoesVerbatim">
    /// Echo-and-close mode (<see cref="TelnetUpstreamTestCaseReplies"/>): no banner, and every data byte
    /// is echoed as it comes, with no line handling and no <c>quit</c>.
    /// </param>
    public TelnetSession(IExchangeLog log, long maxLineBytes, bool echoesVerbatim = false)
    {
        this.log = log;
        this.maxLineBytes = maxLineBytes;
        this.echoesVerbatim = echoesVerbatim;
        parser = new TelnetInboundParser(maxLineBytes);
        lines = new TelnetLineAssembler(maxLineBytes);
        negotiator.WriteOpening(outbound);
        if (!echoesVerbatim)
        {
            outbound.Write(Banner);
        }
    }

    /// <summary>
    /// Whether the session is over, so the server closes the connection: the client sent
    /// <c>quit</c> and has answered every <c>SEND</c> the server made, or a line or
    /// subnegotiation was too long, or a line was answered with a chosen error.
    /// </summary>
    public bool IsOver => cutShort || (quitReceived && unansweredSends == 0);

    /// <summary>
    /// The line the client completed and the server has not answered yet, or
    /// <see langword="null"/>; while one waits, <see cref="Receive"/> takes no more bytes.
    /// </summary>
    public byte[]? LineAwaitingAnswer { get; private set; }

    /// <summary>
    /// How many lines the client has completed, the one awaiting an answer included.
    /// </summary>
    public int CompletedLineCount { get; private set; }

    /// <summary>
    /// Takes bytes the client sent, stopping as soon as the session is over or a line awaits its
    /// answer (<see cref="LineAwaitingAnswer"/>).
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>How many of <paramref name="bytes"/> were taken.</returns>
    public int Receive(ReadOnlySpan<byte> bytes)
    {
        var index = 0;
        while (index < bytes.Length && !IsOver && LineAwaitingAnswer is null)
        {
            Answer(parser.Take(bytes[index]));
            index++;
        }

        return index;
    }

    /// <summary>
    /// Answers <see cref="LineAwaitingAnswer"/>: with <paramref name="chosenError"/> as one line,
    /// after which the session is over (ADR-0138), or, when it is <see langword="null"/>, as
    /// usual - echoed back, or <c>bye</c> for <c>quit</c>.
    /// </summary>
    /// <param name="chosenError">A chosen-error fault rule's text, or <see langword="null"/>.</param>
    public void AnswerAwaitingLine(string? chosenError)
    {
        var line = LineAwaitingAnswer!;
        LineAwaitingAnswer = null;
        if (chosenError is not null)
        {
            outbound.Write(Encoding.UTF8.GetBytes(chosenError));
            outbound.Write(LineEnding);
            cutShort = true;

            return;
        }

        AnswerLine(line);
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
        if (echoesVerbatim)
        {
            WriteEscapedData([value]);

            return;
        }

        if (quitReceived)
        {
            return;
        }

        switch (lines.Take(value))
        {
            case TelnetLineStatus.Completed:
                LineAwaitingAnswer = lines.CompletedLine;
                CompletedLineCount++;
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
