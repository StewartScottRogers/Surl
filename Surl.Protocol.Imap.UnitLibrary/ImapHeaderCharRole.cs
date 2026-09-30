namespace Surl.Protocol.Imap;

/// <summary>
/// What one character of a header field value is part of, as <see cref="ImapHeaderText.Roles"/>
/// reads it.
/// </summary>
internal enum ImapHeaderCharRole
{
    /// <summary>Plain text, where a separator counts.</summary>
    Plain,

    /// <summary>A quoted string, its quotes included.</summary>
    Quoted,

    /// <summary>A comment, its parentheses included.</summary>
    Comment,

    /// <summary>An angle-bracketed address, its brackets included.</summary>
    Angle,
}
