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
/// The words of a client line are separated by runs of spaces and tabs. When a tab or a quote
/// (<c>"</c> or <c>'</c>) is on a line that holds an initial response, it may have split an argument
/// in two (Redis's inline <c>AUTH user "a password"</c> in a tunnel), so nothing after the first mask
/// is written (ADR-0183, amendments 4 and 7).
/// </para>
/// <para>
/// When the first word is a command word and the second is <c>AUTH</c> or <c>AUTHENTICATE</c>, the
/// first may be the command or an IMAP tag, so the response of both readings is withheld: the third
/// and the fourth word.
/// </para>
/// </remarks>
public sealed class SaslLineRedactionRule : ILineRedactionRule
{
    private const byte CarriageReturn = 0x0D;
    private const byte LineFeed = 0x0A;
    private const int InitialResponseWordOffset = 2;
    private const int MostWordsRead = 4;

    private static ReadOnlySpan<byte> WordSeparators => " \t"u8;

    // A tab separates words and a quote joins them, so either may leave a secret's bytes outside its word.
    private static ReadOnlySpan<byte> ArgumentSplitters => "\t\"'"u8;

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
            output.Write(line.IsEmpty || line.SequenceEqual("*"u8) ? line : LineBytesRedactor.SecretMask);
        }
        else
        {
            RedactInitialResponse(line, output);
        }
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

    private void RedactInitialResponse(ReadOnlySpan<byte> line, ArrayBufferWriter<byte> output)
    {
        // The words of [tag] AUTH mechanism initial-response, however many spaces and tabs separate them.
        Span<Range> words = stackalloc Range[MostWordsRead];
        var count = SplitIntoWords(line, words);
        ReadOnlySpan<Range> found = words[..count];
        WriteWithWordsMasked(line, found[FindInitialResponseWords(line, found)], output);
    }

    // Which of the words may be an initial response: none, one, or one for each reading of the line.
    private Range FindInitialResponseWords(ReadOnlySpan<byte> line, ReadOnlySpan<Range> words)
    {
        for (var commandIndex = 0; commandIndex <= 1; commandIndex++)
        {
            var responseIndex = commandIndex + InitialResponseWordOffset;
            if (responseIndex < words.Length && IsCommandWord(line[words[commandIndex]]))
            {
                // A second word that starts SASL too may be the command, and the first word its tag.
                var mayBeTagged = commandIndex == 0 && IsSaslStartWord(line[words[1]]);
                return responseIndex..(mayBeTagged ? words.Length : responseIndex + 1);
            }
        }

        return default;
    }

    private static void WriteWithWordsMasked(
        ReadOnlySpan<byte> line, ReadOnlySpan<Range> maskedWords, ArrayBufferWriter<byte> output)
    {
        var written = 0;
        foreach (var word in maskedWords)
        {
            output.Write(line[written..word.Start.Value]);
            output.Write(LineBytesRedactor.SecretMask);
            written = word.End.Value;
            if (line.ContainsAny(ArgumentSplitters))
            {
                return;
            }
        }

        output.Write(line[written..]);
    }

    // Either word, whatever this rule's command words are, so that every rule reads such a line alike.
    private static bool IsSaslStartWord(ReadOnlySpan<byte> word) =>
        Ascii.EqualsIgnoreCase(word, "AUTH"u8) || Ascii.EqualsIgnoreCase(word, "AUTHENTICATE"u8);

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
