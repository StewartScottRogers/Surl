using Surl.Protocol.Abstractions;

namespace Surl.Dtls;

/// <summary>
/// The DTLS 1.2 record layer of one association over its <see cref="IDatagramFlow"/> (ADR-0087
/// decision 8). Both directions start in epoch 0, plaintext, and each moves once to epoch 1
/// under its protection after <c>ChangeCipherSpec</c>; nothing renegotiates, so there is no
/// epoch 2. The layer owns the protections it is given and disposes them.
/// </summary>
/// <param name="flow">The association's datagram flow.</param>
public sealed class DtlsRecordLayer(IDatagramFlow flow) : IDisposable
{
    private DtlsRecordReader reader = new(epoch: 0, protection: null);
    private DtlsRecordWriter writer = new(epoch: 0, protection: null);

    /// <summary>The epoch whose records are accepted.</summary>
    public ushort ReadEpoch => reader.Epoch;

    /// <summary>The epoch records are sent in.</summary>
    public ushort WriteEpoch => writer.Epoch;

    /// <summary>The sequence number the next record sent will carry.</summary>
    public long NextWriteSequenceNumber => writer.NextSequenceNumber;

    /// <summary>
    /// Moves reading to epoch 1 under <paramref name="protection"/>, after the peer's
    /// <c>ChangeCipherSpec</c>. Epoch 0 records are dropped from then on.
    /// </summary>
    /// <param name="protection">The peer's write protection.</param>
    /// <exception cref="InvalidOperationException">Reading has already moved to epoch 1.</exception>
    public void StartProtectedReadEpoch(DtlsRecordProtection protection)
    {
        ArgumentNullException.ThrowIfNull(protection);
        ThrowIfProtected(reader.Epoch);
        reader = new DtlsRecordReader(epoch: 1, protection);
    }

    /// <summary>
    /// Moves sending to epoch 1 under <paramref name="protection"/>, after sending
    /// <c>ChangeCipherSpec</c>; its sequence numbers start again at 0.
    /// </summary>
    /// <param name="protection">This side's write protection.</param>
    /// <exception cref="InvalidOperationException">Sending has already moved to epoch 1.</exception>
    public void StartProtectedWriteEpoch(DtlsRecordProtection protection)
    {
        ArgumentNullException.ThrowIfNull(protection);
        ThrowIfProtected(writer.Epoch);
        writer = new DtlsRecordWriter(epoch: 1, protection);
    }

    /// <summary>
    /// Reads the records of the datagram that opened the flow.
    /// </summary>
    /// <returns>The records accepted and the reasons the others were dropped.</returns>
    public DtlsDatagramRecords ReadFirstDatagram() => reader.ReadDatagram(flow.FirstDatagram.Span);

    /// <summary>
    /// Waits for the next datagram and reads its records.
    /// </summary>
    /// <param name="cancellationToken">Cuts the wait off.</param>
    /// <returns>The records accepted and the reasons the others were dropped.</returns>
    public async ValueTask<DtlsDatagramRecords> ReceiveAsync(CancellationToken cancellationToken)
    {
        var datagram = await flow.ReceiveAsync(cancellationToken).ConfigureAwait(false);
        return reader.ReadDatagram(datagram.Span);
    }

    /// <summary>
    /// Writes one record in the current write epoch, for the caller to pack into a datagram.
    /// </summary>
    /// <param name="contentType">What the record carries.</param>
    /// <param name="plaintext">At most 2^14 bytes.</param>
    /// <returns>The whole record.</returns>
    /// <exception cref="DtlsSequenceNumberExhaustedException">The epoch's last sequence number is used.</exception>
    public byte[] SealRecord(DtlsContentType contentType, ReadOnlySpan<byte> plaintext) =>
        writer.Seal(contentType, plaintext);

    /// <summary>
    /// Sends one record in the current write epoch as a datagram of its own.
    /// </summary>
    /// <param name="contentType">What the record carries.</param>
    /// <param name="plaintext">At most 2^14 bytes.</param>
    /// <param name="cancellationToken">Cuts the send off.</param>
    /// <returns>A task that completes once the datagram has been sent.</returns>
    /// <exception cref="DtlsSequenceNumberExhaustedException">The epoch's last sequence number is used.</exception>
    public ValueTask SendRecordAsync(
        DtlsContentType contentType, ReadOnlyMemory<byte> plaintext, CancellationToken cancellationToken) =>
        flow.SendAsync(writer.Seal(contentType, plaintext.Span), cancellationToken);

    /// <summary>
    /// Disposes the protections the layer was given. The flow stays the caller's.
    /// </summary>
    public void Dispose()
    {
        reader.Protection?.Dispose();
        writer.Protection?.Dispose();
    }

    private static void ThrowIfProtected(ushort epoch)
    {
        if (epoch != 0)
        {
            throw new InvalidOperationException("DTLS here moves to epoch 1 once and never renegotiates.");
        }
    }
}
