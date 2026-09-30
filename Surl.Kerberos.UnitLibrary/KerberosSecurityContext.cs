using System.Buffers.Binary;
using System.Formats.Asn1;

namespace Surl.Kerberos;

/// <summary>
/// One accepted Kerberos login (ADR-0057 decision 5): the client it names, whether it asked for
/// mutual authentication, the AP-REP that proves surl held the service key, and the RFC 4121
/// section 4.2 wrap and MIC tokens exchanged under the context key. One context serves one
/// connection; it is not safe to use from several threads at once.
/// </summary>
public sealed class KerberosSecurityContext
{
    private const int ApReplyKeyUsage = 12;
    private const int AcceptorSealKeyUsage = 22;
    private const int AcceptorSignKeyUsage = 23;
    private const int InitiatorSealKeyUsage = 24;
    private const int InitiatorSignKeyUsage = 25;
    private const byte SentByAcceptorFlag = 0x01;
    private const byte SealedFlag = 0x02;
    private const byte AcceptorSubkeyFlag = 0x04;
    private const int TokenHeaderLength = 16;
    private const int WrapFillerLength = 1;
    private const int MicFillerLength = 5;
    private const ushort MicCountFields = 0xFFFF;

    private static readonly byte[] WrapTokenId = [0x05, 0x04];
    private static readonly byte[] MicTokenId = [0x04, 0x04];

    private readonly KerberosSessionKeys keys;
    private readonly DateTimeOffset clientTime;
    private readonly int clientMicroseconds;
    private readonly uint acceptorInitialSequenceNumber;
    private readonly IKerberosRandomSource randomSource;
    private ulong nextAcceptorSequenceNumber;
    private ulong nextClientSequenceNumber;

    /// <summary>Initialises the context <see cref="KerberosAcceptor" /> accepted.</summary>
    /// <param name="clientPrincipal">The ticket's client.</param>
    /// <param name="isMutualAuthenticationRequested">Whether the AP-REQ's <c>ap-options</c> had <c>mutual-required</c>.</param>
    /// <param name="keys">The session key and the context key.</param>
    /// <param name="authenticator">The accepted authenticator: its time and the client's first sequence number.</param>
    /// <param name="acceptorInitialSequenceNumber">Surl's first sequence number: the AP-REP's when one is sent, else the client's, as MIT krb5 does.</param>
    /// <param name="randomSource">Where the AP-REP's confounder comes from.</param>
    internal KerberosSecurityContext(
        KerberosPrincipalName clientPrincipal,
        bool isMutualAuthenticationRequested,
        KerberosSessionKeys keys,
        KerberosAuthenticatorPart authenticator,
        uint acceptorInitialSequenceNumber,
        IKerberosRandomSource randomSource)
    {
        ClientPrincipal = clientPrincipal;
        IsMutualAuthenticationRequested = isMutualAuthenticationRequested;
        this.keys = keys;
        clientTime = authenticator.ClientTime;
        clientMicroseconds = authenticator.ClientMicroseconds;
        this.acceptorInitialSequenceNumber = acceptorInitialSequenceNumber;
        this.randomSource = randomSource;
        nextAcceptorSequenceNumber = acceptorInitialSequenceNumber;
        nextClientSequenceNumber = authenticator.SequenceNumber;
    }

    /// <summary>Gets the client the ticket names, such as <c>user@EXAMPLE.COM</c>.</summary>
    public KerberosPrincipalName ClientPrincipal { get; }

    /// <summary>Gets whether the client asked for an AP-REP: the AP-REQ's <c>ap-options</c> had <c>mutual-required</c>.</summary>
    public bool IsMutualAuthenticationRequested { get; }

    /// <summary>
    /// Makes the AP-REP token (ADR-0057 decision 5): an <c>EncAPRepPart</c> holding the
    /// authenticator's <c>ctime</c> and <c>cusec</c>, no subkey, and surl's sequence number,
    /// encrypted under the ticket's session key with key usage 12, then framed under the Kerberos
    /// OID with <c>TOK_ID</c> <c>02 00</c>.
    /// </summary>
    /// <returns>The token.</returns>
    /// <exception cref="InvalidOperationException">The client did not ask for mutual authentication.</exception>
    public byte[] CreateApRepToken()
    {
        if (!IsMutualAuthenticationRequested)
        {
            throw new InvalidOperationException("The client did not ask for mutual authentication, so no AP-REP is sent.");
        }

        byte[] confounder = new byte[KerberosEncryptionProfile.ConfounderLength];
        randomSource.Fill(confounder);
        byte[] cipherText = keys.SessionProfile.Encrypt(keys.SessionKey, ApReplyKeyUsage, confounder, WriteEncApRepPart());
        return GssApiToken.Frame(GssApiToken.ApReplyTokenId, WriteApRep(cipherText));
    }

    /// <summary>
    /// Wraps <paramref name="message" /> in an RFC 4121 wrap token without confidentiality:
    /// <c>TOK_ID</c> <c>05 04</c>, <c>SentByAcceptor</c>, a right rotation count of 0, surl's next
    /// sequence number, the message in clear and its checksum under key usage 22.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>The token.</returns>
    public byte[] Wrap(ReadOnlySpan<byte> message)
    {
        byte[] header = WriteHeader(WrapTokenId, extraCount: 0, rightRotationCount: 0, nextAcceptorSequenceNumber++);
        byte[] checksum = keys.ContextProfile.ComputeChecksum(keys.ContextKey, AcceptorSealKeyUsage, [.. message, .. header]);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(4), (ushort)checksum.Length);
        return [.. header, .. message, .. checksum];
    }

    /// <summary>
    /// Reads a client's RFC 4121 wrap token, with or without confidentiality and with any right
    /// rotation count: it must not be flagged <c>SentByAcceptor</c> or <c>AcceptorSubkey</c>, must
    /// carry the client's next sequence number, and must pass its check under key usage 24.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="message">The message, or empty when the token is refused.</param>
    /// <returns><see langword="true" /> when the token is the client's next one and intact.</returns>
    public bool TryUnwrap(ReadOnlySpan<byte> token, out byte[] message)
    {
        message = [];
        if (!IsClientsNextToken(token, WrapTokenId, WrapFillerLength))
        {
            return false;
        }

        byte[] header = token[..TokenHeaderLength].ToArray();
        int extraCount = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4));
        byte[] data = RotateLeft(token[TokenHeaderLength..], BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(6)));
        bool isIntact = (header[2] & SealedFlag) != 0
            ? TryOpenSealed(header, data, extraCount, out message)
            : TryOpenSigned(header, data, extraCount, out message);
        nextClientSequenceNumber += isIntact ? 1UL : 0UL;
        return isIntact;
    }

    /// <summary>
    /// Makes an RFC 4121 MIC token for <paramref name="message" />: <c>TOK_ID</c> <c>04 04</c>,
    /// <c>SentByAcceptor</c>, surl's next sequence number and the checksum under key usage 23.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>The token.</returns>
    public byte[] GetMic(ReadOnlySpan<byte> message)
    {
        byte[] header = WriteHeader(MicTokenId, MicCountFields, MicCountFields, nextAcceptorSequenceNumber++);
        byte[] checksum = keys.ContextProfile.ComputeChecksum(keys.ContextKey, AcceptorSignKeyUsage, [.. message, .. header]);
        return [.. header, .. checksum];
    }

    /// <summary>
    /// Checks a client's RFC 4121 MIC token for <paramref name="message" />: not flagged
    /// <c>SentByAcceptor</c> or <c>AcceptorSubkey</c>, the client's next sequence number, and the
    /// checksum under key usage 25.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="token">The token.</param>
    /// <returns><see langword="true" /> when the token is the client's next one and matches.</returns>
    public bool VerifyMic(ReadOnlySpan<byte> message, ReadOnlySpan<byte> token)
    {
        if (!IsClientsNextToken(token, MicTokenId, MicFillerLength)
            || !keys.ContextProfile.VerifyChecksum(keys.ContextKey, InitiatorSignKeyUsage, [.. message, .. token[..TokenHeaderLength]], token[TokenHeaderLength..]))
        {
            return false;
        }

        nextClientSequenceNumber++;
        return true;
    }

    private static byte[] WriteHeader(ReadOnlySpan<byte> tokenId, ushort extraCount, ushort rightRotationCount, ulong sequenceNumber)
    {
        byte[] header = new byte[TokenHeaderLength];
        tokenId.CopyTo(header);
        header[2] = SentByAcceptorFlag;
        header[3] = 0xFF;
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(4), extraCount);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(6), rightRotationCount);
        BinaryPrimitives.WriteUInt64BigEndian(header.AsSpan(8), sequenceNumber);
        return header;
    }

    // Undoes the sender's right rotation of the data after the header (RFC 4121 section 4.2.5).
    private static byte[] RotateLeft(ReadOnlySpan<byte> data, int rightRotationCount)
    {
        int count = data.Length == 0 ? 0 : rightRotationCount % data.Length;
        return [.. data[count..], .. data[..count]];
    }

    private bool IsClientsNextToken(ReadOnlySpan<byte> token, byte[] tokenId, int fillerLength) =>
        token.Length >= TokenHeaderLength
        && token.StartsWith(tokenId)
        && (token[2] & (SentByAcceptorFlag | AcceptorSubkeyFlag)) == 0
        && token.Slice(3, fillerLength).IndexOfAnyExcept((byte)0xFF) < 0
        && BinaryPrimitives.ReadUInt64BigEndian(token[8..]) == nextClientSequenceNumber;

    // Without confidentiality the data are the message and its checksum, EC bytes long, taken
    // over the message and the header with EC and RRC zero (RFC 4121 section 4.2.4).
    private bool TryOpenSigned(byte[] header, byte[] data, int extraCount, out byte[] message)
    {
        message = [];
        if (extraCount != keys.ContextProfile.HmacLength || data.Length < extraCount)
        {
            return false;
        }

        byte[] checkedHeader = [.. header];
        checkedHeader.AsSpan(4, 4).Clear();
        byte[] signedMessage = data[..^extraCount];
        if (!keys.ContextProfile.VerifyChecksum(keys.ContextKey, InitiatorSealKeyUsage, [.. signedMessage, .. checkedHeader], data.AsSpan(data.Length - extraCount)))
        {
            return false;
        }

        message = signedMessage;
        return true;
    }

    // With confidentiality the data decrypt to the message, EC filler bytes and a copy of the
    // header, whose TOK_ID, flags, filler and sequence number must match the one sent in clear.
    private bool TryOpenSealed(byte[] header, byte[] data, int extraCount, out byte[] message)
    {
        message = [];
        if (!keys.ContextProfile.TryDecrypt(keys.ContextKey, InitiatorSealKeyUsage, data, out byte[] plainText)
            || plainText.Length < extraCount + TokenHeaderLength)
        {
            return false;
        }

        ReadOnlySpan<byte> headerCopy = plainText.AsSpan(plainText.Length - TokenHeaderLength);
        if (!headerCopy[..4].SequenceEqual(header.AsSpan(0, 4)) || !headerCopy[8..].SequenceEqual(header.AsSpan(8)))
        {
            return false;
        }

        message = plainText[..^(extraCount + TokenHeaderLength)];
        return true;
    }

    // EncAPRepPart ::= [APPLICATION 27] SEQUENCE { ctime [0], cusec [1], subkey [2] OPTIONAL,
    //   seq-number [3] OPTIONAL }
    private byte[] WriteEncApRepPart()
    {
        AsnWriter writer = new(KerberosDer.Rules);
        using (writer.PushSequence(KerberosDer.ApplicationTag(27)))
        using (writer.PushSequence())
        {
            using (writer.PushSequence(KerberosDer.ContextTag(0)))
            {
                writer.WriteGeneralizedTime(clientTime, omitFractionalSeconds: true);
            }

            WriteIntegerField(writer, 1, clientMicroseconds);
            WriteIntegerField(writer, 3, acceptorInitialSequenceNumber);
        }

        return writer.Encode();
    }

    // AP-REP ::= [APPLICATION 15] SEQUENCE { pvno [0] 5, msg-type [1] 15, enc-part [2]
    //   EncryptedData { etype [0], cipher [2] } }
    private byte[] WriteApRep(byte[] cipherText)
    {
        AsnWriter writer = new(KerberosDer.Rules);
        using (writer.PushSequence(KerberosDer.ApplicationTag(15)))
        using (writer.PushSequence())
        {
            WriteIntegerField(writer, 0, 5);
            WriteIntegerField(writer, 1, 15);
            using (writer.PushSequence(KerberosDer.ContextTag(2)))
            using (writer.PushSequence())
            {
                WriteIntegerField(writer, 0, (int)keys.SessionProfile.EncryptionType);
                using (writer.PushSequence(KerberosDer.ContextTag(2)))
                {
                    writer.WriteOctetString(cipherText);
                }
            }
        }

        return writer.Encode();
    }

    private static void WriteIntegerField(AsnWriter writer, int number, long value)
    {
        using (writer.PushSequence(KerberosDer.ContextTag(number)))
        {
            writer.WriteInteger(value);
        }
    }
}
