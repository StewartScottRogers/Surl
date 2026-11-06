namespace Surl.Dtls;

/// <summary>
/// Reads what the server sent without its keys: each record's header, and for plaintext epoch 0
/// records their content.
/// </summary>
internal static class UnitTestRecords
{
    public static List<(DtlsRecordHeader Header, byte[] Fragment)> Split(byte[] datagram)
    {
        var records = new List<(DtlsRecordHeader, byte[])>();
        var remaining = datagram.AsSpan();
        while (DtlsRecordHeader.TryRead(remaining, out var header))
        {
            records.Add((header, remaining.Slice(DtlsRecordHeader.Size, header.Length).ToArray()));
            remaining = remaining[(DtlsRecordHeader.Size + header.Length)..];
        }

        return records;
    }

    /// <summary>The handshake message types the epoch 0 handshake records of a datagram start with.</summary>
    public static List<DtlsHandshakeType> HandshakeTypes(byte[] datagram) =>
        [.. Split(datagram)
            .Where(record => record.Header.Epoch == 0 && record.Header.ContentType == DtlsContentType.Handshake)
            .Select(record => (DtlsHandshakeType)record.Fragment[0])];

    /// <summary>How many sent datagrams begin a <c>ServerHello</c> flight.</summary>
    public static int ServerHelloFlights(IEnumerable<byte[]> sent) =>
        sent.Count(datagram => HandshakeTypes(datagram).FirstOrDefault() == DtlsHandshakeType.ServerHello);

    /// <summary>The alert a datagram holding one plaintext alert record carries.</summary>
    public static DtlsAlert PlaintextAlert(byte[] datagram)
    {
        var (header, fragment) = Split(datagram).Single();
        Assert.AreEqual(DtlsContentType.Alert, header.ContentType);
        Assert.IsTrue(DtlsAlert.TryRead(fragment, out var alert));
        return alert;
    }
}
