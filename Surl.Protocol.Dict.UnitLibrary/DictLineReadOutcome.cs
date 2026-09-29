namespace Surl.Protocol.Dict;

/// <summary>
/// How an attempt to read one DICT command line ended.
/// </summary>
internal enum DictLineReadOutcome
{
    /// <summary>
    /// A whole line, ended by LF, was read.
    /// </summary>
    LineRead,

    /// <summary>
    /// The client closed the connection between lines.
    /// </summary>
    ConnectionClosed,

    /// <summary>
    /// The client closed the connection part way through a line.
    /// </summary>
    ConnectionClosedMidLine,

    /// <summary>
    /// The line, its line ending included, is longer than the line limit.
    /// </summary>
    LineTooLong,
}
