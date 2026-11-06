namespace Surl.Quic;

/// <summary>A <c>PATH_CHALLENGE</c> frame (type <c>0x1a</c>, RFC 9000 section 19.17): 8 bytes the peer is to echo in a <c>PATH_RESPONSE</c>.</summary>
/// <param name="Data">The 8 bytes of challenge data.</param>
public sealed record QuicPathChallengeFrame(ReadOnlyMemory<byte> Data) : QuicFrame
{
    /// <summary>The length of path challenge and response data, in bytes.</summary>
    public const int DataLength = 8;

    internal static QuicPathChallengeFrame ReadFrom(QuicReader reader, byte frameType) => new(reader.ReadBytes(DataLength));

    internal override void WriteTo(QuicWriter writer) => WritePathData(writer, QuicFrameTypes.PathChallenge, Data);

    /// <summary>Writes a <c>PATH_CHALLENGE</c> or <c>PATH_RESPONSE</c> frame, refusing data that is not 8 bytes.</summary>
    internal static void WritePathData(QuicWriter writer, byte frameType, ReadOnlyMemory<byte> data)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(data.Length, DataLength, nameof(Data));
        writer.WriteByte(frameType);
        writer.WriteBytes(data.Span);
    }
}
