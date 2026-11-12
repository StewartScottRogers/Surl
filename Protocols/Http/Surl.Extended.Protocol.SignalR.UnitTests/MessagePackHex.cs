namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// Turns the spaced hexadecimal the MessagePack tests are written in into bytes.
/// </summary>
internal static class MessagePackHex
{
    public static byte[] Bytes(string hex) => Convert.FromHexString(hex.Replace(" ", string.Empty, StringComparison.Ordinal));

    /// <summary>
    /// The bytes with a one-byte VarInt length prefix, for a message of fewer than 128 bytes.
    /// </summary>
    public static byte[] Prefixed(string hex)
    {
        var body = Bytes(hex);

        return [(byte)body.Length, .. body];
    }

    /// <summary>
    /// The message inside a one-byte-prefixed binary message the server sent.
    /// </summary>
    public static byte[] Unprefixed(byte[] message) => message[1..];
}
