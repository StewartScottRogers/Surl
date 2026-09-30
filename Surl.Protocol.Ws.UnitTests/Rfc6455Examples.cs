namespace Surl.Protocol.Ws;

/// <summary>
/// The example frames of RFC 6455 section 5.7, byte for byte.
/// </summary>
internal static class Rfc6455Examples
{
    /// <summary>The payload every text example carries: "Hello".</summary>
    public static readonly byte[] Hello = [0x48, 0x65, 0x6c, 0x6c, 0x6f];

    /// <summary>A single-frame unmasked text message containing "Hello".</summary>
    public static readonly byte[] UnmaskedTextHello = [0x81, 0x05, 0x48, 0x65, 0x6c, 0x6c, 0x6f];

    /// <summary>A single-frame masked text message containing "Hello".</summary>
    public static readonly byte[] MaskedTextHello = [0x81, 0x85, 0x37, 0xfa, 0x21, 0x3d, 0x7f, 0x9f, 0x4d, 0x51, 0x58];

    /// <summary>The first frame of a fragmented unmasked text message: "Hel".</summary>
    public static readonly byte[] FragmentedTextFirst = [0x01, 0x03, 0x48, 0x65, 0x6c];

    /// <summary>The second frame of a fragmented unmasked text message: "lo".</summary>
    public static readonly byte[] FragmentedTextSecond = [0x80, 0x02, 0x6c, 0x6f];

    /// <summary>An unmasked ping request whose body is "Hello".</summary>
    public static readonly byte[] UnmaskedPingHello = [0x89, 0x05, 0x48, 0x65, 0x6c, 0x6c, 0x6f];

    /// <summary>A masked ping response (a pong) whose body is "Hello".</summary>
    public static readonly byte[] MaskedPongHello = [0x8a, 0x85, 0x37, 0xfa, 0x21, 0x3d, 0x7f, 0x9f, 0x4d, 0x51, 0x58];

    /// <summary>The header of a 256-byte binary message in a single unmasked frame.</summary>
    public static readonly byte[] Binary256Header = [0x82, 0x7E, 0x01, 0x00];

    /// <summary>The header of a 64 KiB binary message in a single unmasked frame.</summary>
    public static readonly byte[] Binary64KiBHeader = [0x82, 0x7F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00];

    /// <summary>Concatenates <paramref name="parts"/>.</summary>
    public static byte[] Join(params byte[][] parts) => parts.SelectMany(part => part).ToArray();

    /// <summary>A payload of <paramref name="length"/> bytes counting up modulo 256.</summary>
    public static byte[] CountingPayload(int length) => Enumerable.Range(0, length).Select(index => (byte)index).ToArray();
}
