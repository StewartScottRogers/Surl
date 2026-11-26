using System.Buffers;
using System.Runtime.InteropServices;

namespace Surl.Protocol.Abstractions;

/// <summary>
/// An <see cref="IReceivedBytesRedactor"/> for protocols whose client sends lines (FTP, SMTP,
/// IMAP, POP3, NATS). It holds a line back until its line end arrives, hands the whole line to an
/// <see cref="ILineRedactionRule"/> and writes what the rule returns followed by the line end as
/// it arrived (ADR-0182, decision 6).
/// </summary>
/// <remarks>
/// A line ends at a CR LF. A redactor created to end a line at a bare line feed too, for a server
/// that reads lines so (FTP, NATS), ends one at any line feed (ADR-0182, amendment 31). It never
/// holds more than <see cref="MaxHeldBytes"/>. A line that grows past that with no line end is
/// written as <see cref="SecretMask"/> and the rest of that line is dropped up to its line end,
/// which is written as it arrived.
/// </remarks>
public sealed class LineBytesRedactor : IReceivedBytesRedactor
{
    private const byte CarriageReturn = 0x0D;
    private const byte LineFeed = 0x0A;

    /// <summary>
    /// The most bytes held back when none is given: 64 KiB (ADR-0182, decision 6).
    /// </summary>
    public const int DefaultMaxHeldBytes = 64 * 1024;

    private static ReadOnlySpan<byte> CrLfLineEnd => "\r\n"u8;

    private static ReadOnlySpan<byte> BareLineEnd => "\n"u8;

    private readonly ILineRedactionRule _rule;
    private readonly bool _endsLineAtBareLineFeed;
    private readonly List<byte> _held = [];
    private bool _discardingToLineEnd;
    private bool _previousWasCarriageReturn;

    /// <summary>
    /// What replaces a secret: four asterisks (ADR-0182, decision 1).
    /// </summary>
    public static ReadOnlySpan<byte> SecretMask => "****"u8;

    /// <summary>
    /// Creates a redactor for one exchange.
    /// </summary>
    /// <param name="rule">Decides what each whole line may show.</param>
    /// <param name="maxHeldBytes">The most bytes held back while waiting for a line end; at least 1.</param>
    /// <param name="endsLineAtBareLineFeed">Whether a line feed with no carriage return before it ends a line too, as the server's line reader reads it; if not, only a CR LF does.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rule"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxHeldBytes"/> is below 1.</exception>
    public LineBytesRedactor(ILineRedactionRule rule, int maxHeldBytes = DefaultMaxHeldBytes, bool endsLineAtBareLineFeed = false)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxHeldBytes, 1);
        _rule = rule;
        MaxHeldBytes = maxHeldBytes;
        _endsLineAtBareLineFeed = endsLineAtBareLineFeed;
    }

    /// <summary>
    /// The most bytes this redactor holds back.
    /// </summary>
    public int MaxHeldBytes { get; }

    /// <summary>
    /// How many bytes are held back now: the unfinished line.
    /// </summary>
    public int HeldByteCount => _held.Count;

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        foreach (byte next in received)
        {
            if (_discardingToLineEnd)
            {
                DropToLineEnd(next, output);
            }
            else
            {
                HoldOrFinishLine(next, output);
            }
        }
    }

    /// <inheritdoc />
    public void ObserveSent(ReadOnlySpan<byte> sent) => _rule.ObserveSent(sent);

    /// <inheritdoc />
    public void Flush(ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (_held.Count > 0)
        {
            _rule.Redact(CollectionsMarshal.AsSpan(_held), output);
        }

        _held.Clear();
        _discardingToLineEnd = false;
        _previousWasCarriageReturn = false;
    }

    private void DropToLineEnd(byte next, ArrayBufferWriter<byte> output)
    {
        if (EndsLine(next, _previousWasCarriageReturn))
        {
            output.Write(_previousWasCarriageReturn ? CrLfLineEnd : BareLineEnd);
            _discardingToLineEnd = false;
        }

        _previousWasCarriageReturn = next == CarriageReturn;
    }

    private void HoldOrFinishLine(byte next, ArrayBufferWriter<byte> output)
    {
        bool afterCarriageReturn = _held.Count > 0 && _held[^1] == CarriageReturn;
        _held.Add(next);
        if (EndsLine(next, afterCarriageReturn))
        {
            int lineEndLength = afterCarriageReturn ? CrLfLineEnd.Length : BareLineEnd.Length;
            ReadOnlySpan<byte> held = CollectionsMarshal.AsSpan(_held);
            _rule.Redact(held[..^lineEndLength], output);
            output.Write(held[^lineEndLength..]);
            _held.Clear();
        }
        else if (_held.Count > MaxHeldBytes)
        {
            output.Write(SecretMask);
            _held.Clear();
            _discardingToLineEnd = true;
            _previousWasCarriageReturn = next == CarriageReturn;
        }
    }

    private bool EndsLine(byte next, bool afterCarriageReturn) =>
        next == LineFeed && (afterCarriageReturn || _endsLineAtBareLineFeed);
}
