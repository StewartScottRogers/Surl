namespace Surl.Content;

/// <summary>
/// Why <see cref="ContentStore.MapRequestPath(string)"/> refused a request path.
/// </summary>
public enum ContentPathRefusal
{
    /// <summary>
    /// Not refused: the request path was mapped.
    /// </summary>
    None = 0,

    /// <summary>
    /// The request path does not start with <c>/</c>.
    /// </summary>
    NotRooted = 1,

    /// <summary>
    /// The request path has an empty segment (<c>//</c>) anywhere but a trailing
    /// <c>/</c>, which is how a UNC path such as <c>//server/share</c> starts.
    /// </summary>
    EmptySegment = 2,

    /// <summary>
    /// A <c>%</c> is not followed by two hexadecimal digits.
    /// </summary>
    InvalidPercentEncoding = 3,

    /// <summary>
    /// A segment's percent-decoded bytes are not valid UTF-8.
    /// </summary>
    InvalidUtf8 = 4,

    /// <summary>
    /// A decoded segment is <c>.</c> or <c>..</c>, whether written raw or percent-encoded.
    /// </summary>
    DotSegment = 5,

    /// <summary>
    /// A decoded segment contains <c>/</c> or <c>\</c>, which only a percent escape or a raw
    /// backslash can put there.
    /// </summary>
    SeparatorInSegment = 6,

    /// <summary>
    /// A decoded segment contains <c>:</c>, which names a drive (<c>C:</c>) or an
    /// alternate data stream on Windows.
    /// </summary>
    ColonInSegment = 7,

    /// <summary>
    /// A decoded segment contains a control character, <c>NUL</c> included.
    /// </summary>
    ControlCharacter = 8,

    /// <summary>
    /// A decoded segment ends in <c>.</c> or a space, which Windows strips, so the name
    /// served would not be the name asked for.
    /// </summary>
    TrailingDotOrSpace = 9,

    /// <summary>
    /// A decoded segment is a Windows device name such as <c>CON</c>, <c>NUL</c> or
    /// <c>COM1</c>, with or without an extension.
    /// </summary>
    ReservedDeviceName = 10,

    /// <summary>
    /// The path is well formed, but a symbolic link along it finally resolves outside the
    /// served root.
    /// </summary>
    ResolvesOutsideRoot = 11,
}
