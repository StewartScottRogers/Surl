using System.Buffers.Binary;
using System.Collections.Concurrent;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ldap;

/// <summary>
/// An <see cref="ISaslSecurityLayer"/> a test can read and write by hand: a protected message is
/// one marker byte - <see cref="ServerMarker"/> from the server, <see cref="ClientMarker"/> from
/// the client - then the message unchanged. A buffer from the client without its marker fails.
/// It records every buffer it was asked to unprotect.
/// </summary>
/// <param name="maximumProtectedBytes">The most protected bytes a buffer from the client may hold.</param>
internal sealed class UnitTestSecurityLayer(int maximumProtectedBytes = 65536) : ISaslSecurityLayer
{
    public const byte ServerMarker = 0x53;
    public const byte ClientMarker = 0x43;

    private readonly ConcurrentQueue<byte[]> unprotected = new();

    public int MaximumProtectedBytes => maximumProtectedBytes;

    /// <summary>Every buffer handed to <see cref="TryUnprotect"/>, without its length, in order.</summary>
    public IReadOnlyList<byte[]> BuffersFromTheClient => [.. unprotected];

    /// <summary>A buffer the client sends: the 4-byte length, the client's marker, the message.</summary>
    public static byte[] ClientBuffer(byte[] message) => Framed([ClientMarker, .. message]);

    /// <summary>The 4-byte big-endian length, then <paramref name="protectedBytes"/>.</summary>
    public static byte[] Framed(byte[] protectedBytes)
    {
        var buffer = new byte[4 + protectedBytes.Length];
        BinaryPrimitives.WriteInt32BigEndian(buffer, protectedBytes.Length);
        protectedBytes.CopyTo(buffer, 4);
        return buffer;
    }

    /// <summary>
    /// The messages in what the server wrote from <paramref name="start"/> on, each buffer's length
    /// and server marker checked and taken off.
    /// </summary>
    public static byte[] ServerMessages(byte[] written, int start)
    {
        var messages = new List<byte>();
        for (var offset = start; offset < written.Length;)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(written.AsSpan(offset));
            Assert.AreEqual(ServerMarker, written[offset + 4]);
            messages.AddRange(written.AsSpan(offset + 5, length - 1));
            offset += 4 + length;
        }

        return [.. messages];
    }

    public byte[] Protect(ReadOnlySpan<byte> message) => [ServerMarker, .. message];

    public bool TryUnprotect(ReadOnlySpan<byte> buffer, out byte[] message)
    {
        unprotected.Enqueue(buffer.ToArray());
        if (buffer.Length > 0 && buffer[0] == ClientMarker)
        {
            message = buffer[1..].ToArray();
            return true;
        }

        message = [];
        return false;
    }
}
