namespace Surl.Cli;

/// <summary>What follows the separator in a layer-2 item's head (ADR-0101 decision 3).</summary>
internal enum ConfigurationItemValueKind
{
    /// <summary>Nothing: the option has no separator, and the whole head is the name (<c>--topic</c>, <c>--queue</c>).</summary>
    None,

    /// <summary>
    /// One non-empty field that may hold any character but U+0000: a password or a path
    /// (<c>--mailbox</c>, <c>--share</c>, <c>--bucket</c>).
    /// </summary>
    Text,

    /// <summary>One non-empty field with no control character: an address or a name (<c>--mail-catch-all</c>).</summary>
    Name,

    /// <summary>A <c>,</c>-separated list of names (<c>--mail-alias</c>).</summary>
    List,

    /// <summary>A <c>,</c>-separated list of members, each an account name or <c>@</c> and a group name (<c>--group</c>).</summary>
    Members,

    /// <summary>
    /// A <c>,</c>-separated list of grants, each a member, its last <c>:</c> and one of the shape's
    /// access words (<c>--share-access</c>, <c>--topic-access</c>).
    /// </summary>
    Grants,
}
