using System.Globalization;
using System.Text;

namespace Surl.Authentication;

/// <summary>
/// The first <c>--authorized-keys</c> line that is not a key, and why (ADR-0051, section 6).
/// </summary>
/// <param name="LineNumber">The line, counted from 1.</param>
/// <param name="Refusal">Why the line was refused.</param>
/// <param name="KeyType">
/// The key type the line names for <see cref="AuthorizedKeysLineRefusal.KeyTypeNotSupported"/>,
/// unescaped; otherwise <see langword="null"/>.
/// </param>
public sealed record AuthorizedKeysLineFailure(int LineNumber, AuthorizedKeysLineRefusal Refusal, string? KeyType)
{
    /// <summary>
    /// The refusal as ADR-0051 section 6 words it after the path: <c>line &lt;n&gt;: &lt;reason&gt;</c>,
    /// with the key type escaped as ADR-0006 section 3 escapes a peer's bytes. It never holds a
    /// key. <c>Surl.Console</c> writes <c>surl: (2) Authorized keys &lt;path&gt;, </c> before it.
    /// </summary>
    /// <returns>For example <c>line 3: key type sk-ssh-ed25519@openssh.com is not supported</c>.</returns>
    public string Describe() => $"line {LineNumber}: {DescribeRefusal()}";

    private string DescribeRefusal() => Refusal switch
    {
        AuthorizedKeysLineRefusal.ExpectedKeyTypeAndKey => "expected <key type> <key>",
        AuthorizedKeysLineRefusal.KeyOptionsNotSupported => "key options are not supported",
        AuthorizedKeysLineRefusal.KeyTypeNotSupported => $"key type {Escape(KeyType ?? string.Empty)} is not supported",
        AuthorizedKeysLineRefusal.MalformedKey => "the key is malformed",
        _ => "not UTF-8",
    };

    // ADR-0006 section 3: each byte 0x20 to 0x7E but backslash as itself, CR as \r, LF as \n,
    // every other byte as \x and two upper-case hex digits.
    private static string Escape(string text)
    {
        var builder = new StringBuilder();
        foreach (var value in Encoding.UTF8.GetBytes(text))
        {
            builder.Append(EscapeByte(value));
        }

        return builder.ToString();
    }

    private static string EscapeByte(byte value) => value switch
    {
        (byte)'\r' => "\\r",
        (byte)'\n' => "\\n",
        >= 0x20 and <= 0x7E when value != (byte)'\\' => ((char)value).ToString(),
        _ => "\\x" + value.ToString("X2", CultureInfo.InvariantCulture),
    };
}
