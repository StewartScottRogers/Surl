using Surl.Protocol.Abstractions;

namespace Surl.Dtls;

/// <summary>
/// The records of one server association while it handshakes and after: epoch 0 and, once keys
/// exist, epoch 1 in each direction side by side, because a retransmitted final flight carries
/// both (RFC 6347 section 4.2.4). It keeps the server's last flight and sends it again whole, each
/// record with a fresh sequence number, packed into datagrams of at most
/// <see cref="DtlsHandshakeFragmenter.MaximumDatagramLength"/> bytes (ADR-0087 decision 7). It does
/// not own the protections it is given.
/// </summary>
/// <param name="flow">The association's datagram flow.</param>
internal sealed class DtlsAssociationRecords(IDatagramFlow flow)
{
    private readonly DtlsRecordReader epoch0Reader = new(epoch: 0, protection: null);
    private readonly List<FlightRecord> flight = [];
    private DtlsRecordReader? epoch1Reader;
    private DtlsRecordWriter epoch0Writer = new(epoch: 0, protection: null);
    private DtlsRecordWriter? epoch1Writer;

    /// <summary>
    /// Restarts epoch 0's sequence numbers at <paramref name="nextSequenceNumber"/>, the verified
    /// hello's, as the stateless server's first flight continues from it.
    /// </summary>
    /// <param name="nextSequenceNumber">The next epoch 0 sequence number.</param>
    public void ContinueEpoch0WritesFrom(long nextSequenceNumber) =>
        epoch0Writer = new DtlsRecordWriter(epoch: 0, protection: null, nextSequenceNumber);

    /// <summary>Accepts epoch 1 records under the client's protection, beside epoch 0's.</summary>
    /// <param name="protection">The client's write protection.</param>
    public void StartProtectedReads(DtlsRecordProtection protection) => epoch1Reader = new DtlsRecordReader(epoch: 1, protection);

    /// <summary>Sends every later record in epoch 1 under the server's protection.</summary>
    /// <param name="protection">The server's write protection.</param>
    public void StartProtectedWrites(DtlsRecordProtection protection) => epoch1Writer = new DtlsRecordWriter(epoch: 1, protection);

    /// <summary>
    /// Reads every record of a datagram in the epoch it names, dropping silently those of an epoch
    /// not (or no longer) read and every invalid one (RFC 6347 section 4.1.2.7).
    /// </summary>
    /// <param name="datagram">A datagram from the client.</param>
    /// <returns>
    /// The records accepted, in order, read one at a time as the caller takes them, so a
    /// <c>ChangeCipherSpec</c> it acts on starts epoch 1 reads before the <c>Finished</c> behind it
    /// in the same datagram is read.
    /// </returns>
    public IEnumerable<DtlsRecord> Read(ReadOnlyMemory<byte> datagram)
    {
        while (DtlsRecordHeader.TryRead(datagram.Span, out var header) && datagram.Length - DtlsRecordHeader.Size >= header.Length)
        {
            var length = DtlsRecordHeader.Size + header.Length;
            var reader = header.Epoch == 0 ? epoch0Reader : epoch1Reader;
            foreach (var record in reader?.ReadDatagram(datagram.Span[..length]).Records ?? [])
            {
                yield return record;
            }

            datagram = datagram[length..];
        }
    }

    /// <summary>Forgets the last flight; the next one is added message by message.</summary>
    public void StartFlight() => flight.Clear();

    /// <summary>
    /// Adds a handshake message to the flight in the current write epoch, fragmented so each record
    /// fits one datagram.
    /// </summary>
    /// <param name="message">The whole message.</param>
    public void AddToFlight(DtlsHandshakeMessage message)
    {
        var writer = CurrentWriter;
        var overhead = writer.Protection?.Overhead ?? 0;
        var budget = DtlsHandshakeFragmenter.MaximumDatagramLength - DtlsRecordHeader.Size - overhead;
        foreach (var fragment in DtlsHandshakeFragmenter.Fragment(message, budget))
        {
            flight.Add(new FlightRecord(DtlsContentType.Handshake, writer.Epoch, fragment));
        }
    }

    /// <summary>Adds <c>ChangeCipherSpec</c> to the flight in the current write epoch.</summary>
    public void AddChangeCipherSpecToFlight() =>
        flight.Add(new FlightRecord(DtlsContentType.ChangeCipherSpec, CurrentWriter.Epoch, [1]));

    /// <summary>
    /// Sends the flight, each record sealed afresh, packed into as few datagrams as fit.
    /// </summary>
    /// <param name="cancellationToken">Cuts the sends off.</param>
    /// <returns>A task that completes once every datagram is sent.</returns>
    public async ValueTask SendFlightAsync(CancellationToken cancellationToken)
    {
        var datagram = new List<byte>(DtlsHandshakeFragmenter.MaximumDatagramLength);
        foreach (var entry in flight)
        {
            var record = (entry.Epoch == 0 ? epoch0Writer : epoch1Writer!).Seal(entry.ContentType, entry.Plaintext);
            if (datagram.Count + record.Length > DtlsHandshakeFragmenter.MaximumDatagramLength)
            {
                await flow.SendAsync(datagram.ToArray(), cancellationToken).ConfigureAwait(false);
                datagram.Clear();
            }

            datagram.AddRange(record);
        }

        await flow.SendAsync(datagram.ToArray(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Sends one record in the current write epoch as a datagram of its own.</summary>
    /// <param name="contentType">What the record carries.</param>
    /// <param name="plaintext">The record's plaintext.</param>
    /// <param name="cancellationToken">Cuts the send off.</param>
    /// <returns>A task that completes once the datagram is sent.</returns>
    public ValueTask SendAsync(DtlsContentType contentType, byte[] plaintext, CancellationToken cancellationToken) =>
        flow.SendAsync(CurrentWriter.Seal(contentType, plaintext), cancellationToken);

    private DtlsRecordWriter CurrentWriter => epoch1Writer ?? epoch0Writer;

    private sealed record FlightRecord(DtlsContentType ContentType, ushort Epoch, byte[] Plaintext);
}
