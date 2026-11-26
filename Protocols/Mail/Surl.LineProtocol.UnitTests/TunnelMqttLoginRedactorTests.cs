using System.Buffers;

namespace Surl.LineProtocol;

/// <summary>
/// MQTT <c>CONNECT</c> passwords and MQTT 5 Authentication Data sent through a tunnel, as the exchange
/// log shows them (ADR-0182, amendments 10 and 28).
/// </summary>
[TestClass]
public sealed class TunnelMqttLoginRedactorTests
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
        byte[] sent = [0x10, 0xFF, 0xFF, 0xFF, 0xFF, .. new byte[TunnelMqttLoginRedactor.MaxHeldBytes + 1]];
        var redactor = new TunnelMqttLoginRedactor();
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
        var redactor = new TunnelMqttLoginRedactor();
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
        var redactor = new TunnelMqttLoginRedactor();

        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Redact([0x10], null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Flush(null!));
    }

    // MQTT 5.0 section 3.1: Authentication Method (0x15) and Authentication Data (0x16) among the
    // CONNECT properties. Pinned upstream curl speaks 3.1.1 only, so the bytes are the specification's.
    [TestMethod]
    public void Mqtt5Connect_WithAuthenticationData_IsLoggedWithItMaskedHoweverSplit()
    {
        byte[] sent = Connect(
            5,
            0x02,
            [36, .. Method, 0x16, .. Text("client-first"), 0x11, 0x00, 0x00, 0x00, 0x3C, .. Text("cid")]);
        byte[] logged = Connect(
            5,
            0x02,
            [28, .. Method, 0x16, .. Text("****"), 0x11, 0x00, 0x00, 0x00, 0x3C, .. Text("cid")]);

        TunnelRedactionAssert.AssertLoggedHoweverSplit([.. logged, .. Publish], [.. sent, .. Publish]);
    }

    [TestMethod]
    public void Mqtt5Connect_WithAuthenticationDataAndPassword_IsLoggedWithBothMasked() =>
        TunnelRedactionAssert.AssertLoggedHoweverSplit(
            Connect(5, 0x42, [7, 0x16, .. Text("****"), .. Text("cid"), .. Text("****")]),
            Connect(5, 0x42, [12, 0x16, .. Text("client-fi"), .. Text("cid"), .. Text("s3cret-pw")]));

    // Not a Will property (table 2-4), so only a client crafting its own bytes puts it there.
    [TestMethod]
    public void Mqtt5Connect_WithAuthenticationDataAmongTheWillProperties_IsLoggedWithItMasked() =>
        TunnelRedactionAssert.AssertLoggedHoweverSplit(
            Connect(5, 0x06, [0, .. Text("cid"), 7, 0x16, .. Text("****"), .. Text("t"), .. Text("w")]),
            Connect(5, 0x06, [0, .. Text("cid"), 6, 0x16, .. Text("abc"), .. Text("t"), .. Text("w")]));

    // An identifier written in two bytes is malformed (section 1.5.5), and still names the secret.
    [TestMethod]
    public void Mqtt5Connect_WithAnOverlongAuthenticationDataIdentifier_IsLoggedWithItMasked() =>
        CollectionAssert.AreEqual(
            Connect(5, 0x02, [7, 0x16, .. Text("****"), .. Text("cid")]),
            TunnelRedactionAssert.Logged(Connect(5, 0x02, [7, 0x96, 0x00, .. Text("abc"), .. Text("cid")])));

    // One property of each value type table 2-4 has, then the secret.
    [TestMethod]
    public void Mqtt5Connect_WithAPropertyOfEveryValueType_FindsTheAuthenticationDataAfterThem()
    {
        byte[] others =
        [
            0x17, 1, // Byte
            0x21, 0, 20, // Two Byte Integer
            0x27, 0, 1, 0, 0, // Four Byte Integer
            0x0B, 0x81, 0x01, // Variable Byte Integer
            0x26, .. Text("k"), .. Text("v"), // UTF-8 String Pair
            .. Method, // UTF-8 String
        ];
        var sent = Connect(5, 0x02, [(byte)(others.Length + 6), .. others, 0x16, .. Text("abc"), .. Text("cid")]);
        var logged = Connect(5, 0x02, [(byte)(others.Length + 7), .. others, 0x16, .. Text("****"), .. Text("cid")]);

        CollectionAssert.AreEqual(logged, TunnelRedactionAssert.Logged(sent));
    }

    [TestMethod]
    public void Mqtt5Connect_WithoutASecret_IsLoggedAsReceived()
    {
        var connect = Connect(5, 0x82, [.. new byte[] { 5, 0x11, 0, 0, 0, 60 }, .. Text("cid"), .. Text("alice")]);

        TunnelRedactionAssert.AssertLoggedHoweverSplit(connect, connect);
    }

    // Each row is what follows the flags and keep alive of an MQTT 5 CONNECT announcing no password.
    [TestMethod]
    [DataRow(new byte[] { }, DisplayName = "no properties")]
    [DataRow(new byte[] { 9, 0x16, 0, 3, 97 }, DisplayName = "properties cut short")]
    [DataRow(new byte[] { 4, 0x7F, 0x16, 0, 0, 0, 0 }, DisplayName = "an unknown property")]
    [DataRow(new byte[] { 1, 0x80, 0, 0 }, DisplayName = "an identifier cut short")]
    [DataRow(new byte[] { 3, 0x16, 0, 9, 0, 0 }, DisplayName = "a value past the property list")]
    [DataRow(new byte[] { 2, 0x11, 0, 0, 0 }, DisplayName = "an integer past the property list")]
    [DataRow(new byte[] { 4, 0x26, 0, 0, 0, 0, 0 }, DisplayName = "half a string pair")]
    [DataRow(new byte[] { 2, 0x26, 0, 0, 0 }, DisplayName = "a string pair's name cut short")]
    [DataRow(new byte[] { 0, 0, 9, 99 }, DisplayName = "client identifier cut short")]
    [DataRow(new byte[] { 0, 0, 0, 0x16, 0, 1, 97 }, DisplayName = "bytes after the last field")]
    public void Mqtt5Connect_WhoseFieldsDoNotLeadToItsEnd_HasEverythingAfterTheKeepAliveMasked(byte[] afterKeepAlive)
    {
        byte[] logged = [0x10, 14, .. Text("MQTT"), 5, 0x02, 0x00, 0x3C, .. "****"u8];

        CollectionAssert.AreEqual(logged, TunnelRedactionAssert.Logged(Connect(5, 0x02, afterKeepAlive)));
    }

    // MQTT 5.0 section 3.15: a reason code (0x18, Continue authentication), then the properties.
    [TestMethod]
    public void Mqtt5Auth_AfterTheConnect_IsLoggedWithTheAuthenticationDataMaskedHoweverSplit()
    {
        var connect = Connect(5, 0x02, [16, .. Method, .. Text("cid")]);
        byte[] auth = [0xF0, 33, 0x18, 31, .. Method, 0x16, .. Text("client-final")];
        byte[] logged = [0xF0, 25, 0x18, 23, .. Method, 0x16, .. Text("****")];

        TunnelRedactionAssert.AssertLoggedHoweverSplit(
            [.. connect, .. logged, .. Publish, .. logged],
            [.. connect, .. auth, .. Publish, .. auth]);
    }

    [TestMethod]
    [DataRow(new byte[] { 0xF0, 0 }, DisplayName = "no reason code")]
    [DataRow(new byte[] { 0xF0, 1, 0x19 }, DisplayName = "a reason code alone")]
    [DataRow(new byte[] { 0xF0, 2, 0x00, 0 }, DisplayName = "no properties")]
    [DataRow(new byte[] { 0xF0, 6, 0x18, 4, 0x15, 0, 1, 65 }, DisplayName = "no Authentication Data")]
    public void Mqtt5Auth_WithoutAuthenticationData_IsLoggedAsReceived(byte[] auth)
    {
        byte[] sent = [.. Connect(5, 0x02, [0, .. Text("cid")]), .. auth, .. Publish];

        TunnelRedactionAssert.AssertLoggedHoweverSplit(sent, sent);
    }

    [TestMethod]
    [DataRow(new byte[] { 0xF0, 5, 0x18, 9, 0x16, 0, 1 }, DisplayName = "properties cut short")]
    [DataRow(new byte[] { 0xF0, 4, 0x18, 2, 0x7F, 0 }, DisplayName = "an unknown property")]
    [DataRow(new byte[] { 0xF0, 6, 0x18, 0, 0x16, 0, 1, 97 }, DisplayName = "bytes after the properties")]
    public void Mqtt5Auth_WhosePropertiesDoNotEndThePacket_IsLoggedWithItsWholeBodyMasked(byte[] auth)
    {
        var connect = Connect(5, 0x02, [0, .. Text("cid")]);

        CollectionAssert.AreEqual(
            (byte[])[.. connect, 0xF0, 4, .. "****"u8, .. Publish],
            TunnelRedactionAssert.Logged([.. connect, .. auth, .. Publish]));
    }

    [TestMethod]
    public void Mqtt5Auth_CutOffInsideTheAuthenticationData_IsLoggedMasked()
    {
        var connect = Connect(5, 0x02, [0, .. Text("cid")]);
        byte[] auth = [0xF0, 16, 0x18, 14, 0x16, .. Text("client-fin")];

        CollectionAssert.AreEqual(
            (byte[])[.. connect, 0xF0, 4, .. "****"u8],
            TunnelRedactionAssert.Logged([.. connect, .. auth]));
    }

    [TestMethod]
    public void MalformedTunnel_AnAuthPacketOverTheHeldLimit_IsOneMaskAndALaterPublishIsLoggedAsReceived()
    {
        var connect = Connect(5, 0x02, [0, .. Text("cid")]);
        byte[] header = [0xF0, 0x81, 0x80, 0x04]; // Remaining Length 65,537
        var redactor = new TunnelMqttLoginRedactor();
        var output = new ArrayBufferWriter<byte>();
        redactor.Redact(connect, output);

        redactor.Redact(header, output);
        CollectionAssert.AreEqual((byte[])[.. connect, .. "****"u8], output.WrittenSpan.ToArray());

        redactor.Redact(new byte[65537], output);
        redactor.Redact(Publish, output);
        redactor.Flush(output);

        CollectionAssert.AreEqual((byte[])[.. connect, .. "****"u8, .. Publish], output.WrittenSpan.ToArray());
    }

    [TestMethod]
    public void MqttRedactor_APacketOfTheHeldLimit_IsHeldUntilWholeAndOneByteMoreIsNot()
    {
        // 65,532 bytes after a four-byte fixed header make a packet of 64 KiB.
        byte[] atLimit = [0xF0, 0xFC, 0xFF, 0x03, .. new byte[TunnelMqttLoginRedactor.MaxHeldBytes - 4]];
        byte[] overLimit = [0xF0, 0xFD, 0xFF, 0x03];
        var redactor = new TunnelMqttLoginRedactor();
        var output = new ArrayBufferWriter<byte>();

        redactor.Redact(atLimit.AsSpan(0, atLimit.Length - 1), output);
        Assert.AreEqual(0, output.WrittenCount);

        redactor.Redact(atLimit.AsSpan(atLimit.Length - 1), output);
        Assert.AreNotEqual(0, output.WrittenCount);

        output.ResetWrittenCount();
        redactor.Redact(overLimit, output);
        CollectionAssert.AreEqual("****"u8.ToArray(), output.WrittenSpan.ToArray());
    }

    // A PUBLISH is never held, however long it is: each read of it is logged at once.
    [TestMethod]
    public void MqttRedactor_APublishOverTheHeldLimit_IsLoggedAsItArrives()
    {
        var connect = Connect(5, 0x02, [0, .. Text("cid")]);
        byte[] header = [0x30, 0x81, 0x80, 0x04];
        byte[] payload = new byte[65537];
        Array.Fill(payload, (byte)0x16);
        var redactor = new TunnelMqttLoginRedactor();
        var output = new ArrayBufferWriter<byte>();
        redactor.Redact(connect, output);

        redactor.Redact(header, output);
        CollectionAssert.AreEqual((byte[])[.. connect, .. header], output.WrittenSpan.ToArray());

        redactor.Redact(payload.AsSpan(0, 40000), output);
        Assert.AreEqual(connect.Length + header.Length + 40000, output.WrittenCount);

        redactor.Redact(payload.AsSpan(40000), output);
        redactor.Flush(output);

        CollectionAssert.AreEqual((byte[])[.. connect, .. header, .. payload], output.WrittenSpan.ToArray());
    }

    [TestMethod]
    public void MalformedTunnel_ALaterRemainingLengthPastFourBytes_LogsTheRestAsReceived()
    {
        byte[] sent = [.. Connect(5, 0x02, [0, .. Text("cid")]), 0xF0, 0xFF, 0xFF, 0xFF, 0xFF, 0xF0, 2, 0x18, 0];

        TunnelRedactionAssert.AssertLoggedHoweverSplit(sent, sent);
    }

    // Authentication Method SCRAM-SHA-256.
    private static byte[] Method => [0x15, .. Text("SCRAM-SHA-256")];

    // A PUBLISH to topic "t" with no properties, whose payload reads like an Authentication Data property.
    private static byte[] Publish => [0x30, 10, .. Text("t"), 0, 0x16, 0, 3, .. "abc"u8];

    // Two length bytes and the text, as MQTT writes a string.
    private static byte[] Text(string text) => [(byte)(text.Length >> 8), (byte)text.Length, .. Wire.Bytes(text)];

    private static byte[] Connect(byte level, byte flags, params byte[] afterKeepAlive)
    {
        byte[] body = [.. Text("MQTT"), level, flags, 0x00, 0x3C, .. afterKeepAlive];

        return [0x10, (byte)body.Length, .. body];
    }
}
