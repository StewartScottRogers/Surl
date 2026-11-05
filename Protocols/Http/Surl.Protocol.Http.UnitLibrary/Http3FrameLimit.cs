namespace Surl.Protocol.Http;

/// <summary>
/// How long a frame other than <c>DATA</c> may be (ADR-0082 decision 7): <c>--max-message</c>, or
/// <see cref="int.MaxValue"/> bytes when that is 0 (no limit) or larger, since a payload is held whole.
/// </summary>
/// <param name="maxMessageBytes">The <c>--max-message</c> limit, 0 for none.</param>
internal sealed class Http3FrameLimit(long maxMessageBytes)
{
    /// <summary>The longest payload a frame other than <c>DATA</c> may have.</summary>
    public int MaxLength { get; } = (int)Math.Min(maxMessageBytes == 0 ? int.MaxValue : maxMessageBytes, int.MaxValue);

    /// <summary>Whether <paramref name="header"/>'s payload is longer than <see cref="MaxLength"/>.</summary>
    /// <param name="header">The frame header.</param>
    /// <returns><see langword="true"/> when it is too long.</returns>
    public bool IsTooLong(Http3FrameHeader header) => header.Length > (ulong)MaxLength;

    /// <summary>Throws <c>H3_EXCESSIVE_LOAD</c> when <paramref name="header"/>'s payload is longer than <see cref="MaxLength"/>.</summary>
    /// <param name="header">The header of a frame other than <c>DATA</c>.</param>
    /// <param name="streamId">The stream it arrived on, for the log.</param>
    /// <exception cref="Http3ConnectionException">The frame is too long.</exception>
    public void Check(Http3FrameHeader header, ulong streamId)
    {
        if (IsTooLong(header))
        {
            throw new Http3ConnectionException(
                Http3ErrorCode.ExcessiveLoad, $"a frame of type 0x{header.Type:x} on stream {streamId} is {header.Length} bytes, past the {MaxLength}-byte message limit");
        }
    }
}
