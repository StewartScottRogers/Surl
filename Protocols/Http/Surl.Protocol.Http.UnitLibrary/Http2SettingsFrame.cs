using System.Buffers.Binary;

namespace Surl.Protocol.Http;

/// <summary>
/// A <c>SETTINGS</c> frame (RFC 9113 section 6.5), always on stream 0: either settings in the
/// order sent, or, with <c>ACK</c>, the acknowledgement of the peer's, with none.
/// </summary>
/// <param name="Ack">The <c>ACK</c> flag.</param>
/// <param name="Settings">The settings, unknown identifiers included, in wire order.</param>
internal sealed record Http2SettingsFrame(bool Ack, IReadOnlyList<Http2Setting> Settings) : Http2Frame(0)
{
    /// <summary>The acknowledgement of the peer's settings: <c>ACK</c> and no payload.</summary>
    public static Http2SettingsFrame Acknowledgement { get; } = new(true, []);

    /// <inheritdoc />
    public override Http2FrameType Type => Http2FrameType.Settings;

    /// <inheritdoc />
    public override byte Flags => Http2FrameFlags.When(Ack, Http2FrameFlags.Ack);

    /// <inheritdoc />
    public override int PayloadLength => Settings.Count * Http2Setting.EncodedLength;

    /// <inheritdoc />
    public override void WritePayload(Span<byte> payload)
    {
        for (var index = 0; index < Settings.Count; index++)
        {
            var entry = payload[(index * Http2Setting.EncodedLength)..];
            BinaryPrimitives.WriteUInt16BigEndian(entry, (ushort)Settings[index].Identifier);
            BinaryPrimitives.WriteUInt32BigEndian(entry[2..], Settings[index].Value);
        }
    }
}
