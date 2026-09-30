using System.Text;
using System.Text.Unicode;

namespace Surl.Authentication;

/// <summary>
/// Reads the text of an <c>--authorized-keys</c> file into keys, as ADR-0051 section 6 decides:
/// OpenSSH's <c>authorized_keys</c> format in UTF-8 with an optional byte-order mark, lines split
/// as a <c>--user-file</c>'s are (at LF, one CR before it dropped), blank lines and <c>#</c>
/// comments skipped (after leading spaces and tabs, as <c>sshd</c> skips them), and every other
/// line <c>&lt;key type&gt; &lt;base64 key blob&gt; [comment]</c>, separated by spaces or tabs.
/// Key options before the type are refused rather than ignored. It never touches the disk:
/// <c>Surl.Console</c> reads the file and hands its bytes here.
/// </summary>
public static class AuthorizedKeysParser
{
    private static readonly byte[] ByteOrderMark = [0xEF, 0xBB, 0xBF];

    private static readonly char[] FieldSeparators = [' ', '\t'];

    // The options sshd(8) takes without a value ("AUTHORIZED_KEYS FILE FORMAT").
    private static readonly IReadOnlySet<string> FlagOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "agent-forwarding", "cert-authority", "no-agent-forwarding", "no-port-forwarding", "no-pty",
        "no-touch-required", "no-user-rc", "no-X11-forwarding", "port-forwarding", "pty", "restrict",
        "user-rc", "verify-required", "X11-forwarding",
    };

    /// <summary>
    /// Parses <paramref name="content"/> as the keys <paramref name="userName"/> may log in with.
    /// A file with no keys is not an error.
    /// </summary>
    /// <param name="content">The file's bytes.</param>
    /// <param name="userName">The user part of <c>--authorized-keys &lt;user:file&gt;</c>, already checked by <c>Surl.Cli</c>.</param>
    /// <returns>Every key, or the first line refused and why.</returns>
    public static AuthorizedKeysParseResult Parse(ReadOnlySpan<byte> content, string userName)
    {
        ArgumentNullException.ThrowIfNull(userName);

        var keys = new List<AuthorizedKey>();
        var remaining = content.StartsWith(ByteOrderMark) ? content[ByteOrderMark.Length..] : content;
        var lineNumber = 0;
        while (!remaining.IsEmpty)
        {
            lineNumber++;
            var failure = ReadLine(TakeLine(ref remaining), lineNumber, userName, keys);
            if (failure is not null)
            {
                return new AuthorizedKeysParseResult([], failure);
            }
        }

        return new AuthorizedKeysParseResult(keys, null);
    }

    private static ReadOnlySpan<byte> TakeLine(ref ReadOnlySpan<byte> remaining)
    {
        var end = remaining.IndexOf((byte)'\n');
        var line = end < 0 ? remaining : remaining[..end];
        remaining = end < 0 ? [] : remaining[(end + 1)..];

        return line.EndsWith((byte)'\r') ? line[..^1] : line;
    }

    private static AuthorizedKeysLineFailure? ReadLine(
        ReadOnlySpan<byte> line, int lineNumber, string userName, List<AuthorizedKey> keys)
    {
        if (!Utf8.IsValid(line))
        {
            return new AuthorizedKeysLineFailure(lineNumber, AuthorizedKeysLineRefusal.NotUtf8, null);
        }

        var text = Encoding.UTF8.GetString(line).TrimStart(FieldSeparators);
        if (text.Length == 0 || text.StartsWith('#'))
        {
            return null;
        }

        var fields = text.Split(FieldSeparators, StringSplitOptions.RemoveEmptyEntries);
        var refusal = RefuseFields(fields, out var blob);
        if (refusal is not null)
        {
            var keyType = refusal == AuthorizedKeysLineRefusal.KeyTypeNotSupported ? fields[0] : null;

            return new AuthorizedKeysLineFailure(lineNumber, refusal.Value, keyType);
        }

        keys.Add(new AuthorizedKey(userName, fields[0], blob));

        return null;
    }

    private static AuthorizedKeysLineRefusal? RefuseFields(string[] fields, out byte[] blob)
    {
        blob = [];
        if (fields.Length < 2)
        {
            return AuthorizedKeysLineRefusal.ExpectedKeyTypeAndKey;
        }

        if (!SshPublicKeyBlob.SupportedKeyTypes.Contains(fields[0]))
        {
            return IsKeyOptions(fields[0])
                ? AuthorizedKeysLineRefusal.KeyOptionsNotSupported
                : AuthorizedKeysLineRefusal.KeyTypeNotSupported;
        }

        blob = new byte[fields[1].Length];
        var isBase64 = Convert.TryFromBase64String(fields[1], blob, out var length);
        blob = blob[..length];

        return isBase64 && SshPublicKeyBlob.IsWellFormed(blob, fields[0]) ? null : AuthorizedKeysLineRefusal.MalformedKey;
    }

    // An option list is comma-separated, and a valued option holds '=' and often a quoted value.
    private static bool IsKeyOptions(string field) =>
        field.AsSpan().IndexOfAny("=\",") >= 0 || FlagOptions.Contains(field);
}
