namespace Surl.Quic;

/// <summary>
/// A run of <c>PADDING</c> frames (type <c>0x00</c>, RFC 9000 section 19.1). Each padding frame
/// is one zero byte; the reader folds consecutive ones into a single value of that many bytes.
/// </summary>
/// <param name="Length">How many padding bytes; at least 1.</param>
public sealed record QuicPaddingFrame(int Length) : QuicFrame
{
    internal static QuicPaddingFrame ReadFrom(QuicReader reader, byte frameType)
    {
        var length = 1;
        while (!reader.IsAtEnd && reader.PeekByte() == QuicFrameTypes.Padding)
        {
            reader.ReadByte();
            length++;
        }

        return new QuicPaddingFrame(length);
    }

    internal override void WriteTo(QuicWriter writer)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(Length, 1);
        writer.WriteZeros(Length);
    }
}
