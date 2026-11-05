using System.Text;
using System.Text.Unicode;

namespace Surl.Authentication;

/// <summary>
/// A SCRAM <c>client-first-message</c> (RFC 5802 section 7):
/// <c>gs2-cbind-flag "," [authzid] "," [reserved-mext ","] username "," nonce ["," extensions]</c>,
/// read as UTF-8. Surl offers no <c>-PLUS</c> mechanism, so it supports no channel binding: a
/// flag of <c>n</c> or <c>y</c> is accepted, and <c>p=</c> is refused with
/// <c>channel-binding-not-supported</c> (RFC 5802 sections 6 and 7).
/// </summary>
/// <param name="Gs2Header">The GS2 header, both commas included: what <c>c=</c> must carry back, base64-encoded.</param>
/// <param name="Bare">The <c>client-first-message-bare</c>: everything after the GS2 header.</param>
/// <param name="UserName">The <c>n=</c> user name, its <c>=2C</c> and <c>=3D</c> decoded.</param>
/// <param name="AuthorizationId">The <c>a=</c> identity decoded, or <see langword="null"/> when none was sent.</param>
/// <param name="ClientNonce">The <c>r=</c> nonce.</param>
internal sealed record ScramClientFirstMessage(
    string Gs2Header, string Bare, string UserName, string? AuthorizationId, string ClientNonce)
{
    /// <summary>
    /// The <c>server-error</c> for a message that does not follow the syntax.
    /// </summary>
    public const string InvalidEncoding = "invalid-encoding";

    /// <summary>
    /// The <c>server-error</c> for <c>p=</c>, since no <c>-PLUS</c> mechanism is offered.
    /// </summary>
    public const string ChannelBindingNotSupported = "channel-binding-not-supported";

    /// <summary>
    /// The <c>server-error</c> for a mandatory extension (<c>m=</c>), of which none is defined.
    /// </summary>
    public const string ExtensionsNotSupported = "extensions-not-supported";

    /// <summary>
    /// Reads <paramref name="message"/>.
    /// </summary>
    /// <param name="message">The client's response, as sent.</param>
    /// <returns>The message, or the <c>server-error</c> value that refuses it.</returns>
    public static (ScramClientFirstMessage? Message, string? Error) Read(ReadOnlySpan<byte> message)
    {
        if (!Utf8.IsValid(message))
        {
            return (null, InvalidEncoding);
        }

        var text = Encoding.UTF8.GetString(message);
        var parts = text.Split(',');
        if (parts.Length < 4)
        {
            return (null, InvalidEncoding);
        }

        var flagError = ReadChannelBindingFlagError(parts[0]);
        return flagError is null ? ReadAfterFlag(text, parts) : (null, flagError);
    }

    private static string? ReadChannelBindingFlagError(string flag) => flag switch
    {
        "n" or "y" => null,
        _ when flag.StartsWith("p=", StringComparison.Ordinal) => ChannelBindingNotSupported,
        _ => InvalidEncoding,
    };

    private static (ScramClientFirstMessage? Message, string? Error) ReadAfterFlag(string text, string[] parts)
    {
        if (parts[2].StartsWith("m=", StringComparison.Ordinal))
        {
            return (null, ExtensionsNotSupported);
        }

        var authorizationId = parts[1].Length > 0 ? ReadAttribute(parts[1], 'a') : string.Empty;
        var userName = ReadAttribute(parts[2], 'n');
        var nonce = ReadNonce(parts[3]);

        return authorizationId is null || userName is null || nonce is null
            ? (null, InvalidEncoding)
            : (Create(text, parts, authorizationId, userName, nonce), null);
    }

    // The GS2 header is the flag, the authzid and their two commas; an empty authzid is none.
    private static ScramClientFirstMessage Create(
        string text, string[] parts, string authorizationId, string userName, string nonce)
    {
        var headerLength = parts[0].Length + parts[1].Length + 2;

        return new ScramClientFirstMessage(
            text[..headerLength], text[headerLength..], userName, authorizationId.Length > 0 ? authorizationId : null, nonce);
    }

    /// <summary>
    /// The value of a <c>saslname</c> attribute <c>&lt;name&gt;=&lt;value&gt;</c>, its <c>=2C</c>
    /// and <c>=3D</c> decoded to <c>,</c> and <c>=</c>.
    /// </summary>
    /// <param name="part">The attribute as sent.</param>
    /// <param name="name">The attribute's letter.</param>
    /// <returns>The decoded value, or <see langword="null"/> when it is another attribute, empty, or has any other <c>=</c>.</returns>
    public static string? ReadAttribute(string part, char name) =>
        part.Length >= 3 && part[0] == name && part[1] == '=' ? DecodeSaslName(part[2..]) : null;

    private static string? DecodeSaslName(string value)
    {
        var decoded = new StringBuilder(value.Length);
        var index = 0;
        while (index < value.Length)
        {
            var isEscape = value[index] == '=';
            var character = isEscape ? DecodeEscape(value.AsSpan(index + 1)) : value[index];
            if (character is null)
            {
                return null;
            }

            decoded.Append(character.Value);
            index += isEscape ? 3 : 1;
        }

        return decoded.ToString();
    }

    private static char? DecodeEscape(ReadOnlySpan<char> afterEquals) =>
        afterEquals.StartsWith("2C") ? ',' : afterEquals.StartsWith("3D") ? '=' : null;

    /// <summary>
    /// The value of an <c>r=</c> attribute: one or more printable ASCII characters but <c>,</c>.
    /// </summary>
    /// <param name="part">The attribute as sent.</param>
    /// <returns>The nonce, or <see langword="null"/> when it is not one.</returns>
    public static string? ReadNonce(string part) =>
        part.Length > 2 && part.StartsWith("r=", StringComparison.Ordinal) && part[2..].All(character => character is >= '!' and <= '~')
            ? part[2..]
            : null;
}
