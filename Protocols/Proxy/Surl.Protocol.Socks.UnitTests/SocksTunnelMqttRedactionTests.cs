namespace Surl.Protocol.Socks;

/// <summary>
/// MQTT <c>CONNECT</c> passwords through a granted SOCKS tunnel, as the exchange log shows them
/// (ADR-0182, amendment 10). <c>Surl.LineProtocol</c>'s <c>TunnelMqttConnectRedactorTests</c> cover
/// every form of the packet.
/// </summary>
[TestClass]
public sealed class SocksTunnelMqttRedactionTests
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
        SocksTunnelRedactionTestKit.AssertLoggedHoweverSplit(
            [.. CurlConnectMasked, .. CurlSubscribe],
            [.. CurlConnect, .. CurlSubscribe]);

    [TestMethod]
    public void Connect_CutOffInsideThePassword_IsLoggedMasked() =>
        CollectionAssert.AreEqual(
            (byte[])[0x10, 14, .. CurlConnect[2..12], .. "****"u8],
            SocksTunnelRedactionTestKit.Logged(CurlConnect[..^3]));

    // Two length bytes and the text, as MQTT writes a string.
    private static byte[] Text(string text) => [(byte)(text.Length >> 8), (byte)text.Length, .. SocksTestKit.Latin1Bytes(text)];
}
