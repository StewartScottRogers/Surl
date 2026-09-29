using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Gopher;

/// <summary>
/// Why <see cref="GopherSelectorReader.ReadAsync"/> did or did not return a selector line.
/// </summary>
internal enum GopherSelectorReadOutcome
{
    /// <summary>
    /// A whole line arrived.
    /// </summary>
    LineRead = 0,

    /// <summary>
    /// The client half-closed before a line feed arrived.
    /// </summary>
    ConnectionClosed = 1,

    /// <summary>
    /// The line passed <see cref="ExchangeLimits.MaxLineBytes"/>, line ending included.
    /// </summary>
    LineTooLong = 2,

    /// <summary>
    /// <see cref="ExchangeLimits.HeadTimeout"/> ran out before a whole line arrived.
    /// </summary>
    HeadTimedOut = 3,
}
