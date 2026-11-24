using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Xmpp;

/// <summary>
/// The <see cref="IReceivedBytesRedactor"/> of the XMPP client's bytes: the text of an
/// <c>&lt;auth mechanism="PLAIN"&gt;</c> element, and of the <c>&lt;response&gt;</c> that answers a PLAIN
/// <c>&lt;auth/&gt;</c> sent with no initial response, is written as <see cref="LineBytesRedactor.SecretMask"/>;
/// every other byte is logged as received (ADR-0182, decisions 1 and 6; ADR-0183, decision 1).
/// </summary>
/// <remarks>
/// The base64 text holds the authorization identity, the user name and the password in one value, so all of it is
/// replaced, as the line protocols' SASL initial responses are; the user name stays visible in the server's own
/// login notes and on the event stream. A tag is held back until its closing <c>&gt;</c> (quotes respected), so a
/// mechanism split across reads is still seen; the text of a matched element is never held, only counted, until the
/// <c>&lt;/</c> that ends it. A tag past <see cref="MaxHeldBytes"/> is written as it arrived and read no further.
/// SCRAM, ANONYMOUS and every other mechanism carry no recoverable password, so their elements are logged as
/// received (ADR-0182, decision 8).
/// </remarks>
internal sealed class XmppSaslPlainRedactor : IReceivedBytesRedactor
{
    /// <summary>The most bytes held back while waiting for the end of a tag: 64 KiB, as <see cref="LineBytesRedactor"/>.</summary>
    public const int MaxHeldBytes = LineBytesRedactor.DefaultMaxHeldBytes;

    private readonly List<byte> heldTag = [];
    private Stage stage = Stage.Text;
    private char openQuote;
    private bool plainAuthAwaitsResponse;
    private int maskedTextBytes;
    private bool previousWasLessThan;

    private enum Stage
    {
        Text,
        Tag,
        MaskedText,
    }

    /// <inheritdoc/>
    public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        foreach (var next in received)
        {
            Take(next, output);
        }
    }

    /// <inheritdoc/>
    public void ObserveSent(ReadOnlySpan<byte> sent)
    {
        // The password is only ever in the client's element, so what the server sent changes nothing.
    }

    /// <inheritdoc/>
    public void Flush(ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (stage == Stage.MaskedText)
        {
            WriteMaskIfTextSeen(output, maskedTextBytes);
        }

        output.Write(CollectionsMarshal.AsSpan(heldTag));
        heldTag.Clear();
        stage = Stage.Text;
    }

    private void Take(byte next, ArrayBufferWriter<byte> output)
    {
        if (stage == Stage.MaskedText)
        {
            TakeMaskedText(next, output);
        }
        else if (stage == Stage.Tag)
        {
            TakeTag(next, output);
        }
        else if (next == (byte)'<')
        {
            heldTag.Add(next);
            stage = Stage.Tag;
        }
        else
        {
            output.Write([next]);
        }
    }

    // Everything up to the "</" that ends the element is the secret, markup-looking or not (CDATA included).
    private void TakeMaskedText(byte next, ArrayBufferWriter<byte> output)
    {
        maskedTextBytes++;
        if (previousWasLessThan && next == (byte)'/')
        {
            WriteMaskIfTextSeen(output, maskedTextBytes - 2);
            heldTag.AddRange("</"u8);
            stage = Stage.Tag;
        }

        previousWasLessThan = next == (byte)'<';
    }

    private void TakeTag(byte next, ArrayBufferWriter<byte> output)
    {
        heldTag.Add(next);
        var character = (char)next;
        if (openQuote != '\0')
        {
            openQuote = character == openQuote ? '\0' : openQuote;
        }
        else if (character is '"' or '\'')
        {
            openQuote = character;
        }
        else if (character == '>')
        {
            FinishTag(output);
        }
        else if (heldTag.Count > MaxHeldBytes)
        {
            WriteHeldTag(output);
        }
    }

    private void WriteHeldTag(ArrayBufferWriter<byte> output)
    {
        output.Write(CollectionsMarshal.AsSpan(heldTag));
        heldTag.Clear();
        openQuote = '\0';
        stage = Stage.Text;
    }

    private void FinishTag(ArrayBufferWriter<byte> output)
    {
        var tag = Encoding.UTF8.GetString(CollectionsMarshal.AsSpan(heldTag));
        WriteHeldTag(output);
        var hasText = !tag.EndsWith("/>", StringComparison.Ordinal);
        var name = LocalName(tag);
        var isPlainAuth = name == "auth" && MechanismIsPlain(tag);
        var isAwaitedResponse = name == "response" && plainAuthAwaitsResponse;
        if (name is "auth" or "response")
        {
            plainAuthAwaitsResponse = isPlainAuth && !hasText;
        }

        if ((isPlainAuth || isAwaitedResponse) && hasText)
        {
            stage = Stage.MaskedText;
            previousWasLessThan = false;
            maskedTextBytes = 0;
        }
    }

    private void WriteMaskIfTextSeen(ArrayBufferWriter<byte> output, int textBytes)
    {
        if (textBytes > 0)
        {
            output.Write(LineBytesRedactor.SecretMask);
        }

        maskedTextBytes = 0;
    }

    // "<sasl:auth mechanism='PLAIN'>" is "auth": the part after any prefix, up to the first space, slash or bracket.
    private static string LocalName(string tag)
    {
        var end = tag.AsSpan(1).IndexOfAny(" \t\r\n/>") + 1;
        var name = tag.AsSpan(1, end - 1);
        return name[(name.LastIndexOf(':') + 1)..].ToString();
    }

    private static bool MechanismIsPlain(string tag)
    {
        var at = tag.IndexOf("mechanism", StringComparison.Ordinal);
        if (at < 0)
        {
            return false;
        }

        var rest = tag.AsSpan(at + "mechanism".Length).TrimStart();
        // A held tag always ends in '>', so something follows "mechanism"; StartsWith needs no empty-span branch.
        if (!rest.StartsWith("=", StringComparison.Ordinal))
        {
            return false;
        }

        rest = rest[1..].TrimStart();
        return rest.StartsWith("\"PLAIN\"", StringComparison.Ordinal) || rest.StartsWith("'PLAIN'", StringComparison.Ordinal);
    }
}
