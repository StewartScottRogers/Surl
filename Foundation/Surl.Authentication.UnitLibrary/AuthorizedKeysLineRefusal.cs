namespace Surl.Authentication;

/// <summary>
/// Why an <c>--authorized-keys</c> line is not a key (ADR-0051, section 6).
/// </summary>
public enum AuthorizedKeysLineRefusal
{
    /// <summary>
    /// The line holds fewer than a key type and a key: <c>expected &lt;key type&gt; &lt;key&gt;</c>.
    /// </summary>
    ExpectedKeyTypeAndKey,

    /// <summary>
    /// OpenSSH key options (<c>from=</c>, <c>command=</c>, <c>restrict</c>, ...) come before the key
    /// type: <c>key options are not supported</c>. Ignoring one would grant more than the file says.
    /// </summary>
    KeyOptionsNotSupported,

    /// <summary>
    /// The key type is not one ADR-0051 section 6 lists (<c>sk-*</c> and certificates included):
    /// <c>key type &lt;type&gt; is not supported</c>.
    /// </summary>
    KeyTypeNotSupported,

    /// <summary>
    /// The key is not base64, or its blob is malformed or of another type: <c>the key is malformed</c>.
    /// </summary>
    MalformedKey,

    /// <summary>
    /// The line's bytes are not UTF-8: <c>not UTF-8</c>.
    /// </summary>
    NotUtf8,
}
