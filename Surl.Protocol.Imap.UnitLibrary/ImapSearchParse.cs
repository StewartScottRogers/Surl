namespace Surl.Protocol.Imap;

/// <summary>
/// How reading a <c>SEARCH</c>'s arguments ended.
/// </summary>
internal enum ImapSearchParse
{
    /// <summary>The arguments were read: answer the search.</summary>
    Read,

    /// <summary>The arguments do not parse: <c>BAD Invalid arguments</c>.</summary>
    Invalid,

    /// <summary>A charset other than <c>US-ASCII</c> and <c>UTF-8</c>: <c>NO [BADCHARSET ...]</c>.</summary>
    UnsupportedCharset,
}
