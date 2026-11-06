using System.Net;

namespace Surl.Dtls;

/// <summary>
/// <see cref="DtlsCookieExchange"/>: what is answered with <c>HelloVerifyRequest</c>, what passes,
/// and every datagram ignored without an answer before an address is verified (RFC 6347 section 4.2.1).
/// </summary>
[TestClass]
public sealed class DtlsCookieExchangeTests
{
    private static readonly EndPoint Client = new IPEndPoint(IPAddress.Loopback, 50000);

    [TestMethod]
    public void Answer_HelloWithItsCookie_IsVerifiedWithItsRecordSequenceNumber()
    {
        var secrets = new DtlsCookieSecrets(new ManualTimeProvider());
        var hello = Hello();
        hello = hello with { Cookie = secrets.MakeCookie(Client, hello) };
        var datagram = Record(DtlsContentType.Handshake, 0, Message(DtlsHandshakeType.ClientHello, hello.Write()), sequenceNumber: 3);

        var verified = DtlsCookieExchange.Answer(datagram, Client, secrets, out var answer);

        Assert.IsNull(answer);
        Assert.AreEqual(3, verified!.RecordSequenceNumber);
        CollectionAssert.AreEqual(hello.Cookie, verified.Hello.Cookie);
    }

    [TestMethod]
    [DataRow(0, DisplayName = "Empty datagram")]
    [DataRow(1, DisplayName = "Alert record")]
    [DataRow(2, DisplayName = "Epoch 1 record")]
    [DataRow(3, DisplayName = "Record longer than the datagram")]
    [DataRow(4, DisplayName = "Fragment shorter than its header")]
    [DataRow(5, DisplayName = "Not a ClientHello")]
    [DataRow(6, DisplayName = "Fragment not at offset 0")]
    [DataRow(7, DisplayName = "Fragment shorter than the message")]
    [DataRow(8, DisplayName = "Body shorter than the fragment length")]
    [DataRow(9, DisplayName = "Malformed hello")]
    public void Answer_DatagramThatIsNotAWholeClientHello_IsIgnoredWithoutAnAnswer(int variant)
    {
        var secrets = new DtlsCookieSecrets(new ManualTimeProvider());

        var verified = DtlsCookieExchange.Answer(NotAWholeHello(variant), Client, secrets, out var answer);

        Assert.IsNull(verified);
        Assert.IsNull(answer);
    }

    private static byte[] NotAWholeHello(int variant)
    {
        var body = Hello().Write();
        var hello = Message(DtlsHandshakeType.ClientHello, body);
        return variant switch
        {
            0 => [],
            1 => Record(DtlsContentType.Alert, 0, hello),
            2 => Record(DtlsContentType.Handshake, 1, hello),
            3 => Record(DtlsContentType.Handshake, 0, hello)[..^1],
            4 => Record(DtlsContentType.Handshake, 0, [1, 0, 0]),
            5 => Record(DtlsContentType.Handshake, 0, Message(DtlsHandshakeType.ServerHello, body)),
            6 => Record(DtlsContentType.Handshake, 0, Fragment(body.Length, 1, body.Length - 1, body[1..])),
            7 => Record(DtlsContentType.Handshake, 0, Fragment(body.Length, 0, body.Length - 1, body[..^1])),
            8 => Record(DtlsContentType.Handshake, 0, Fragment(body.Length, 0, body.Length, body[..^1])),
            _ => Record(DtlsContentType.Handshake, 0, Message(DtlsHandshakeType.ClientHello, [.. body, 0])),
        };
    }

    private static DtlsClientHello Hello() => new(DtlsRecordHeader.Dtls12Version, new byte[32], [], [], [0xC02B], [0], []);

    private static byte[] Message(DtlsHandshakeType type, byte[] body) => new DtlsHandshakeMessage(type, 0, body).ToUnfragmentedBytes();

    private static byte[] Fragment(int length, int offset, int fragmentLength, byte[] bytes)
    {
        var fragment = new byte[DtlsHandshakeHeader.Size + bytes.Length];
        new DtlsHandshakeHeader(DtlsHandshakeType.ClientHello, length, 0, offset, fragmentLength).Write(fragment);
        bytes.CopyTo(fragment, DtlsHandshakeHeader.Size);
        return fragment;
    }

    private static byte[] Record(DtlsContentType type, ushort epoch, byte[] plaintext, long sequenceNumber = 0)
    {
        var record = new byte[DtlsRecordHeader.Size + plaintext.Length];
        new DtlsRecordHeader(type, DtlsRecordHeader.Dtls12Version, epoch, sequenceNumber, (ushort)plaintext.Length).Write(record);
        plaintext.CopyTo(record, DtlsRecordHeader.Size);
        return record;
    }
}
