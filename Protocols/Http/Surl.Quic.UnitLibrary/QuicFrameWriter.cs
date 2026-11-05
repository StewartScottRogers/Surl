namespace Surl.Quic;

/// <summary>Writes frames, in order, into a packet payload in the clear (RFC 9000 section 19).</summary>
public static class QuicFrameWriter
{
    /// <summary>Returns the payload the frames make, each frame's type first.</summary>
    /// <param name="frames">The frames, in the order they are to be sent.</param>
    /// <returns>The payload bytes.</returns>
    /// <exception cref="ArgumentException">A frame holds a value RFC 9000 does not allow it to send.</exception>
    public static byte[] Write(IEnumerable<QuicFrame> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);
        var writer = new QuicWriter();
        foreach (var frame in frames)
        {
            frame.WriteTo(writer);
        }

        return writer.ToArray();
    }
}
