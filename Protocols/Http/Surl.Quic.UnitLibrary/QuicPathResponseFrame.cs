namespace Surl.Quic;

/// <summary>A <c>PATH_RESPONSE</c> frame (type <c>0x1b</c>, RFC 9000 section 19.18): the 8 bytes of a <c>PATH_CHALLENGE</c>, echoed.</summary>
/// <param name="Data">The 8 bytes of the challenge being answered.</param>
public sealed record QuicPathResponseFrame(ReadOnlyMemory<byte> Data) : QuicFrame
{
    internal static QuicPathResponseFrame ReadFrom(QuicReader reader, byte frameType) => new(reader.ReadBytes(QuicPathChallengeFrame.DataLength));

    internal override void WriteTo(QuicWriter writer) => QuicPathChallengeFrame.WritePathData(writer, QuicFrameTypes.PathResponse, Data);
}
