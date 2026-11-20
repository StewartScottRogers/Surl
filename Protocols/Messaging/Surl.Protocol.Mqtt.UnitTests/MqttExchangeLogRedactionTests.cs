using System.Buffers;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Mqtt;

[TestClass]
public sealed class MqttExchangeLogRedactionTests
{
    private static readonly byte[] Mask = [0x00, 0x04, (byte)'*', (byte)'*', (byte)'*', (byte)'*'];

    private static IReceivedBytesRedactor NewRedactor() =>
        new MqttProtocolServer(TestMessageBus.New(), new AnonymousAuthenticationPolicy()).CreateRedactor(new ExchangeRedactionContext(1, "mqtt"));

    private static byte[] Logged(params byte[][] chunks)
    {
        var redactor = NewRedactor();
        var output = new ArrayBufferWriter<byte>();
        redactor.ObserveSent([0x20, 0x02, 0x00, 0x00]);
        foreach (var chunk in chunks)
        {
            redactor.Redact(chunk, output);
        }

        redactor.Flush(output);
        return output.WrittenSpan.ToArray();
    }

    private static byte[] UserAndPasswordConnect(string passwordText) =>
        ClientPackets.ConnectWithFields(0xC2, "cid", ClientPackets.String("alice"), ClientPackets.String(passwordText));

    private static byte[] UserAndPasswordConnectMasked() =>
        Loggable(ClientPackets.ConnectWithFields(0xC2, "cid", ClientPackets.String("alice"), Mask), UserAndPasswordConnect("s3cret"));

    // The log keeps the fixed header as received, so the remaining length still counts the password.
    private static byte[] Loggable(byte[] masked, byte[] received)
    {
        var loggable = (byte[])masked.Clone();
        loggable[1] = received[1];
        return loggable;
    }

    [TestMethod]
    public void ConnectWithUserNameAndPassword_IsLoggedWithThePasswordMasked() =>
        CollectionAssert.AreEqual(UserAndPasswordConnectMasked(), Logged(UserAndPasswordConnect("s3cret")));

    [TestMethod]
    public void ConnectSplitAtEveryByte_IsLoggedTheSameAsWhole()
    {
        var connect = UserAndPasswordConnect("s3cret");
        var expected = UserAndPasswordConnectMasked();
        for (var split = 0; split <= connect.Length; split++)
        {
            CollectionAssert.AreEqual(expected, Logged(connect[..split], connect[split..]), $"split at {split}");
        }
    }

    [TestMethod]
    public void ConnectWithWillAndPassword_IsLoggedWithOnlyThePasswordMasked()
    {
        byte[] will = [.. ClientPackets.String("w/topic"), .. ClientPackets.String("will-s3cret")];
        var connect = ClientPackets.ConnectWithFields(0xC6, "cid", will, ClientPackets.String("alice"), ClientPackets.String("s3cret"));
        var expected = Loggable(ClientPackets.ConnectWithFields(0xC6, "cid", will, ClientPackets.String("alice"), Mask), connect);

        CollectionAssert.AreEqual(expected, Logged(connect));
    }

    [TestMethod]
    public void ConnectWithoutPassword_IsLoggedAsReceived()
    {
        var connect = ClientPackets.ConnectWithFields(0x82, "cid", ClientPackets.String("alice"));

        CollectionAssert.AreEqual(connect, Logged(connect));
    }

    [TestMethod]
    public void ConnectWithPasswordAndNoUserName_HasThePasswordMasked() =>
        CollectionAssert.AreEqual(
            Loggable(ClientPackets.ConnectWithFields(0x42, "cid", Mask), ClientPackets.ConnectWithFields(0x42, "cid", ClientPackets.String("s3cret"))),
            Logged(ClientPackets.ConnectWithFields(0x42, "cid", ClientPackets.String("s3cret"))));

    [TestMethod]
    public void ConnectWithBytesAfterThePassword_KeepsThem()
    {
        byte[] trailing = [0x01, 0x02];

        CollectionAssert.AreEqual(
            Loggable(ClientPackets.ConnectWithFields(0x42, "cid", Mask, trailing), ClientPackets.ConnectWithFields(0x42, "cid", ClientPackets.String("s3cret"), trailing)),
            Logged(ClientPackets.ConnectWithFields(0x42, "cid", ClientPackets.String("s3cret"), trailing)));
    }

    [TestMethod]
    public void PublishHoldingThePasswordBytes_IsLoggedUnchanged()
    {
        var publish = ClientPackets.Publish(0x30, "t", "s3cret");

        CollectionAssert.AreEqual((byte[])[.. UserAndPasswordConnectMasked(), .. publish], Logged(UserAndPasswordConnect("s3cret"), publish));
    }

    [TestMethod]
    public void ConnectAndPublishInOneChunk_AreLoggedWithOnlyTheConnectMasked()
    {
        var publish = ClientPackets.Publish(0x30, "t", "s3cret");

        CollectionAssert.AreEqual(
            (byte[])[.. UserAndPasswordConnectMasked(), .. publish],
            Logged(ClientPackets.Join(UserAndPasswordConnect("s3cret"), publish)));
    }

    [TestMethod]
    public void FirstPacketThatIsNotConnect_IsLoggedAsReceived()
    {
        var publish = ClientPackets.Publish(0x30, "t", "s3cret");

        CollectionAssert.AreEqual(publish, Logged(publish[..3], publish[3..]));
    }

    [TestMethod]
    public void RemainingLengthPastFourBytes_IsLoggedAsReceived()
    {
        byte[] bytes = [0x10, 0x80, 0x80, 0x80, 0x80, 0x01, 0x02];

        CollectionAssert.AreEqual(bytes, Logged(bytes));
    }

    [TestMethod]
    public void ConnectCutShortAtEveryByte_NeverLogsAPasswordByte()
    {
        byte[] will = [.. ClientPackets.String("w/topic"), .. ClientPackets.String("will-s3cret")];
        var connect = ClientPackets.ConnectWithFields(0xC6, "cid", will, ClientPackets.String("alice"), ClientPackets.String("PASSWORD"));
        for (var length = 0; length < connect.Length; length++)
        {
            var logged = Encoding.Latin1.GetString(Logged(connect[..length]));
            Assert.DoesNotContain("PASSWORD", logged, $"cut at {length}");
            Assert.DoesNotContain("PASSWOR", logged, $"cut at {length}");
        }
    }

    [TestMethod]
    public void ConnectCutShortInsideThePassword_IsLoggedWithAMaskInItsPlace()
    {
        var connect = ClientPackets.ConnectWithFields(0x42, "cid", ClientPackets.String("PASSWORD"));

        var expectedBody = ClientPackets.ConnectWithFields(0x42, "cid", Mask)[2..];

        CollectionAssert.AreEqual(expectedBody, Logged(connect[..^3])[2..]);
    }

    [TestMethod]
    public void Level5ConnectWithPropertiesAndWill_IsLoggedWithOnlyThePasswordMasked()
    {
        var connect = Level5Connect(0xC6, ClientPackets.String("s3cret"));

        CollectionAssert.AreEqual(Loggable(Level5Connect(0xC6, Mask), connect), Logged(connect));
        for (var length = 0; length < connect.Length - 8; length++)
        {
            CollectionAssert.AreEqual(connect[..length], Logged(connect[..length]), $"cut at {length}");
        }
    }

    [TestMethod]
    public void Level5ConnectWithoutWill_IsLoggedWithThePasswordMasked() =>
        CollectionAssert.AreEqual(Loggable(Level5Connect(0xC2, Mask), Level5Connect(0xC2, ClientPackets.String("s3cret"))), Logged(Level5Connect(0xC2, ClientPackets.String("s3cret"))));

    [TestMethod]
    public void Level5PropertiesLengthWithTooManyBytes_IsLoggedAsReceived()
    {
        byte[] connect = [0x10, 0x10, 0x00, 0x04, .. "MQTT"u8, 5, 0x42, 0x00, 0x3C, 0x80, 0x80, 0x80, 0x80, 0x01];

        CollectionAssert.AreEqual(connect, Logged(connect));
    }

    [TestMethod]
    public void ConnectLongerThanTheHeldBound_IsLoggedAsAMaskAndTheRestOfItDropped()
    {
        var connect = MqttTestExchange.LongPacket(0x10, [.. ClientPackets.String("MQTT"), 4, 0xC2, 0x00, 0x3C, .. ClientPackets.String(new string('c', MqttConnectBytesRedactor.MaxHeldBytes))]);
        var publish = ClientPackets.Publish(0x30, "t", "s3cret");

        CollectionAssert.AreEqual((byte[])[.. "****"u8, .. publish], Logged(connect[..65540], connect[65540..], publish));
    }

    [TestMethod]
    public void ConnectWithATwoByteRemainingLength_IsLoggedWithThePasswordMasked()
    {
        byte[] Build(byte[] password) =>
            MqttTestExchange.LongPacket(0x10, [.. ClientPackets.String("MQTT"), 4, 0x42, 0x00, 0x3C, .. ClientPackets.String(new string('c', 200)), .. password]);
        var connect = Build(ClientPackets.String("s3cret"));
        var expected = Build(Mask);
        expected[1] = connect[1];
        expected[2] = connect[2];

        CollectionAssert.AreEqual(expected, Logged(connect));
    }

    [TestMethod]
    public void Flush_WithNothingHeld_WritesNothing() => Assert.AreEqual(0, Logged().Length);

    [TestMethod]
    public void Redact_WithANullOutput_Throws()
    {
        var redactor = NewRedactor();

        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Redact([], null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Flush(null!));
    }

    private static byte[] Level5Connect(byte flags, byte[] password)
    {
        byte[] properties = [0x05, 0x11, 0x00, 0x00, 0x00, 0x01];
        byte[] will = (flags & 0x04) == 0 ? [] : [.. properties, .. ClientPackets.String("w"), .. ClientPackets.String("m")];
        byte[] user = (flags & 0x80) == 0 ? [] : ClientPackets.String("alice");
        return ClientPackets.Packet(0x10, [.. ClientPackets.String("MQTT"), 5, flags, 0x00, 0x3C, .. properties, .. ClientPackets.String("cid"), .. will, .. user, .. password]);
    }
}
