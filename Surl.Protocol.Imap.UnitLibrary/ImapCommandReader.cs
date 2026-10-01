using Surl.LineProtocol;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Imap;

/// <summary>
/// Reads one IMAP command at a time: its first line and, for each synchronizing literal it
/// announces, the <c>+</c> continuation, the literal's bytes and the line after them (ADR-0055,
/// decision 9).
/// </summary>
/// <remarks>
/// The whole command, every line with its CRLF and every literal, is bounded by
/// <see cref="ExchangeLimits.MaxLineBytes"/> (0 means no limit): a literal that would pass it is
/// refused before its continuation is sent. Each line is under the line reader's head timeout,
/// and each literal under a head timeout of its own.
/// </remarks>
internal sealed class ImapCommandReader
{
    private readonly CrlfLineReader lineReader;
    private readonly IConnection connection;
    private readonly ExchangeLimits limits;
    private readonly TimeProvider timeProvider;

    public ImapCommandReader(CrlfLineReader lineReader, IConnection connection, ExchangeLimits limits, TimeProvider timeProvider)
    {
        this.lineReader = lineReader;
        this.connection = connection;
        this.limits = limits;
        this.timeProvider = timeProvider;
    }

    /// <summary>
    /// Reads the next command.
    /// </summary>
    /// <param name="cancellationToken">The exchange's cancellation.</param>
    /// <returns>The command, or the named reason there is none.</returns>
    public async ValueTask<ImapCommandReadResult> ReadCommandAsync(CancellationToken cancellationToken)
    {
        var command = new PendingCommand(this);
        while (true)
        {
            var read = await lineReader.ReadLineAsync(cancellationToken);
            if ((command.AddLine(read) ?? command.AppendMessage()) is { } ended)
            {
                return ended;
            }

            var (outcome, literal) = await ReadLiteralAsync(command.Literal!, command.Bytes, cancellationToken);
            if (command.AddLiteral(outcome, literal) is { } failed)
            {
                return failed;
            }
        }
    }

    // The lines and literals of the command being read, and what it has cost so far.
    private sealed class PendingCommand(ImapCommandReader reader)
    {
        private readonly List<byte[]> lines = [];
        private readonly List<byte[]> literals = [];
        private string? tag;

        public long Bytes { get; private set; }

        public ImapLiteralMarker? Literal { get; private set; }

        // The command's end, or null when the line announces a literal to read next.
        public ImapCommandReadResult? AddLine(CrlfLineReadResult read)
        {
            if (read.Line is not { } line)
            {
                return End(ToCommandOutcome(read.Outcome));
            }

            tag = lines.Count == 0 ? ImapArguments.ReadTagOf(line) : tag;
            Bytes += line.Length + 2;

            // A line whose tag is invalid is answered at once, so no continuation is sent for it.
            Literal = tag is null ? null : ImapLiteralMarker.Find(line);
            return reader.IsPastLineLimit(Bytes) ? End(ImapCommandReadOutcome.LineTooLong) : AddCommandLine(line);
        }

        private ImapCommandReadResult? AddCommandLine(byte[] line)
        {
            lines.Add(Literal is null ? line : line[..Literal.Start]);
            return Literal is null ? new ImapCommandReadResult(ImapCommandReadOutcome.CommandRead, tag, new ImapCommandText(lines, literals)) : null;
        }

        // The command so far when the literal it announces is APPEND's message, which is not
        // read here; otherwise null.
        public ImapCommandReadResult? AppendMessage()
        {
            var text = new ImapCommandText(lines, literals);
            return !Literal!.IsNonSynchronizing && ImapAppendRequest.IsMessageNext(text)
                ? new ImapCommandReadResult(ImapCommandReadOutcome.AppendMessage, tag, text, Literal.Length)
                : null;
        }

        // Null when the literal was read; otherwise the command's end.
        public ImapCommandReadResult? AddLiteral(ImapCommandReadOutcome outcome, byte[] literal)
        {
            literals.Add(literal);
            Bytes += literal.Length;
            return outcome == ImapCommandReadOutcome.CommandRead ? null : End(outcome);
        }

        private ImapCommandReadResult End(ImapCommandReadOutcome outcome) => new(outcome, tag, null);
    }

    /// <summary>
    /// Reads <c>APPEND</c>'s message once the session has accepted it: sends the <c>+</c>
    /// continuation, copies the literal's bytes to <paramref name="destination"/> under the idle
    /// timeout alone (ADR-0055, decision 9), then reads the rest of the command's line.
    /// </summary>
    /// <param name="length">The bytes the literal announced.</param>
    /// <param name="destination">Where the message goes.</param>
    /// <param name="cancellationToken">The exchange's cancellation.</param>
    /// <returns>How the read ended, with the rest of the line as <see cref="ImapCommandText"/>
    /// when it was read.</returns>
    public async ValueTask<ImapCommandReadResult> ReadAppendMessageAsync(long length, Stream destination, CancellationToken cancellationToken)
    {
        await ReplyLineWriter.WriteAsync(connection, ImapResponses.ReadyForLiteral, cancellationToken);
        if (!await lineReader.ReadCountedRunAsync(length, destination, cancellationToken))
        {
            return new ImapCommandReadResult(ImapCommandReadOutcome.Closed, null, null);
        }

        var read = await lineReader.ReadLineAsync(cancellationToken);
        return read.Line is { } line
            ? new ImapCommandReadResult(ImapCommandReadOutcome.CommandRead, null, new ImapCommandText([line], []))
            : new ImapCommandReadResult(ToCommandOutcome(read.Outcome), null, null);
    }

    private static ImapCommandReadOutcome ToCommandOutcome(CrlfLineReadOutcome outcome) => outcome switch
    {
        CrlfLineReadOutcome.LineTooLong => ImapCommandReadOutcome.LineTooLong,
        CrlfLineReadOutcome.HeadTimedOut => ImapCommandReadOutcome.HeadTimedOut,
        _ => ImapCommandReadOutcome.Closed,
    };

    private bool IsPastLineLimit(long commandBytes) => limits.MaxLineBytes > 0 && commandBytes > limits.MaxLineBytes;

    // A literal that may not be sent is refused before its "+" continuation, so the client sends
    // none of its bytes (RFC 3501, section 7.5).
    private async ValueTask<(ImapCommandReadOutcome Outcome, byte[] Literal)> ReadLiteralAsync(ImapLiteralMarker marker, long commandBytes, CancellationToken cancellationToken)
    {
        if (marker.IsNonSynchronizing)
        {
            return (ImapCommandReadOutcome.NonSynchronizingLiteral, []);
        }

        if (marker.Length > Array.MaxLength || IsPastLineLimit(commandBytes + marker.Length))
        {
            return (ImapCommandReadOutcome.LiteralTooLong, []);
        }

        await ReplyLineWriter.WriteAsync(connection, ImapResponses.ReadyForLiteral, cancellationToken);
        return await ReadLiteralBytesAsync(marker.Length, cancellationToken);
    }

    private async ValueTask<(ImapCommandReadOutcome Outcome, byte[] Literal)> ReadLiteralBytesAsync(long length, CancellationToken cancellationToken)
    {
        using var headTimeout = new CancellationTokenSource(limits.HeadTimeout, timeProvider);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, headTimeout.Token);
        using var literal = new MemoryStream();
        try
        {
            return await lineReader.ReadCountedRunAsync(length, literal, cancellation.Token)
                ? (ImapCommandReadOutcome.CommandRead, literal.ToArray())
                : (ImapCommandReadOutcome.Closed, []);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (ImapCommandReadOutcome.HeadTimedOut, []);
        }
    }
}
