using System.Buffers;
using Surl.LineProtocol;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// The <see cref="ILineRedactionRule"/> for the lines of a text protocol carried through a
/// <c>CONNECT</c> tunnel. <c>CONNECT</c> does not say which protocol that is, so every line is judged
/// by all of these, and what the one that withholds from earliest in the line writes is logged
/// (ADR-0182, amendment 16): <see cref="HttpProxyTunnelPasswordAndCredentialHeaderLineRule"/>
/// (<c>PASS</c>, <c>ACCT</c> and the credential headers),
/// <see cref="HttpProxyTunnelImapLoginLineRule"/> (the IMAP <c>LOGIN</c> password) and two
/// <see cref="SaslLineRedactionRule"/>s, for SMTP's <c>334 </c> continuation and for the <c>+ </c>
/// continuation of IMAP and POP3. A line none of them changes is written as received.
/// </summary>
/// <remarks>
/// A line may end with the line feed that ended it, when no carriage return came before it; the line
/// feed is kept and the rest is judged. A server line split between the origin's reads is still
/// recognised as a continuation, because its first bytes are kept until it ends. A server's bare
/// <c>+</c> or <c>334</c> line, with no space after it, is a continuation too.
/// </remarks>
internal sealed class HttpProxyTunnelLineRedactionRule : ILineRedactionRule
{
    private const byte LineFeed = (byte)'\n';

    // As long as the longest continuation prefix, "334 ": all a SASL rule reads of a server's line.
    private const int ContinuationPrefixLength = 4;

    private readonly HttpProxyTunnelImapLoginLineRule imapLogin = new();
    private readonly ILineRedactionRule[] rules;

    private byte[] unfinishedOriginLineStart = [];

    public HttpProxyTunnelLineRedactionRule() => rules =
    [
        new HttpProxyTunnelPasswordAndCredentialHeaderLineRule(),
        imapLogin,
        new SaslLineRedactionRule(["AUTH"], "334 "),
        new SaslLineRedactionRule(["AUTH", "AUTHENTICATE"], "+ "),
    ];

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> line, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var endsWithLineFeed = line.EndsWith([LineFeed]);
        imapLogin.LineEndLength = endsWithLineFeed ? 1 : 2;
        RedactWithoutLineFeed(endsWithLineFeed ? line[..^1] : line, output);
        if (endsWithLineFeed)
        {
            output.Write([LineFeed]);
        }
    }

    /// <inheritdoc />
    public void ObserveSent(ReadOnlySpan<byte> sent)
    {
        byte[] joined = [.. unfinishedOriginLineStart, .. sent];
        ShowEveryRule(joined);
        if (IsEndedByBareContinuation(joined, out var continuation))
        {
            // The SASL rules look for the prefix with its space, which some servers leave out.
            ShowEveryRule([.. continuation, (byte)' ']);
        }

        ReadOnlySpan<byte> unfinished = joined.AsSpan(joined.AsSpan().LastIndexOf(LineFeed) + 1);
        unfinishedOriginLineStart = unfinished[..Math.Min(unfinished.Length, ContinuationPrefixLength)].ToArray();
    }

    // Whether the last line the server finished is "+" or "334" and nothing else.
    private static bool IsEndedByBareContinuation(ReadOnlySpan<byte> sent, out ReadOnlySpan<byte> continuation)
    {
        var lines = sent.TrimEnd("\r\n"u8);
        continuation = lines[(lines.LastIndexOf(LineFeed) + 1)..];

        return sent.EndsWith([LineFeed]) && (continuation.SequenceEqual("+"u8) || continuation.SequenceEqual("334"u8));
    }

    private void ShowEveryRule(ReadOnlySpan<byte> sent)
    {
        foreach (var rule in rules)
        {
            rule.ObserveSent(sent);
        }
    }

    // Every rule sees every line, because each keeps state from one line to the next.
    private void RedactWithoutLineFeed(ReadOnlySpan<byte> line, ArrayBufferWriter<byte> output)
    {
        var logged = line;
        var shownLength = int.MaxValue;
        foreach (var rule in rules)
        {
            var judged = new ArrayBufferWriter<byte>();
            rule.Redact(line, judged);
            var unchangedLength = line.CommonPrefixLength(judged.WrittenSpan);
            if (!judged.WrittenSpan.SequenceEqual(line) && unchangedLength < shownLength)
            {
                logged = judged.WrittenSpan;
                shownLength = unchangedLength;
            }
        }

        output.Write(logged);
    }
}
