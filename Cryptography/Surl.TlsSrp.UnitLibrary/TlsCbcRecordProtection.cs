using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Surl.TlsSrp;

/// <summary>
/// One direction's protection of TLS 1.2 records after <c>ChangeCipherSpec</c> for RFC 5054's CBC
/// suites (ADR-0122 decisions 2 and 3): HMAC-SHA1 and a CBC block cipher with an explicit IV per
/// record (RFC 5246 section 6.2.3.2), either MAC-then-encrypt (RFC 5246) or encrypt-then-MAC
/// (RFC 7366). It keeps the direction's 64-bit sequence number, which every MAC covers, so a
/// replayed, reordered or dropped record fails its MAC. A bad MAC and bad padding are refused alike,
/// with <see cref="TlsAlertDescription.BadRecordMac"/>.
/// </summary>
public sealed class TlsCbcRecordProtection : IDisposable
{
    private readonly SymmetricAlgorithm cipher;
    private readonly byte[] macKey;
    private readonly RandomNumberGenerator ivSource;
    private readonly int blockLength;

    /// <summary>Creates one direction's protection with its sequence number at zero.</summary>
    /// <param name="bulkCipher">The suite's block cipher.</param>
    /// <param name="encryptionKey">The direction's <c>write_key</c>.</param>
    /// <param name="macKey">The direction's <c>write_MAC_key</c>: 20 bytes.</param>
    /// <param name="encryptThenMac">Whether RFC 7366's encrypt-then-MAC was negotiated.</param>
    /// <param name="ivSource">Where explicit IVs come from: <see cref="RandomNumberGenerator.Create()"/> in production.</param>
    public TlsCbcRecordProtection(
        TlsBulkCipher bulkCipher, byte[] encryptionKey, byte[] macKey, bool encryptThenMac, RandomNumberGenerator ivSource)
    {
        ArgumentNullException.ThrowIfNull(encryptionKey);
        ArgumentNullException.ThrowIfNull(macKey);
        ArgumentNullException.ThrowIfNull(ivSource);

        cipher = bulkCipher == TlsBulkCipher.TripleDesEdeCbc ? TripleDES.Create() : Aes.Create();
        cipher.Key = encryptionKey;
        this.macKey = macKey;
        this.ivSource = ivSource;
        EncryptThenMac = encryptThenMac;
        blockLength = TlsSrpCipherSuites.BlockLengthOf(bulkCipher);
    }

    /// <summary>Whether records are encrypt-then-MAC (RFC 7366) rather than MAC-then-encrypt.</summary>
    public bool EncryptThenMac { get; }

    /// <summary>The sequence number the next record protected or unprotected will carry in its MAC.</summary>
    public ulong SequenceNumber { get; private set; }

    /// <summary>Protects one record's plaintext and advances the sequence number.</summary>
    /// <param name="contentType">The record's content type, which the MAC covers.</param>
    /// <param name="version">The record header's version, which the MAC covers.</param>
    /// <param name="plaintext">The fragment to protect; the writer keeps it to 2^14 bytes.</param>
    /// <returns>The <c>TLSCiphertext.fragment</c>: the IV, the ciphertext and, for encrypt-then-MAC, the MAC.</returns>
    public byte[] Protect(TlsContentType contentType, ushort version, ReadOnlySpan<byte> plaintext)
    {
        var iv = new byte[blockLength];
        ivSource.GetBytes(iv);
        var fragment = EncryptThenMac
            ? ProtectEncryptThenMac(contentType, version, plaintext, iv)
            : ProtectMacThenEncrypt(contentType, version, plaintext, iv);
        SequenceNumber++;
        return fragment;
    }

    /// <summary>Removes one record's protection and advances the sequence number.</summary>
    /// <param name="contentType">The record header's content type, which the MAC covers.</param>
    /// <param name="version">The record header's version, which the MAC covers.</param>
    /// <param name="fragment">The <c>TLSCiphertext.fragment</c>.</param>
    /// <returns>The plaintext fragment.</returns>
    /// <exception cref="TlsAlertException">
    /// <see cref="TlsAlertDescription.BadRecordMac"/> for a bad length, MAC or padding;
    /// <see cref="TlsAlertDescription.RecordOverflow"/> for plaintext over 2^14 bytes.
    /// </exception>
    public byte[] Unprotect(TlsContentType contentType, ushort version, ReadOnlySpan<byte> fragment)
    {
        var plaintext = EncryptThenMac
            ? UnprotectEncryptThenMac(contentType, version, fragment)
            : UnprotectMacThenEncrypt(contentType, version, fragment);
        if (plaintext.Length > TlsRecordFormat.MaxPlaintextLength)
        {
            throw new TlsAlertException(
                TlsAlertDescription.RecordOverflow, $"A record's plaintext is {plaintext.Length} bytes, over 2^14.");
        }

        SequenceNumber++;
        return plaintext;
    }

    /// <inheritdoc />
    public void Dispose() => cipher.Dispose();

    private byte[] ProtectMacThenEncrypt(TlsContentType contentType, ushort version, ReadOnlySpan<byte> plaintext, byte[] iv)
    {
        var mac = Mac(contentType, version, plaintext);
        var padded = TlsCbcPadding.Pad([.. plaintext, .. mac], blockLength);
        return [.. iv, .. cipher.EncryptCbc(padded, iv, PaddingMode.None)];
    }

    private byte[] ProtectEncryptThenMac(TlsContentType contentType, ushort version, ReadOnlySpan<byte> plaintext, byte[] iv)
    {
        byte[] body = [.. iv, .. cipher.EncryptCbc(TlsCbcPadding.Pad(plaintext, blockLength), iv, PaddingMode.None)];
        return [.. body, .. Mac(contentType, version, body)];
    }

    private byte[] UnprotectMacThenEncrypt(TlsContentType contentType, ushort version, ReadOnlySpan<byte> fragment)
    {
        const int macLength = TlsSrpCipherSuites.MacLength;
        var ciphertextLength = fragment.Length - blockLength;
        if (ciphertextLength < macLength + 1 || ciphertextLength % blockLength != 0)
        {
            throw BadRecordMac($"A MAC-then-encrypt record of {fragment.Length} bytes cannot hold an IV, a MAC and padding.");
        }

        var padded = cipher.DecryptCbc(fragment[blockLength..], fragment[..blockLength], PaddingMode.None);
        var contentLength = TlsCbcPadding.MeasureContent(padded, macLength, out var paddingIsValid);
        var expectedMac = Mac(contentType, version, padded.AsSpan(0, contentLength));
        var macIsValid = CryptographicOperations.FixedTimeEquals(expectedMac, padded.AsSpan(contentLength, macLength));
        if (!(macIsValid & paddingIsValid))
        {
            throw BadRecordMac("A MAC-then-encrypt record's MAC or padding is wrong.");
        }

        return padded[..contentLength];
    }

    private byte[] UnprotectEncryptThenMac(TlsContentType contentType, ushort version, ReadOnlySpan<byte> fragment)
    {
        var bodyLength = fragment.Length - TlsSrpCipherSuites.MacLength;
        if (bodyLength < 2 * blockLength || bodyLength % blockLength != 0)
        {
            throw BadRecordMac($"An encrypt-then-MAC record of {fragment.Length} bytes cannot hold an IV, a block and a MAC.");
        }

        var body = fragment[..bodyLength];
        if (!CryptographicOperations.FixedTimeEquals(Mac(contentType, version, body), fragment[bodyLength..]))
        {
            throw BadRecordMac("An encrypt-then-MAC record's MAC is wrong.");
        }

        var padded = cipher.DecryptCbc(body[blockLength..], body[..blockLength], PaddingMode.None);
        var contentLength = TlsCbcPadding.MeasureContent(padded, 0, out var paddingIsValid);
        if (!paddingIsValid)
        {
            throw BadRecordMac("An encrypt-then-MAC record's padding is wrong.");
        }

        return padded[..contentLength];
    }

    /// <summary>
    /// <c>HMAC_SHA1(MAC_write_key, seq_num + type + version + length + content)</c>
    /// (RFC 5246 section 6.2.3.1; RFC 7366 section 3 MACs the IV and ciphertext as the content).
    /// </summary>
    private byte[] Mac(TlsContentType contentType, ushort version, ReadOnlySpan<byte> content)
    {
        var input = new byte[13 + content.Length];
        BinaryPrimitives.WriteUInt64BigEndian(input, SequenceNumber);
        input[8] = (byte)contentType;
        BinaryPrimitives.WriteUInt16BigEndian(input.AsSpan(9), version);
        BinaryPrimitives.WriteUInt16BigEndian(input.AsSpan(11), (ushort)content.Length);
        content.CopyTo(input.AsSpan(13));
        return HMACSHA1.HashData(macKey, input);
    }

    private static TlsAlertException BadRecordMac(string message) => new(TlsAlertDescription.BadRecordMac, message);
}
