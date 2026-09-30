namespace Surl.MailStore;

/// <summary>
/// How <see cref="MailboxStore.ChangeFlags"/> combines the flags it is given with a message's
/// flags, as IMAP's <c>STORE</c> does with <c>+FLAGS</c>, <c>-FLAGS</c> and <c>FLAGS</c>.
/// </summary>
public enum MailFlagChange
{
    /// <summary>Set the given flags and keep the others.</summary>
    Add,

    /// <summary>Clear the given flags and keep the others.</summary>
    Remove,

    /// <summary>Set exactly the given flags.</summary>
    Replace,
}
