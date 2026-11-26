using System.Buffers;

namespace Surl.LineProtocol;

/// <summary>MQTT <c>CONNECT</c> passwords sent through a tunnel, as the exchange log shows them (ADR-0182, amendment 10).</summary>
[TestClass]
public sealed class TunnelMqttConnectRedactorTests
{
    // What pinned upstream curl 8.21.0 sent for `-u tester:secret mqtt://...`
    // (Surl.Protocol.Mqtt.UnitTests/Fixtures/connect-user-and-password): CONNECT, then SUBSCRIBE.
    private static readonly byte[] CurlConnect =
        [0x10, 0x28, .. Text("MQTT"), 0x04, 0xC2, 0x00, 0x3C, .. Text("curlAqUlJVUw"), .. Text("tester"), .. Text("secret")];

    private static readonly byte[] CurlConnectMasked =
        [0x10, 0x26, .. Text("MQTT"), 0x04, 0xC2, 0x00, 0x3C, .. Text("curlAqUlJVUw"), .. Text("tester"), .. Text("****")];

    private static readonly byte[] CurlSubscribe = [0x82, 0x06, 0x00, 0x01, 0x00, 0x01, (byte)'t', 0x00];

    [TestMethod]
    public void Mqtt311Connect_AsCurlSendsIt_IsLoggedWithThePasswordMasked() =>
        TunnelRedactionAssert.AssertLoggedHoweverSplit(
            [.. CurlConnectMasked, .. CurlSubscribe],
            [.. CurlConnect, .. CurlSubscribe]);

    [TestMethod]
    public void Mqtt5Connect_WithPropertiesAndWill_IsLoggedWithOnlyThePasswordMasked()
    {
        byte[] fields =
        [
            5, 0x11, 0x00, 0x00, 0x00, 0x3C, // Session Expiry Interval
            .. Text("cid"),
            2, 0x01, 0x01, // Will properties
            .. Text("w/topic"),
            .. Text("will-payload"),
            .. Text("alice"),
        ];
        var sent = Connect(5, 0xC6, [.. fields, .. Text("s3cret-pw")]);
        var logged = Connect(5, 0xC6, [.. fields, .. Text("****")]);

        TunnelRedactionAssert.AssertLoggedHoweverSplit([.. logged, 0xE0, 0x00], [.. sent, 0xE0, 0x00]);
    }

    [TestMethod]
    public void Mqtt5Connect_WithPasswordAndNoUserName_IsLoggedMasked() =>
        TunnelRedactionAssert.AssertLoggedHoweverSplit(
            Connect(5, 0x42, [0, .. Text("cid"), .. Text("****")]),
            Connect(5, 0x42, [0, .. Text("cid"), .. Text("s3cret-pw")]));

    [TestMethod]
    public void Connect_WithAnEmptyPassword_IsLoggedAsFourAndTheMask() =>
        CollectionAssert.AreEqual(
            Connect(4, 0x42, [.. Text("cid"), .. Text("****")]),
            TunnelRedactionAssert.Logged(Connect(4, 0x42, [.. Text("cid"), .. Text("")])));

    [TestMethod]
    public void Connect_LongEnoughForATwoByteRemainingLength_KeepsItsFraming()
    {
        byte[] fields = [.. Text("MQTT"), 4, 0x42, 0x00, 0x3C, .. Text(new string('c', 200))];
        byte[] sent = [0x10, 0xDF, 0x01, .. fields, .. Text("s3cret-pw")];
        byte[] logged = [0x10, 0xDA, 0x01, .. fields, .. Text("****")];

        CollectionAssert.AreEqual(logged, TunnelRedactionAssert.Logged(sent));
    }

    [TestMethod]
    public void Connect_WithoutAPassword_IsLoggedAsReceived()
    {
        var connect = Connect(4, 0x82, [.. Text("cid"), .. Text("alice")]);

        TunnelRedactionAssert.AssertLoggedHoweverSplit(connect, connect);
    }

    // Each row is what follows the flags and keep alive of a CONNECT announcing a password.
    [TestMethod]
    [DataRow((byte)4, (byte)0x42, new byte[] { }, DisplayName = "no client identifier")]
    [DataRow((byte)4, (byte)0x42, new byte[] { 0, 9, 99 }, DisplayName = "client identifier cut short")]
    [DataRow((byte)5, (byte)0x42, new byte[] { }, DisplayName = "no properties")]
    [DataRow((byte)5, (byte)0x42, new byte[] { 9, 0x11 }, DisplayName = "properties cut short")]
    [DataRow((byte)4, (byte)0x46, new byte[] { 0, 0, 0, 9, 116 }, DisplayName = "will topic cut short")]
    [DataRow((byte)5, (byte)0x46, new byte[] { 0, 0, 0, 9 }, DisplayName = "will properties cut short")]
    [DataRow((byte)4, (byte)0x46, new byte[] { 0, 0, 0, 0, 0 }, DisplayName = "will payload cut short")]
    [DataRow((byte)4, (byte)0xC2, new byte[] { 0, 0, 0, 9, 97 }, DisplayName = "user name cut short")]
    [DataRow((byte)4, (byte)0x42, new byte[] { 0, 0 }, DisplayName = "no password")]
    [DataRow((byte)4, (byte)0x42, new byte[] { 0, 0, 0, 9, 112 }, DisplayName = "password cut short")]
    [DataRow((byte)4, (byte)0x42, new byte[] { 0, 0, 0, 1, 112, 119 }, DisplayName = "bytes after the password")]
    public void Connect_WhoseFieldsDoNotLeadToThePassword_HasEverythingAfterTheKeepAliveMasked(
        byte level,
        byte flags,
        byte[] afterKeepAlive)
    {
        var sent = Connect(level, flags, afterKeepAlive);
        byte[] logged = [0x10, 14, .. Text("MQTT"), level, flags, 0x00, 0x3C, .. "****"u8];

        CollectionAssert.AreEqual(logged, TunnelRedactionAssert.Logged(sent));
    }

    [TestMethod]
    public void Connect_CutOffInsideThePassword_IsLoggedMasked() =>
        CollectionAssert.AreEqual(
            (byte[])[0x10, 14, .. CurlConnect[2..12], .. "****"u8],
            TunnelRedactionAssert.Logged(CurlConnect[..^3]));

    // Cut off before the keep alive ends, nothing held is a password.
    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(7)]
    [DataRow(11)]
    public void Connect_CutOffBeforeTheKeepAliveEnds_IsLoggedAsReceived(int length) =>
        CollectionAssert.AreEqual(CurlConnect[..length], TunnelRedactionAssert.Logged(CurlConnect[..length]));

    [TestMethod]
    public void MalformedTunnel_ARemainingLengthPastFourBytes_IsLoggedAsReceivedAndNeverHeld()
    {
        byte[] sent = [0x10, 0xFF, 0xFF, 0xFF, 0xFF, .. new byte[TunnelMqttConnectRedactor.MaxHeldBytes + 1]];
        var redactor = new TunnelMqttConnectRedactor();
        var output = new ArrayBufferWriter<byte>();

        redactor.Redact(sent.AsSpan(0, 5), output);
        Assert.AreEqual(5, output.WrittenCount);

        redactor.Redact(sent.AsSpan(5), output);
        CollectionAssert.AreEqual(sent, output.WrittenSpan.ToArray());

        redactor.Flush(output);
        CollectionAssert.AreEqual(sent, output.WrittenSpan.ToArray());
    }

    [TestMethod]
    public void MalformedTunnel_APacketOverTheHeldLimit_IsOneMaskAndNeverHeld()
    {
        // Remaining Length 65,537, followed by that many bytes and one packet more.
        byte[] header = [0x10, 0x81, 0x80, 0x04];
        byte[] body = new byte[65537];
        byte[] later = [0xE0, 0x00];
        var redactor = new TunnelMqttConnectRedactor();
        var output = new ArrayBufferWriter<byte>();

        redactor.Redact(header, output);
        CollectionAssert.AreEqual("****"u8.ToArray(), output.WrittenSpan.ToArray());

        redactor.Redact(body.AsSpan(0, 40000), output);
        redactor.Redact([.. body.AsSpan(40000), .. later], output);
        redactor.Flush(output);

        CollectionAssert.AreEqual((byte[])[.. "****"u8, .. later], output.WrittenSpan.ToArray());
    }

    [TestMethod]
    public void MalformedTunnel_APacketThatIsNoConnect_IsLoggedAsReceived()
    {
        byte[] sent = [0x10, 0x00, .. Wire.Bytes("anything at all")];

        TunnelRedactionAssert.AssertLoggedHoweverSplit(sent, sent);
    }

    [TestMethod]
    public void MqttRedactor_NullOutput_IsRefused()
    {
        var redactor = new TunnelMqttConnectRedactor();

        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Redact([0x10], null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Flush(null!));
    }

    // Two length bytes and the text, as MQTT writes a string.
    private static byte[] Text(string text) => [(byte)(text.Length >> 8), (byte)text.Length, .. Wire.Bytes(text)];

    private static byte[] Connect(byte level, byte flags, params byte[] afterKeepAlive)
    {
        byte[] body = [.. Text("MQTT"), level, flags, 0x00, 0x3C, .. afterKeepAlive];

        return [0x10, (byte)body.Length, .. body];
    }
}
