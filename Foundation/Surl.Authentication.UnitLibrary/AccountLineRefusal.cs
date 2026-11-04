namespace Surl.Authentication;

/// <summary>
/// Why a <c>--user-file</c> line is not an account (ADR-0032, section 2).
/// </summary>
public enum AccountLineRefusal
{
    /// <summary>
    /// The line holds no <c>:</c>: <c>expected &lt;user:password&gt;</c>.
    /// </summary>
    ExpectedUserColonPassword,

    /// <summary>
    /// Nothing follows the first <c>:</c>: <c>the password is empty</c>.
    /// </summary>
    EmptyPassword,

    /// <summary>
    /// The user name holds U+0000 to U+001F or U+007F: <c>the user name holds a control character</c>.
    /// </summary>
    ControlCharacterInUserName,

    /// <summary>
    /// The user name was already given, in the file or by <c>--user</c>:
    /// <c>user &lt;user&gt; is given twice</c>.
    /// </summary>
    UserNameGivenTwice,

    /// <summary>
    /// The line's bytes are not UTF-8: <c>not UTF-8</c>.
    /// </summary>
    NotUtf8,
}
