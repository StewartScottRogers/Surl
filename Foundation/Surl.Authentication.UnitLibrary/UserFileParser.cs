using System.Text;
using System.Text.Unicode;

namespace Surl.Authentication;

/// <summary>
/// Reads the text of a <c>--user-file</c> into accounts, as ADR-0032 section 2 decides:
/// UTF-8 with an optional byte-order mark, lines split at LF with one CR before it dropped,
/// blank lines and <c>#</c> comments skipped, and every other line <c>user:password</c> split at
/// its first <c>:</c>, nothing trimmed. It never touches the disk: <c>Surl.Console</c> reads the
/// file and hands its bytes here.
/// </summary>
public static class UserFileParser
{
    private static readonly byte[] ByteOrderMark = [0xEF, 0xBB, 0xBF];

    /// <summary>
    /// Parses <paramref name="content"/> and adds its accounts after
    /// <paramref name="userOptionAccounts"/> (ADR-0032, section 1: file accounts come after
    /// every <c>--user</c> account, and a name either already gave is refused).
    /// </summary>
    /// <param name="content">The file's bytes.</param>
    /// <param name="userOptionAccounts">The accounts from <c>--user</c>, already checked by <c>Surl.Cli</c>.</param>
    /// <returns>Every account, or the first line refused and why.</returns>
    public static UserFileParseResult Parse(ReadOnlySpan<byte> content, IReadOnlyList<Account> userOptionAccounts)
    {
        ArgumentNullException.ThrowIfNull(userOptionAccounts);

        var accounts = new List<Account>(userOptionAccounts);
        var names = new HashSet<string>(userOptionAccounts.Select(account => account.UserName), StringComparer.Ordinal);
        var remaining = SkipByteOrderMark(content);
        var lineNumber = 0;
        while (!remaining.IsEmpty)
        {
            lineNumber++;
            var failure = ReadLine(TakeLine(ref remaining), lineNumber, names, accounts);
            if (failure is not null)
            {
                return new UserFileParseResult([], failure);
            }
        }

        return new UserFileParseResult(accounts, null);
    }

    private static ReadOnlySpan<byte> SkipByteOrderMark(ReadOnlySpan<byte> content) =>
        content.StartsWith(ByteOrderMark) ? content[ByteOrderMark.Length..] : content;

    /// <summary>
    /// Takes the next line off <paramref name="remaining"/>: up to its LF, or to its end, with
    /// one CR before either dropped.
    /// </summary>
    private static ReadOnlySpan<byte> TakeLine(ref ReadOnlySpan<byte> remaining)
    {
        var end = remaining.IndexOf((byte)'\n');
        var line = end < 0 ? remaining : remaining[..end];
        remaining = end < 0 ? [] : remaining[(end + 1)..];

        return line.EndsWith((byte)'\r') ? line[..^1] : line;
    }

    private static UserFileLineFailure? ReadLine(
        ReadOnlySpan<byte> line, int lineNumber, HashSet<string> names, List<Account> accounts)
    {
        if (!Utf8.IsValid(line))
        {
            return new UserFileLineFailure(lineNumber, AccountLineRefusal.NotUtf8, null);
        }

        var text = Encoding.UTF8.GetString(line);
        if (IsBlankOrComment(text))
        {
            return null;
        }

        var colon = text.IndexOf(':', StringComparison.Ordinal);
        var refusal = RefuseAccountText(text, colon, names);
        if (refusal is not null)
        {
            var userNameGivenTwice = refusal == AccountLineRefusal.UserNameGivenTwice ? text[..colon] : null;

            return new UserFileLineFailure(lineNumber, refusal.Value, userNameGivenTwice);
        }

        var account = new Account(text[..colon], text[(colon + 1)..]);
        names.Add(account.UserName);
        accounts.Add(account);

        return null;
    }

    private static bool IsBlankOrComment(string text) =>
        text.StartsWith('#') || text.AsSpan().TrimStart(" \t").IsEmpty;

    private static AccountLineRefusal? RefuseAccountText(string text, int colon, HashSet<string> names)
    {
        if (colon < 0)
        {
            return AccountLineRefusal.ExpectedUserColonPassword;
        }

        if (colon == text.Length - 1)
        {
            return AccountLineRefusal.EmptyPassword;
        }

        var userName = text[..colon];
        if (userName.Any(IsControlCharacter))
        {
            return AccountLineRefusal.ControlCharacterInUserName;
        }

        return names.Contains(userName) ? AccountLineRefusal.UserNameGivenTwice : null;
    }

    private static bool IsControlCharacter(char character) => character < ' ' || character == '\u007F';
}
