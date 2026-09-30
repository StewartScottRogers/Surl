using System.Buffers.Binary;

namespace Surl.Kerberos;

/// <summary>
/// Makes the RFC 4121 section 4.2 tokens a client, the initiator, sends: wrap tokens with and
/// without confidentiality and with any right rotation count (key usage 24), and MIC tokens (key
/// usage 25), under the context key.
/// </summary>
internal sealed class InitiatorTokens(KerberosEncryptionType encryptionType, byte[] contextKey)
{
    private readonly KerberosEncryptionProfile profile = KerberosEncryptionProfile.For(encryptionType);

    public static byte[] Header(byte[] tokenId, byte flags, ushort extraCount, ushort rightRotationCount, ulong sequenceNumber)
    {
        byte[] header = new byte[16];
        tokenId.CopyTo(header, 0);
        header[2] = flags;
        header[3] = 0xFF;
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(4), extraCount);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(6), rightRotationCount);
        BinaryPrimitives.WriteUInt64BigEndian(header.AsSpan(8), sequenceNumber);
        return header;
    }

    public static byte[] RotateRight(byte[] data, int count)
    {
        count %= data.Length;
        return [.. data[^count..], .. data[..^count]];
    }

    public byte[] WrapSigned(byte[] message, ulong sequenceNumber, ushort rightRotationCount = 0, byte flags = 0x00)
    {
        byte[] checkedHeader = Header([0x05, 0x04], flags, 0, 0, sequenceNumber);
        byte[] checksum = profile.ComputeChecksum(contextKey, 24, [.. message, .. checkedHeader]);
        byte[] header = Header([0x05, 0x04], flags, (ushort)checksum.Length, rightRotationCount, sequenceNumber);
        return [.. header, .. RotateRight([.. message, .. checksum], rightRotationCount)];
    }

    public byte[] WrapSealed(byte[] message, ulong sequenceNumber, ushort extraCount = 0, ushort rightRotationCount = 0)
    {
        byte[] encryptedHeader = Header([0x05, 0x04], 0x02, extraCount, 0, sequenceNumber);
        byte[] filler = Enumerable.Repeat((byte)0xEE, extraCount).ToArray();
        byte[] cipherText = profile.Encrypt(contextKey, 24, ApRequestBuilder.Confounder, [.. message, .. filler, .. encryptedHeader]);
        byte[] header = Header([0x05, 0x04], 0x02, extraCount, rightRotationCount, sequenceNumber);
        return [.. header, .. RotateRight(cipherText, rightRotationCount)];
    }

    public byte[] Mic(byte[] message, ulong sequenceNumber, byte flags = 0x00)
    {
        byte[] header = Header([0x04, 0x04], flags, 0xFFFF, 0xFFFF, sequenceNumber);
        return [.. header, .. profile.ComputeChecksum(contextKey, 25, [.. message, .. header])];
    }
}
