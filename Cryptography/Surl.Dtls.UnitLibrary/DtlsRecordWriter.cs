namespace Surl.Dtls;

/// <summary>
/// Writes the records of one epoch: numbers each record from 0 up to 2^48 - 1 and protects it
/// when the epoch has protection (epoch 0 has none; RFC 6347 section 4.1).
/// </summary>
public sealed class DtlsRecordWriter
{
    /// <summary>The most plaintext one record carries: 2^14 bytes (RFC 5246 section 6.2.1).</summary>
    public const int MaximumPlaintextLength = 1 << 14;

    private readonly DtlsRecordProtection? protection;

    /// <summary>
    /// Creates the writer of <paramref name="epoch"/>, starting at sequence number 0.
    /// </summary>
    /// <param name="epoch">The epoch every record carries.</param>
    /// <param name="protection">The epoch's protection, or <see langword="null"/> for plaintext records.</param>
    public DtlsRecordWriter(ushort epoch, DtlsRecordProtection? protection)
        : this(epoch, protection, nextSequenceNumber: 0)
    {
    }

    /// <summary>
    /// Creates the writer of <paramref name="epoch"/> whose next record is
    /// <paramref name="nextSequenceNumber"/>, so tests reach the end of the sequence space.
    /// </summary>
    internal DtlsRecordWriter(ushort epoch, DtlsRecordProtection? protection, long nextSequenceNumber)
    {
        Epoch = epoch;
        this.protection = protection;
        NextSequenceNumber = nextSequenceNumber;
    }

    /// <summary>The epoch every record carries.</summary>
    public ushort Epoch { get; }

    /// <summary>The sequence number the next record will carry.</summary>
    public long NextSequenceNumber { get; private set; }

    /// <summary>The epoch's protection, or <see langword="null"/> when its records are plaintext.</summary>
    internal DtlsRecordProtection? Protection => protection;

    /// <summary>
    /// Writes one record carrying <paramref name="plaintext"/>: the header, then the plaintext or,
    /// under protection, the explicit nonce, ciphertext and tag.
    /// </summary>
    /// <param name="contentType">What the record carries.</param>
    /// <param name="plaintext">At most <see cref="MaximumPlaintextLength"/> bytes.</param>
    /// <returns>The whole record.</returns>
    /// <exception cref="ArgumentException"><paramref name="plaintext"/> is longer than one record carries.</exception>
    /// <exception cref="DtlsSequenceNumberExhaustedException">The epoch's last sequence number is used.</exception>
    public byte[] Seal(DtlsContentType contentType, ReadOnlySpan<byte> plaintext)
    {
        if (plaintext.Length > MaximumPlaintextLength)
        {
            throw new ArgumentException($"A record carries at most {MaximumPlaintextLength} bytes.", nameof(plaintext));
        }

        if (NextSequenceNumber > DtlsRecordHeader.MaximumSequenceNumber)
        {
            throw new DtlsSequenceNumberExhaustedException(Epoch);
        }

        var header = new DtlsRecordHeader(
            contentType, DtlsRecordHeader.Dtls12Version, Epoch, NextSequenceNumber, Length: 0);
        var fragment = protection is null ? plaintext.ToArray() : protection.Seal(header, plaintext);

        var record = new byte[DtlsRecordHeader.Size + fragment.Length];
        (header with { Length = (ushort)fragment.Length }).Write(record);
        fragment.CopyTo(record, DtlsRecordHeader.Size);
        NextSequenceNumber++;
        return record;
    }
}
