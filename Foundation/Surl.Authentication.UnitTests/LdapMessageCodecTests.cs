using System.Formats.Asn1;

namespace Surl.Authentication;

/// <summary>
/// <see cref="LdapMessageCodec"/> on the messages it writes and on answers a directory might send,
/// well formed or not.
/// </summary>
[TestClass]
public sealed class LdapMessageCodecTests
{
    private static Task<LdapResponse> ReadAsync(byte[] bytes) =>
        LdapMessageCodec.ReadAsync(new MemoryStream(bytes), CancellationToken.None).AsTask();

    private static byte[] WithLongFormLength(byte[] message, int lengthByteCount)
    {
        var body = message[2..];
        var length = new byte[] { (byte)(0x80 | lengthByteCount) }
            .Concat(Enumerable.Range(0, lengthByteCount).Select(i => (byte)(body.Length >> (8 * (lengthByteCount - 1 - i)))));

        return [0x30, .. length, .. body];
    }

    [TestMethod]
    public async Task Read_ShortFormResult_IsKindCodeAndId()
    {
        var response = await ReadAsync(LdapTestMessages.BindResponse(4, 49));

        Assert.AreEqual(new LdapResponse(4, LdapResponseKind.Bind, 49, null), response);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public async Task Read_LongFormLength_IsRead(int lengthByteCount)
    {
        var response = await ReadAsync(WithLongFormLength(LdapTestMessages.SearchDone(2, 0), lengthByteCount));

        Assert.AreEqual(LdapResponseKind.SearchDone, response.Kind);
    }

    [TestMethod]
    public async Task Read_Entry_CarriesItsName()
    {
        var response = await ReadAsync(LdapTestMessages.SearchEntry(2, "uid=a"));

        Assert.AreEqual("uid=a", response.EntryName);
        Assert.AreEqual(LdapResponseKind.SearchEntry, response.Kind);
    }

    [TestMethod]
    public async Task Read_Reference_AndExtendedResponse_AreTheirKinds()
    {
        Assert.AreEqual(LdapResponseKind.SearchReference, (await ReadAsync(LdapTestMessages.SearchReference(2))).Kind);
        Assert.AreEqual(LdapResponseKind.Extended, (await ReadAsync(LdapTestMessages.ExtendedResponse(1, 0))).Kind);
    }

    [TestMethod]
    public async Task Read_ControlsAfterTheOperation_AreIgnored()
    {
        var message = LdapTestMessages.Message(1, writer =>
        {
            using (writer.PushSequence(new Asn1Tag(TagClass.Application, 1, true)))
            {
                writer.WriteEnumeratedValue(LdapResultCode.Success);
                writer.WriteOctetString([]);
                writer.WriteOctetString([]);
            }

            using var controls = writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0, true));
        });

        Assert.AreEqual(0, (await ReadAsync(message)).ResultCode);
    }

    [TestMethod]
    public async Task Read_LengthAbove64KiB_IsRefusedBeforeTheBodyIsRead()
    {
        var head = new byte[] { 0x30, 0x83, 0x01, 0x00, 0x01 };

        var failure = await Assert.ThrowsExactlyAsync<LdapCheckRefusedException>(() => ReadAsync(head));

        Assert.AreEqual("malformed answer", failure.Reason);
    }

    [TestMethod]
    public async Task Read_LengthOf64KiB_IsReadNotRefused()
    {
        var head = new byte[] { 0x30, 0x83, 0x01, 0x00, 0x00 };

        await Assert.ThrowsExactlyAsync<EndOfStreamException>(() => ReadAsync(head));
    }

    [TestMethod]
    [DataRow(new byte[] { 0x31, 0x00 }, DisplayName = "not a sequence")]
    [DataRow(new byte[] { 0x30, 0x80, 0x00, 0x00 }, DisplayName = "an indefinite length")]
    [DataRow(new byte[] { 0x30, 0x84, 0x00, 0x00, 0x00, 0x01, 0x00 }, DisplayName = "four length bytes")]
    [DataRow(new byte[] { 0x30, 0x00 }, DisplayName = "an empty message")]
    [DataRow(new byte[] { 0x30, 0x03, 0x02, 0x01, 0x01 }, DisplayName = "no operation")]
    [DataRow(new byte[] { 0x30, 0x07, 0x02, 0x01, 0x01, 0x67, 0x02, 0x04, 0x00 }, DisplayName = "an operation the source never reads")]
    [DataRow(new byte[] { 0x30, 0x05, 0x02, 0x01, 0x01, 0x04, 0x00 }, DisplayName = "an operation that is not application-tagged")]
    [DataRow(new byte[] { 0x30, 0x08, 0x02, 0x01, 0x01, 0x61, 0x03, 0x04, 0x01, 0x30 }, DisplayName = "a result code of the wrong type")]
    [DataRow(new byte[] { 0x30, 0x0C, 0x02, 0x05, 0x01, 0x00, 0x00, 0x00, 0x00, 0x61, 0x03, 0x0A, 0x01, 0x00 }, DisplayName = "a message id beyond 32 bits")]
    [DataRow(new byte[] { 0x30, 0x0C, 0x02, 0x01, 0x01, 0x61, 0x07, 0x0A, 0x05, 0x01, 0x00, 0x00, 0x00, 0x00 }, DisplayName = "a result code beyond 32 bits")]
    public async Task Read_Malformed_IsRefused(byte[] bytes)
    {
        var failure = await Assert.ThrowsExactlyAsync<LdapCheckRefusedException>(() => ReadAsync(bytes));

        Assert.AreEqual("malformed answer", failure.Reason);
    }

    [TestMethod]
    public async Task Read_TruncatedMessage_EndsTheStream()
    {
        var message = LdapTestMessages.BindResponse(1, 0);

        await Assert.ThrowsExactlyAsync<EndOfStreamException>(() => ReadAsync(message[..^2]));
    }

    [TestMethod]
    public void StartTlsRequest_IsTheExtendedRequestOfRfc4511()
    {
        Assert.AreEqual(
            "301D0201017718" + "8016" + Convert.ToHexString("1.3.6.1.4.1.1466.20037"u8),
            Convert.ToHexString(LdapMessageCodec.StartTlsRequest(1)));
    }

    [TestMethod]
    public void UnbindRequest_IsTheNullOperation()
    {
        Assert.AreEqual("3005020102" + "4200", Convert.ToHexString(LdapMessageCodec.UnbindRequest(2)));
    }

    [TestMethod]
    public void BindRequest_IsASimpleVersion3Bind()
    {
        Assert.AreEqual(
            "300F020103600A0201030402636E800170",
            Convert.ToHexString(LdapMessageCodec.BindRequest(3, "cn", "p"u8.ToArray())));
    }
}
