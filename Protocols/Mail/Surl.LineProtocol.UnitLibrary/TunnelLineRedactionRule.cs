using System.Buffers;
using Surl.Protocol.Abstractions;

namespace Surl.LineProtocol;

/// <summary>
/// The <see cref="ILineRedactionRule"/> the SOCKS server and the HTTP proxy share for the lines of a
/// text protocol carried through a tunnel. Neither SOCKS nor <c>CONNECT</c> says which protocol that
/// is, so every line is judged by all of these, and what the one that withholds from earliest in the
/// line writes is logged (ADR-0182, amendments 9, 16 and 20):
/// <see cref="TunnelPasswordAndCredentialHeaderLineRule"/> (<c>PASS</c>, <c>ACCT</c>, the credential
/// headers and the API-key and session-token headers), <see cref="TunnelImapLoginLineRule"/> (the
/// IMAP <c>LOGIN</c> password), two <see cref="SaslLineRedactionRule"/>s, for SMTP's <c>334 </c>
/// continuation and for the <c>+ </c> continuation of IMAP and POP3, and
/// <see cref="TunnelRedisAuthLineRule"/> (an inline Redis <c>AUTH</c> line with one argument, a quoted
/// command word or a vertical tab, form feed or carriage return, and <c>HELLO ... AUTH</c>; ADR-0183,
/// amendments 8 and 9). A line none of them changes is written as received.
/// </summary>
/// <remarks>
/// A line may end with the line feed that ended it, when no carriage return came before it; the line
/// feed is kept and the rest is judged. A server line split between the origin's reads is still
/// recognised as a continuation, because its first bytes are kept until it ends. A server's bare
/// <c>+</c> or <c>334</c> line, with no space after it, is a continuation too. One instance serves one
/// tunnel.
/// </remarks>
public sealed class TunnelLineRedactionRule : ILineRedactionRule
{
    private const byte LineFeed = (byte)'\n';

    // As long as the longest continuation prefix, "334 ": all a SASL rule reads of a server's line.
    private const int ContinuationPrefixLength = 4;

    private readonly TunnelImapLoginLineRule _imapLogin = new();
    private readonly ILineRedactionRule[] _rules;

    private byte[] _unfinishedOriginLineStart = [];

    /// <summary>
    /// Creates a rule for one tunnel.
    /// </summary>
    public TunnelLineRedactionRule() => _rules =
    [
        new TunnelPasswordAndCredentialHeaderLineRule(),
        _imapLogin,
        new SaslLineRedactionRule(["AUTH"], "334 "),
        new SaslLineRedactionRule(["AUTH", "AUTHENTICATE"], "+ "),
        new TunnelRedisAuthLineRule(),
    ];

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> line, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var endsWithLineFeed = line.EndsWith([LineFeed]);
        _imapLogin.LineEndLength = endsWithLineFeed ? 1 : 2;
        RedactWithoutLineFeed(endsWithLineFeed ? line[..^1] : line, output);
        if (endsWithLineFeed)
        {
            output.Write([LineFeed]);
        }
    }

    /// <inheritdoc />
    public void ObserveSent(ReadOnlySpan<byte> sent)
    {
        byte[] joined = [.. _unfinishedOriginLineStart, .. sent];
        ShowEveryRule(joined);
        if (IsEndedByBareContinuation(joined, out var continuation))
        {
            // The SASL rules look for the prefix with its space, which some servers leave out.
            ShowEveryRule([.. continuation, (byte)' ']);
        }

        ReadOnlySpan<byte> unfinished = joined.AsSpan(joined.AsSpan().LastIndexOf(LineFeed) + 1);
        _unfinishedOriginLineStart = unfinished[..Math.Min(unfinished.Length, ContinuationPrefixLength)].ToArray();
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
        foreach (var rule in _rules)
        {
            rule.ObserveSent(sent);
        }
    }

    // Every rule sees every line, because each keeps state from one line to the next.
    private void RedactWithoutLineFeed(ReadOnlySpan<byte> line, ArrayBufferWriter<byte> output)
    {
        var logged = line;
        var shownLength = int.MaxValue;
        foreach (var rule in _rules)
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
