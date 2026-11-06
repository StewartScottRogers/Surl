namespace Surl.Cli;

/// <summary>
/// One mailbox from a <c>--mailbox &lt;address&gt;:&lt;password&gt;[;protocols=&lt;scheme&gt;,...]</c> item or a
/// <c>--mailbox-file</c> record (ADR-0101 decision 4, ADR-0102 decisions 1.2 and 1.4): an account and a
/// mail owner, both named by <see cref="Address"/>. Read by <see cref="ConfiguredMailboxReader"/> and
/// <see cref="MailboxFileReader"/>.
/// </summary>
/// <param name="Address">
/// The normalised address: the local part as written, <c>@</c>, and the domain in
/// <see cref="MailDomainNames"/>' ASCII lower-case form. Never holds a <c>:</c> or a control character.
/// </param>
/// <param name="Password">The account's password, never empty, never holding U+0000.</param>
/// <param name="Protocols">
/// The schemes, lower-case and each once, the account may log in on; <see langword="null"/> when
/// <c>protocols=</c> was not given, which <c>Surl.Composition</c> reads as the mail schemes only
/// (ADR-0102 decision 1.4).
/// </param>
public sealed record ConfiguredMailbox(string Address, string Password, IReadOnlyList<string>? Protocols)
{
    /// <summary>
    /// Names the mailbox without its password (ADR-0101 decision 5), so no log or exception text
    /// can hold one.
    /// </summary>
    /// <returns><c>ConfiguredMailbox { Address = &lt;address&gt; }</c>.</returns>
    public override string ToString() => $"ConfiguredMailbox {{ Address = {Address} }}";
}
