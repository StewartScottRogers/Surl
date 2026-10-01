using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Surl.Cryptography.Rc4;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// NTLM's SASL security layer after an LDAP <c>NTLM</c> or <c>GSS-SPNEGO</c> bind (ADR-0072,
/// decision 4): [MS-NLMP] section 3.4 with extended session security. Each direction has its
/// own signing key, its own RC4 handle keyed with its sealing key and kept running across
/// messages, and its own sequence number from 0 (sections 3.4.5.2 and 3.4.5.3). A protected
/// buffer is the 16-byte signature - version 1, the first eight bytes of
/// <c>HMAC_MD5(SigningKey, SeqNum || Message)</c>, RC4-encrypted with the direction's handle under
/// key exchange, and the sequence number (section 3.4.4.2) - then the message, sealed with the
/// same handle when <c>NTLMSSP_NEGOTIATE_SEAL</c> was negotiated (section 3.4.3) and in clear when
/// only <c>NTLMSSP_NEGOTIATE_SIGN</c> was.
/// </summary>
internal sealed class NtlmSecurityLayer : ISaslSecurityLayer
{
    /// <summary>
    /// The length of an NTLM signature, which opens every protected buffer.
    /// </summary>
    public const int SignatureLength = 16;

    private const uint SignatureVersion = 1;

    private const int ChecksumLength = 8;

    private const int SequenceNumberOffset = 12;

    private const int Key128Length = 16;

    private const int Key56Length = 7;

    private const int Key40Length = 5;

    private const string ClientToServer = "client-to-server";

    private const string ServerToClient = "server-to-client";

    private readonly bool isSealed;
    private readonly bool isKeyExchange;
    private readonly Direction sending;
    private readonly Direction receiving;

    private NtlmSecurityLayer(NtlmSessionKey sessionKey, bool isAcceptor)
    {
        var flags = sessionKey.NegotiateFlags;
        isSealed = flags.HasFlag(NtlmNegotiateFlags.Seal);
        isKeyExchange = flags.HasFlag(NtlmNegotiateFlags.KeyExchange);

        var client = new Direction(sessionKey.ExportedSessionKey, flags, ClientToServer);
        var server = new Direction(sessionKey.ExportedSessionKey, flags, ServerToClient);
        (sending, receiving) = isAcceptor ? (server, client) : (client, server);
    }

    /// <summary>
    /// NTLM has no buffer limit of its own: the server's <c>--max-message</c> bounds the buffer.
    /// </summary>
    public int MaximumProtectedBytes => int.MaxValue;

    /// <summary>
    /// Whether <paramref name="flags"/> negotiate a layer: sealing or signing.
    /// </summary>
    /// <param name="flags">The <c>AUTHENTICATE_MESSAGE</c>'s flags.</param>
    /// <returns><see langword="true"/> when messages after the login are protected.</returns>
    public static bool IsNegotiated(NtlmNegotiateFlags flags) =>
        (flags & (NtlmNegotiateFlags.Seal | NtlmNegotiateFlags.Sign)) != 0;

    /// <summary>
    /// The server's layer for <paramref name="sessionKey"/>: it protects with the server-to-client
    /// keys and unprotects with the client-to-server ones.
    /// </summary>
    /// <param name="sessionKey">The accepted login's session key and flags, which negotiate a layer with extended session security.</param>
    /// <returns>The layer.</returns>
    public static NtlmSecurityLayer ForAcceptor(NtlmSessionKey sessionKey) => new(sessionKey, isAcceptor: true);

    /// <summary>
    /// The client's layer for <paramref name="sessionKey"/>, the mirror of <see cref="ForAcceptor"/>:
    /// what upstream curl's <c>WinLDAP</c> runs, so a test can play the other side.
    /// </summary>
    /// <param name="sessionKey">The login's session key and flags.</param>
    /// <returns>The layer.</returns>
    public static NtlmSecurityLayer ForInitiator(NtlmSessionKey sessionKey) => new(sessionKey, isAcceptor: false);

    /// <inheritdoc/>
    public byte[] Protect(ReadOnlySpan<byte> message)
    {
        var buffer = new byte[SignatureLength + message.Length];
        var body = buffer.AsSpan(SignatureLength);
        if (isSealed)
        {
            sending.Handle.ApplyKeyStream(message, body);
        }
        else
        {
            message.CopyTo(body);
        }

        Sign(sending, message, buffer);

        return buffer;
    }

    /// <inheritdoc/>
    public bool TryUnprotect(ReadOnlySpan<byte> buffer, out byte[] message)
    {
        message = [];
        if (buffer.Length < SignatureLength)
        {
            return false;
        }

        var body = buffer[SignatureLength..].ToArray();
        if (isSealed)
        {
            receiving.Handle.ApplyKeyStream(body, body);
        }

        var expected = new byte[SignatureLength];
        Sign(receiving, body, expected);
        if (!CryptographicOperations.FixedTimeEquals(expected, buffer[..SignatureLength]))
        {
            return false;
        }

        message = body;
        return true;
    }

    // MAC(Handle, SigningKey, SeqNum, Message) with extended session security, written to the
    // first 16 bytes of destination; the direction's sequence number then moves on.
    private void Sign(Direction direction, ReadOnlySpan<byte> message, Span<byte> destination)
    {
        Span<byte> sequenceNumber = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(sequenceNumber, direction.SequenceNumber++);
        byte[] signed = [.. sequenceNumber, .. message];
        var checksum = HMACMD5.HashData(direction.SigningKey, signed).AsSpan(0, ChecksumLength);

        BinaryPrimitives.WriteUInt32LittleEndian(destination, SignatureVersion);
        if (isKeyExchange)
        {
            direction.Handle.ApplyKeyStream(checksum, destination.Slice(4, ChecksumLength));
        }
        else
        {
            checksum.CopyTo(destination[4..]);
        }

        sequenceNumber.CopyTo(destination[SequenceNumberOffset..]);
    }

    // One direction's keys and state: SIGNKEY and SEALKEY of section 3.4.5.2 and 3.4.5.3, the RC4
    // handle keyed with the sealing key, and the next sequence number.
    private sealed class Direction
    {
        public Direction(byte[] exportedSessionKey, NtlmNegotiateFlags flags, string mode)
        {
            SigningKey = MD5.HashData([.. exportedSessionKey, .. MagicConstant($"session key to {mode} signing key magic constant")]);
            var sealingKey = MD5.HashData(
                [.. exportedSessionKey.AsSpan(0, SealingKeyLength(flags)), .. MagicConstant($"session key to {mode} sealing key magic constant")]);
            Handle = new Rc4(sealingKey, 0);
        }

        public byte[] SigningKey { get; }

        public Rc4 Handle { get; }

        public uint SequenceNumber { get; set; }

        private static int SealingKeyLength(NtlmNegotiateFlags flags) => flags switch
        {
            _ when flags.HasFlag(NtlmNegotiateFlags.Negotiate128) => Key128Length,
            _ when flags.HasFlag(NtlmNegotiateFlags.Negotiate56) => Key56Length,
            _ => Key40Length,
        };

        // The constants are ASCII and end in a NUL, which the hash covers.
        private static byte[] MagicConstant(string text) => Encoding.ASCII.GetBytes(text + "\0");
    }
}
