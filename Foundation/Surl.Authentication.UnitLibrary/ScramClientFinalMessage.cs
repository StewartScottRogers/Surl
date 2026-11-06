using System.Text;

namespace Surl.Authentication;

/// <summary>
/// A SCRAM <c>client-final-message</c> (RFC 5802 section 7):
/// <c>channel-binding "," nonce ["," extensions] "," proof</c>, read as ASCII.
/// </summary>
/// <param name="WithoutProof">The <c>client-final-message-without-proof</c>, the end of the <c>AuthMessage</c>.</param>
/// <param name="ChannelBinding">The <c>c=</c> value decoded from base64: the GS2 header again, since no channel binding is supported.</param>
/// <param name="Nonce">The <c>r=</c> nonce: the client's nonce then the server's.</param>
/// <param name="Proof">The <c>p=</c> <c>ClientProof</c> decoded from base64.</param>
internal sealed record ScramClientFinalMessage(string WithoutProof, byte[] ChannelBinding, string Nonce, byte[] Proof)
{
    /// <summary>
    /// Reads <paramref name="message"/>.
    /// </summary>
    /// <param name="message">The client's response, as sent.</param>
    /// <returns>The message, or <see langword="null"/> when it does not follow the syntax.</returns>
    public static ScramClientFinalMessage? Read(ReadOnlySpan<byte> message) =>
        Ascii.IsValid(message) ? Read(Encoding.ASCII.GetString(message)) : null;

    private static ScramClientFinalMessage? Read(string text)
    {
        var proofStart = text.LastIndexOf(",p=", StringComparison.Ordinal);
        var parts = text.Split(',');
        var channelBinding = ReadBase64Attribute(parts[0], "c=");
        var nonce = parts.Length > 2 ? ScramClientFirstMessage.ReadNonce(parts[1]) : null;
        var proof = proofStart > 0 ? ReadBase64Attribute(text[(proofStart + 1)..], "p=") : null;

        return channelBinding is null || nonce is null || proof is null
            ? null
            : new ScramClientFinalMessage(text[..proofStart], channelBinding, nonce, proof);
    }

    private static byte[]? ReadBase64Attribute(string part, string prefix) =>
        part.StartsWith(prefix, StringComparison.Ordinal) ? ReadBase64(part[prefix.Length..]) : null;

    private static byte[]? ReadBase64(string value)
    {
        var bytes = new byte[value.Length];

        return !value.Any(char.IsWhiteSpace) && Convert.TryFromBase64String(value, bytes, out var length) ? bytes[..length] : null;
    }
}
