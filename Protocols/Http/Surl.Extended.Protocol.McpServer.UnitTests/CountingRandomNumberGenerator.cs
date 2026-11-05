using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Surl.Extended.Protocol.McpServer;

/// <summary>
/// A <see cref="RandomNumberGenerator" /> whose n-th call (1, 2, ...) writes n as a big-endian 32-bit number in
/// the first four bytes and fills the rest with n's low byte, so each session ID a test is given is known and
/// distinct.
/// </summary>
internal sealed class CountingRandomNumberGenerator : RandomNumberGenerator
{
    public int Calls { get; private set; }

    /// <summary>The bytes the n-th call writes into a buffer of <paramref name="length" />.</summary>
    public static byte[] BytesOfCall(int n, int length)
    {
        byte[] data = new byte[length];
        Array.Fill(data, (byte)n);
        BinaryPrimitives.WriteInt32BigEndian(data, n);
        return data;
    }

    public override void GetBytes(byte[] data)
    {
        Calls++;
        BytesOfCall(Calls, data.Length).CopyTo(data, 0);
    }
}
