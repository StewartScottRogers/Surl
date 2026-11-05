namespace Surl.Quic;

/// <summary>
/// One QUIC frame of RFC 9000 section 19 as a typed value. <see cref="QuicFrameReader"/> reads
/// a packet payload into frames and <see cref="QuicFrameWriter"/> writes frames back into one;
/// <see cref="ToBytes"/> writes a single frame.
/// </summary>
public abstract record QuicFrame
{
    private protected QuicFrame()
    {
    }

    /// <summary>Returns this frame's bytes, its type first.</summary>
    /// <returns>The frame's encoding.</returns>
    /// <exception cref="ArgumentException">The frame holds a value RFC 9000 does not allow it to send.</exception>
    public byte[] ToBytes()
    {
        var writer = new QuicWriter();
        WriteTo(writer);
        return writer.ToArray();
    }

    /// <summary>Writes this frame, its type first.</summary>
    internal abstract void WriteTo(QuicWriter writer);
}
