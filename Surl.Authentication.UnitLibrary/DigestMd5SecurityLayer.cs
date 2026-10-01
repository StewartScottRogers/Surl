using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Surl.Cryptography.Rc4;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// <c>DIGEST-MD5</c>'s SASL security layer after an LDAP bind that chose <c>qop=auth-int</c> or
/// <c>auth-conf</c> (ADR-0072, decision 4): RFC 2831 sections 2.3 and 2.4. Each direction has its
/// own integrity key (<c>Kic</c> client to server, <c>Kis</c> server to client, the MD5 of
/// <c>H(A1)</c> and the section's magic constant) and its own sequence number from 0. A protected
/// buffer ends in the 10-byte MAC - the first ten bytes of <c>HMAC-MD5(Ki, SeqNum || message)</c>
/// - the message type 1 (2 bytes) and the sequence number (4 bytes, big-endian, as every number
/// in the RFC). With integrity the message comes first, in clear; with confidentiality the message
/// and the MAC are encrypted together under the direction's confidentiality key (<c>Kcc</c> or
/// <c>Kcs</c>, from the first 16 bytes of <c>H(A1)</c>): <c>rc4</c> with one keystream kept
/// running across messages, or <c>3des</c>, two-key triple DES in CBC mode whose keys are the
/// key's first two 7-byte halves spread over 8 bytes each, whose first IV is the key's last 8
/// bytes and each later IV the last ciphertext block before it, with 1 to 8 padding bytes, each
/// holding the count, between the message and the MAC.
/// </summary>
internal sealed class DigestMd5SecurityLayer : ISaslSecurityLayer
{
    /// <summary>
    /// The <c>maxbuf</c> Surl's LDAP challenge offers: the most protected bytes one buffer from
    /// the client may hold.
    /// </summary>
    public const int OfferedMaximumBuffer = 65536;

    private const int MacLength = 10;

    private const int MessageTypeLength = 2;

    private const int SequenceNumberLength = 4;

    private const int TrailerLength = MessageTypeLength + SequenceNumberLength;

    private const ushort MessageType = 1;

    private const string ClientToServer = "client-to-server";

    private const string ServerToClient = "server-to-client";

    private readonly Direction sending;
    private readonly Direction receiving;

    private DigestMd5SecurityLayer(byte[] sessionKey, string? cipher, bool isAcceptor)
    {
        var client = new Direction(sessionKey, cipher, ClientToServer);
        var server = new Direction(sessionKey, cipher, ServerToClient);
        (sending, receiving) = isAcceptor ? (server, client) : (client, server);
    }

    /// <inheritdoc/>
    public int MaximumProtectedBytes => OfferedMaximumBuffer;

    /// <summary>
    /// The server's layer for an accepted <paramref name="response"/>: it protects with the
    /// server-to-client keys and unprotects with the client-to-server ones.
    /// </summary>
    /// <param name="sessionKey"><c>H(A1)</c>, from <see cref="DigestMd5Calculation.ComputeSessionKey"/>.</param>
    /// <param name="response">The accepted response, whose <c>qop</c> and <c>cipher</c> choose the layer.</param>
    /// <returns>The layer, or <see langword="null"/> for <c>qop=auth</c>, which has none.</returns>
    public static DigestMd5SecurityLayer? ForAcceptor(byte[] sessionKey, DigestMd5Response response) =>
        Create(sessionKey, response, isAcceptor: true);

    /// <summary>
    /// The client's layer, the mirror of <see cref="ForAcceptor"/>: what upstream curl's
    /// <c>WinLDAP</c> runs, so a test can play the other side.
    /// </summary>
    /// <param name="sessionKey"><c>H(A1)</c>.</param>
    /// <param name="response">The response the client sent.</param>
    /// <returns>The layer, or <see langword="null"/> for <c>qop=auth</c>.</returns>
    public static DigestMd5SecurityLayer? ForInitiator(byte[] sessionKey, DigestMd5Response response) =>
        Create(sessionKey, response, isAcceptor: false);

    /// <inheritdoc/>
    public byte[] Protect(ReadOnlySpan<byte> message)
    {
        var sequenceNumber = sending.SequenceNumber++;
        var mac = ComputeMac(sending, sequenceNumber, message);
        byte[] body = sending.Sealing is { } sealing ? sealing.Seal(message, mac) : [.. message, .. mac];

        var buffer = new byte[body.Length + TrailerLength];
        body.CopyTo(buffer, 0);
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(body.Length), MessageType);
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(body.Length + MessageTypeLength), sequenceNumber);

        return buffer;
    }

    /// <inheritdoc/>
    public bool TryUnprotect(ReadOnlySpan<byte> buffer, out byte[] message)
    {
        message = [];
        if (buffer.Length < MacLength + TrailerLength)
        {
            return false;
        }

        var sequenceNumber = receiving.SequenceNumber++;
        var trailer = buffer[^TrailerLength..];
        var body = buffer[..^TrailerLength];
        var isInSequence = BinaryPrimitives.ReadUInt16BigEndian(trailer) == MessageType
            & BinaryPrimitives.ReadUInt32BigEndian(trailer[MessageTypeLength..]) == sequenceNumber;
        if (!isInSequence || !TryOpen(body, out var opened))
        {
            return false;
        }

        var clear = opened.AsSpan(0, opened.Length - MacLength);
        if (!CryptographicOperations.FixedTimeEquals(ComputeMac(receiving, sequenceNumber, clear), opened.AsSpan(clear.Length)))
        {
            return false;
        }

        message = clear.ToArray();
        return true;
    }

    private static DigestMd5SecurityLayer? Create(byte[] sessionKey, DigestMd5Response response, bool isAcceptor) =>
        response.Qop switch
        {
            DigestMd5Response.Integrity => new DigestMd5SecurityLayer(sessionKey, null, isAcceptor),
            DigestMd5Response.Confidentiality => new DigestMd5SecurityLayer(sessionKey, response.Cipher, isAcceptor),
            _ => null,
        };

    // The first ten bytes of HMAC-MD5(Ki, SeqNum || message).
    private static byte[] ComputeMac(Direction direction, uint sequenceNumber, ReadOnlySpan<byte> message)
    {
        var signed = new byte[SequenceNumberLength + message.Length];
        BinaryPrimitives.WriteUInt32BigEndian(signed, sequenceNumber);
        message.CopyTo(signed.AsSpan(SequenceNumberLength));

        return HMACMD5.HashData(direction.IntegrityKey, signed)[..MacLength];
    }

    // The message and MAC a buffer's body carries: in clear with integrity, decrypted with confidentiality.
    private bool TryOpen(ReadOnlySpan<byte> body, out byte[] opened)
    {
        if (receiving.Sealing is { } sealing)
        {
            return sealing.TryOpen(body, out opened);
        }

        opened = body.ToArray();
        return true;
    }

    // One direction's keys and state: Ki, the cipher keyed with Kc for auth-conf, and the next sequence number.
    private sealed class Direction
    {
        public Direction(byte[] sessionKey, string? cipher, string mode)
        {
            IntegrityKey = MD5.HashData([.. sessionKey, .. Encoding.ASCII.GetBytes($"Digest session key to {mode} signing key magic constant")]);
            if (cipher is not null)
            {
                var confidentialityKey = MD5.HashData([.. sessionKey, .. Encoding.ASCII.GetBytes($"Digest H(A1) to {mode} sealing key magic constant")]);
                Sealing = cipher == DigestMd5Response.Rc4Cipher
                    ? new Rc4Sealing(confidentialityKey)
                    : new TripleDesSealing(confidentialityKey);
            }
        }

        public byte[] IntegrityKey { get; }

        public Sealing? Sealing { get; }

        public uint SequenceNumber { get; set; }
    }

    // Encrypts a message and its MAC together, and decrypts them back.
    private abstract class Sealing
    {
        public abstract byte[] Seal(ReadOnlySpan<byte> message, ReadOnlySpan<byte> mac);

        // The message then the MAC, false when the body cannot be one this cipher made.
        public abstract bool TryOpen(ReadOnlySpan<byte> body, out byte[] opened);
    }

    // rc4: one keystream, kept running across messages; no padding.
    private sealed class Rc4Sealing(byte[] key) : Sealing
    {
        private readonly Rc4 keyStream = new(key, 0);

        public override byte[] Seal(ReadOnlySpan<byte> message, ReadOnlySpan<byte> mac)
        {
            byte[] sealedBody = [.. message, .. mac];
            keyStream.ApplyKeyStream(sealedBody, sealedBody);

            return sealedBody;
        }

        public override bool TryOpen(ReadOnlySpan<byte> body, out byte[] opened)
        {
            opened = body.ToArray();
            keyStream.ApplyKeyStream(opened, opened);

            return true;
        }
    }

    // 3des: two-key triple DES in CBC mode, the IV chained across messages, padding before the MAC.
    private sealed class TripleDesSealing : Sealing
    {
        private const int BlockLength = 8;

        private const int DesKeyLength = 7;

        private readonly byte[] tripleDesKey;
        private byte[] initializationVector;

        public TripleDesSealing(byte[] key)
        {
            // Two-key triple DES written as K1, K2, K1: the BCL's one-shot CBC refuses a 16-byte key on Windows.
            var firstKey = SpreadDesKey(key.AsSpan(0, DesKeyLength));
            tripleDesKey = [.. firstKey, .. SpreadDesKey(key.AsSpan(DesKeyLength, DesKeyLength)), .. firstKey];
            initializationVector = key[BlockLength..];
        }

        public override byte[] Seal(ReadOnlySpan<byte> message, ReadOnlySpan<byte> mac)
        {
            var padding = BlockLength - ((message.Length + MacLength) % BlockLength);
            byte[] plain = [.. message, .. Enumerable.Repeat((byte)padding, padding), .. mac];
            using var cipher = CreateCipher();
            var sealedBody = cipher.EncryptCbc(plain, initializationVector, PaddingMode.None);
            initializationVector = sealedBody[^BlockLength..];

            return sealedBody;
        }

        // A body is whole blocks of at least 16 bytes, as the outer length check leaves it 10 or more.
        public override bool TryOpen(ReadOnlySpan<byte> body, out byte[] opened)
        {
            opened = [];
            if (body.Length % BlockLength != 0)
            {
                return false;
            }

            using var cipher = CreateCipher();
            var plain = cipher.DecryptCbc(body, initializationVector, PaddingMode.None);
            initializationVector = body[^BlockLength..].ToArray();
            var padding = plain[^(MacLength + 1)];
            var messageLength = plain.Length - MacLength - padding;
            if (!IsPadding(plain, messageLength, padding))
            {
                return false;
            }

            opened = [.. plain.AsSpan(0, messageLength), .. plain.AsSpan(plain.Length - MacLength)];
            return true;
        }

        // 1 to 8 bytes after the message, each holding the count.
        private static bool IsPadding(byte[] plain, int messageLength, byte padding) =>
            padding is >= 1 and <= BlockLength
            && messageLength >= 0
            && !plain.AsSpan(messageLength, padding).ContainsAnyExcept(padding);

        private TripleDES CreateCipher()
        {
            var cipher = TripleDES.Create();
            cipher.Key = tripleDesKey;

            return cipher;
        }

        // Seven key bytes spread over eight, seven bits to a byte, the low bit of each left for parity.
        private static byte[] SpreadDesKey(ReadOnlySpan<byte> key)
        {
            var spread = new byte[BlockLength];
            spread[0] = key[0];
            for (var index = 1; index < DesKeyLength; index++)
            {
                spread[index] = (byte)((key[index - 1] << (8 - index)) | (key[index] >> index));
            }

            spread[DesKeyLength] = (byte)(key[DesKeyLength - 1] << 1);
            return spread;
        }
    }
}
