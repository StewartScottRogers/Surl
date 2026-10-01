namespace Surl.Protocol.Imap;

/// <summary>
/// The data items <c>FETCH</c> answers (RFC 3501, section 6.4.5; ADR-0055, decision 4).
/// </summary>
internal enum ImapFetchItemKind
{
    /// <summary><c>UID</c>.</summary>
    Uid,

    /// <summary><c>FLAGS</c>.</summary>
    Flags,

    /// <summary><c>INTERNALDATE</c>.</summary>
    InternalDate,

    /// <summary><c>RFC822.SIZE</c>.</summary>
    Rfc822Size,

    /// <summary><c>ENVELOPE</c>.</summary>
    Envelope,

    /// <summary><c>BODY</c>: the body structure without extension data.</summary>
    Body,

    /// <summary><c>BODYSTRUCTURE</c>: the body structure with extension data.</summary>
    BodyStructure,

    /// <summary><c>RFC822</c>: the whole message, as <c>BODY[]</c>.</summary>
    Rfc822,

    /// <summary><c>RFC822.HEADER</c>: the header, as <c>BODY.PEEK[HEADER]</c>.</summary>
    Rfc822Header,

    /// <summary><c>RFC822.TEXT</c>: the text, as <c>BODY[TEXT]</c>.</summary>
    Rfc822Text,

    /// <summary><c>BODY[&lt;section&gt;]&lt;&lt;partial&gt;&gt;</c> or its <c>BODY.PEEK</c> form.</summary>
    BodySection,
}
