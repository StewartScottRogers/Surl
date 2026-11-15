using System.Buffers.Binary;

namespace Surl.Tls13Stream;

/// <summary>Variations of RFC 8448 section 3's ClientHello, made by splicing its bytes and fixing the length fields.</summary>
internal static class UnitTestClientHellos
{
    private const int SessionIdLengthIndex = 38;

    /// <summary>The ClientHello with a 32-byte legacy_session_id, as a client in middlebox-compatibility mode sends.</summary>
    public static byte[] WithSessionId(byte[] clientHello)
    {
        byte[] spliced = [.. clientHello[..SessionIdLengthIndex], 0x20, .. Enumerable.Repeat((byte)7, 32), .. clientHello[(SessionIdLengthIndex + 1)..]];
        return AdjustHandshakeLength(spliced, 32);
    }

    /// <summary>The ClientHello with one more extension at the end.</summary>
    public static byte[] WithExtension(byte[] clientHello, ushort type, byte[] data)
    {
        byte[] extension = [(byte)(type >> 8), (byte)type, (byte)(data.Length >> 8), (byte)data.Length, .. data];
        var copy = AdjustHandshakeLength([.. clientHello, .. extension], extension.Length);
        AdjustExtensionsLength(copy, extension.Length);
        return copy;
    }

    /// <summary>The ClientHello offering ALPN with <paramref name="protocols"/>.</summary>
    public static byte[] WithApplicationProtocols(byte[] clientHello, params string[] protocols)
    {
        byte[] list = [.. protocols.SelectMany(protocol => new byte[] { (byte)protocol.Length }.Concat(System.Text.Encoding.ASCII.GetBytes(protocol)))];
        return WithExtension(clientHello, 0x0010, [(byte)(list.Length >> 8), (byte)list.Length, .. list]);
    }

    /// <summary>The ClientHello with an empty key_share, which makes the server ask for a share with a HelloRetryRequest.</summary>
    public static byte[] WithEmptyKeyShare(byte[] clientHello)
    {
        var start = clientHello.AsSpan().IndexOf(new byte[] { 0x00, 0x33, 0x00, 0x26, 0x00, 0x24 });
        Assert.IsGreaterThan(0, start);
        const int removed = 36;
        byte[] spliced = [.. clientHello[..start], 0x00, 0x33, 0x00, 0x02, 0x00, 0x00, .. clientHello[(start + 4 + 0x26)..]];
        var copy = AdjustHandshakeLength(spliced, -removed);
        AdjustExtensionsLength(copy, -removed);
        return copy;
    }

    /// <summary>The record layer's plaintext record around a handshake message.</summary>
    public static byte[] Record(byte contentType, byte[] fragment) =>
        [contentType, 3, 3, (byte)(fragment.Length >> 8), (byte)fragment.Length, .. fragment];

    private static byte[] AdjustHandshakeLength(byte[] message, int delta)
    {
        var length = (message[1] << 16) | (message[2] << 8) | message[3];
        length += delta;
        message[1] = (byte)(length >> 16);
        message[2] = (byte)(length >> 8);
        message[3] = (byte)length;
        return message;
    }

    private static void AdjustExtensionsLength(byte[] message, int delta)
    {
        var index = 4 + 2 + 32 + 1 + message[SessionIdLengthIndex];
        index += 2 + BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan(index));
        index += 1 + message[index];
        BinaryPrimitives.WriteUInt16BigEndian(message.AsSpan(index), (ushort)(BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan(index)) + delta));
    }
}
