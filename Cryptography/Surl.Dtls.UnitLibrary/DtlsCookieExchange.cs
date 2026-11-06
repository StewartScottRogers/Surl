using System.Net;

namespace Surl.Dtls;

/// <summary>
/// A <c>ClientHello</c> whose cookie verified, with what the handshake carries on from it: the
/// whole message for the handshake hash, and its record sequence number, which the server's
/// first flight continues from.
/// </summary>
/// <param name="Hello">The hello.</param>
/// <param name="Message">The whole message, as the handshake hash covers it.</param>
/// <param name="RecordSequenceNumber">The sequence number of the record that carried it.</param>
internal sealed record DtlsVerifiedClientHello(DtlsClientHello Hello, DtlsHandshakeMessage Message, long RecordSequenceNumber);

/// <summary>
/// The stateless cookie exchange (RFC 6347 section 4.2.1, ADR-0087 decision 4): a datagram whose
/// first record is a whole epoch 0 <c>ClientHello</c> either carries a cookie the secrets made for
/// this endpoint and these hello fields, or is answered with a <c>HelloVerifyRequest</c>. Nothing
/// is kept between the two: the <c>HelloVerifyRequest</c> repeats the hello's record sequence
/// number and <c>message_seq</c>. Anything else - another content type or epoch, a fragmented or
/// malformed hello - is ignored without an answer, since no address is verified yet.
/// </summary>
internal static class DtlsCookieExchange
{
    /// <summary>
    /// Reads the first record of <paramref name="datagram"/> as a <c>ClientHello</c> and checks its cookie.
    /// </summary>
    /// <param name="datagram">A datagram from the client.</param>
    /// <param name="remoteEndPoint">Where it came from.</param>
    /// <param name="secrets">The listener's cookie secrets.</param>
    /// <param name="helloVerifyRequest">The datagram to answer with, or <see langword="null"/>.</param>
    /// <returns>The verified hello, or <see langword="null"/> when there is none.</returns>
    public static DtlsVerifiedClientHello? Answer(
        ReadOnlySpan<byte> datagram, EndPoint remoteEndPoint, DtlsCookieSecrets secrets, out byte[]? helloVerifyRequest)
    {
        helloVerifyRequest = null;
        if (!TryReadClientHello(datagram, out var record, out var message, out var hello))
        {
            return null;
        }

        if (secrets.IsValid(remoteEndPoint, hello))
        {
            return new DtlsVerifiedClientHello(hello, message, record.SequenceNumber);
        }

        var request = new DtlsHelloVerifyRequest(
            DtlsHelloVerifyRequest.Dtls10Version, secrets.MakeCookie(remoteEndPoint, hello));
        var requestMessage = new DtlsHandshakeMessage(DtlsHandshakeType.HelloVerifyRequest, message.MessageSeq, request.Write());
        helloVerifyRequest = new DtlsRecordWriter(epoch: 0, protection: null, record.SequenceNumber)
            .Seal(DtlsContentType.Handshake, requestMessage.ToUnfragmentedBytes());
        return null;
    }

    private static bool TryReadClientHello(
        ReadOnlySpan<byte> datagram, out DtlsRecordHeader record, out DtlsHandshakeMessage message, out DtlsClientHello hello)
    {
        message = null!;
        hello = null!;
        if (!DtlsRecordHeader.TryRead(datagram, out record) || !IsWholePlaintextHandshakeRecord(record, datagram.Length))
        {
            return false;
        }

        var fragment = datagram.Slice(DtlsRecordHeader.Size, record.Length);
        if (!DtlsHandshakeHeader.TryRead(fragment, out var header) || !IsWholeClientHello(header, fragment.Length))
        {
            return false;
        }

        message = new DtlsHandshakeMessage(
            DtlsHandshakeType.ClientHello, header.MessageSeq, fragment.Slice(DtlsHandshakeHeader.Size, header.Length).ToArray());
        try
        {
            hello = DtlsClientHello.Read(message.Body);
            return true;
        }
        catch (DtlsDecodeErrorException)
        {
            return false;
        }
    }

    private static bool IsWholePlaintextHandshakeRecord(DtlsRecordHeader record, int datagramLength) =>
        record.ContentType == DtlsContentType.Handshake
        && record.Epoch == 0
        && datagramLength - DtlsRecordHeader.Size >= record.Length;

    private static bool IsWholeClientHello(DtlsHandshakeHeader header, int fragmentLength) =>
        header.MessageType == DtlsHandshakeType.ClientHello
        && header.FragmentOffset == 0
        && header.FragmentLength == header.Length
        && fragmentLength - DtlsHandshakeHeader.Size >= header.Length;
}
