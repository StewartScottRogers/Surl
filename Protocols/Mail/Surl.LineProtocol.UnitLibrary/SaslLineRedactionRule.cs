using System.Buffers;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.LineProtocol;

/// <summary>
/// The <see cref="ILineRedactionRule"/> the SMTP, IMAP and POP3 servers share: it withholds a SASL
/// initial response from the <c>AUTH</c> or <c>AUTHENTICATE</c> line and every client line that
/// answers a server continuation, because SASL PLAIN and LOGIN carry the password as base64
/// (ADR-0183, decisions 1 to 4).
/// </summary>
/// <remarks>
/// One instance serves one exchange. The server's continuation prefix (<c>334 </c> in SMTP, <c>+ </c>
/// in IMAP and POP3) seen through <see cref="ObserveSent"/> marks the client's next line a secret;
/// any other line the server sends clears the mark, so a client line sent with no continuation
/// pending is logged unchanged. A lone <c>*</c> (the cancel line) and an empty line hold no secret
/// and are logged as they are.
/// <para>
/// The words of a client line are separated by runs of spaces and tabs. When a tab is on a line that
/// holds an initial response, a tab may have split the mechanism or the response in two, so nothing
/// after the mask is written.
/// </para>
/// </remarks>
public sealed class SaslLineRedactionRule : ILineRedactionRule
{
    private const byte Tab = (byte)'\t';
    private const byte CarriageReturn = 0x0D;
    private const byte LineFeed = 0x0A;
    private const int InitialResponseWordOffset = 2;
    private const int MostWordsRead = 4;

    private static ReadOnlySpan<byte> WordSeparators => " \t"u8;

    private readonly byte[][] _commandWords;
    private readonly byte[] _continuationPrefix;
    private bool _continuationPending;

    /// <summary>
    /// Creates a rule for one exchange.
    /// </summary>
    /// <param name="commandWords">The words that start SASL on a client line, such as <c>AUTH</c> or <c>AUTHENTICATE</c>; matched without regard to case, as the line's first word or, after an IMAP tag, its second.</param>
    /// <param name="continuationPrefix">What a server continuation line starts with, such as <c>334 </c> or <c>+ </c>; not empty.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="continuationPrefix"/> is empty.</exception>
    public SaslLineRedactionRule(IReadOnlyCollection<string> commandWords, string continuationPrefix)
    {
        ArgumentNullException.ThrowIfNull(commandWords);
        ArgumentException.ThrowIfNullOrEmpty(continuationPrefix);
        _commandWords = [.. commandWords.Select(word => Encoding.ASCII.GetBytes(word))];
        _continuationPrefix = Encoding.ASCII.GetBytes(continuationPrefix);
    }

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> line, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (_continuationPending)
        {
            _continuationPending = false;
            if (!line.IsEmpty && !line.SequenceEqual("*"u8))
            {
                output.Write(LineBytesRedactor.SecretMask);
                return;
            }
        }
        else if (TryFindInitialResponse(line, out var start, out var end))
        {
            output.Write(line[..start]);
            output.Write(LineBytesRedactor.SecretMask);
            output.Write(line[end..]);
            return;
        }

        output.Write(line);
    }

    /// <inheritdoc />
    public void ObserveSent(ReadOnlySpan<byte> sent)
    {
        while (!sent.IsEmpty)
        {
            var lineFeedIndex = sent.IndexOf(LineFeed);
            var line = lineFeedIndex < 0 ? sent : sent[..lineFeedIndex];
            sent = lineFeedIndex < 0 ? default : sent[(lineFeedIndex + 1)..];
            line = line.TrimEnd(CarriageReturn);
            if (!line.IsEmpty)
            {
                _continuationPending = line.StartsWith(_continuationPrefix);
            }
        }
    }

    private bool TryFindInitialResponse(ReadOnlySpan<byte> line, out int start, out int end)
    {
        // The words of [tag] AUTH mechanism initial-response, however many spaces and tabs separate them.
        Span<Range> words = stackalloc Range[MostWordsRead];
        var count = SplitIntoWords(line, words);
        start = 0;
        end = 0;
        for (var commandIndex = 0; commandIndex <= 1; commandIndex++)
        {
            var responseIndex = commandIndex + InitialResponseWordOffset;
            if (responseIndex < count && IsCommandWord(line[words[commandIndex]]))
            {
                start = words[responseIndex].Start.Value;
                end = line.Contains(Tab) ? line.Length : words[responseIndex].End.Value;
                return true;
            }
        }

        return false;
    }

    private bool IsCommandWord(ReadOnlySpan<byte> word)
    {
        foreach (var commandWord in _commandWords)
        {
            if (Ascii.EqualsIgnoreCase(word, commandWord))
            {
                return true;
            }
        }

        return false;
    }

    // Fills words with the first non-empty runs of bytes that are neither spaces nor tabs.
    private static int SplitIntoWords(ReadOnlySpan<byte> line, Span<Range> words)
    {
        var count = 0;
        var wordStart = 0;
        for (var index = 0; index <= line.Length && count < words.Length; index++)
        {
            if (index == line.Length || WordSeparators.Contains(line[index]))
            {
                if (index > wordStart)
                {
                    words[count++] = wordStart..index;
                }

                wordStart = index + 1;
            }
        }

        return count;
    }
}
