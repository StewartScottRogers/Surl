using System.Buffers;
using System.Text.Unicode;

namespace Surl.MailStore;

/// <summary>
/// How the store names mailboxes (ADR-0050, decision 3): flat strings, <c>INBOX</c> matched in
/// any case and kept in capitals, every other name matched ordinally.
/// </summary>
internal static class MailboxName
{
    /// <summary>
    /// The name of the mailbox every owner reached has.
    /// </summary>
    public const string Inbox = "INBOX";

    /// <summary>
    /// The name the store keeps <paramref name="name"/> under: <c>INBOX</c> for any case of it,
    /// otherwise the name itself.
    /// </summary>
    public static string Canonical(string name) =>
        string.Equals(name, Inbox, StringComparison.OrdinalIgnoreCase) ? Inbox : name;

    /// <summary>
    /// Whether <paramref name="name"/> is 1 to <see cref="MailboxStore.MaxMailboxNameBytes"/>
    /// UTF-8 bytes of valid UTF-16 with no control character (U+0000 to U+001F, U+007F).
    /// </summary>
    public static bool IsValid(string name)
    {
        if (name.Length is 0 or > MailboxStore.MaxMailboxNameBytes || name.Any(IsControl))
        {
            return false;
        }

        Span<byte> utf8 = stackalloc byte[MailboxStore.MaxMailboxNameBytes];
        return Utf8.FromUtf16(name, utf8, out _, out _, replaceInvalidSequences: false) == OperationStatus.Done;
    }

    private static bool IsControl(char character) => character is < ' ' or '\u007F';
}
