namespace Surl.Protocol.Socks;

/// <summary>
/// MQTT <c>CONNECT</c> passwords through a granted SOCKS tunnel, as the exchange log shows them
/// (ADR-0182, amendments 10 and 25). <c>Surl.LineProtocol</c>'s <c>TunnelMqttConnectRedactorTests</c>
/// cover every form of the packet.
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
    public void Mqtt311ConnectAfterAProxyLine_AsCurlSendsIt_IsLoggedWithThePasswordMasked()
    {
        // The tunnel of `--haproxy-protocol --socks5-hostname ... -u tester:secret mqtt://...`:
        // the PROXY line, CONNECT, SUBSCRIBE.
        var request = SocksTestKit.Fixture("socks5h-haproxy-mqtt-user-and-password", "request.bin");
        var tunnel = request[request.AsSpan().IndexOf("PROXY "u8)..];
        var connectStart = tunnel.AsSpan().IndexOf((byte)'\n') + 1;
        var connectLength = 2 + tunnel[connectStart + 1];
        byte[] masked =
        [
            .. tunnel[..connectStart],
            0x10, (byte)(tunnel[connectStart + 1] - 2),
            .. tunnel[(connectStart + 2)..(connectStart + connectLength - 8)],
            .. Text("****"),
            .. tunnel[(connectStart + connectLength)..],
        ];

        SocksTunnelRedactionTestKit.AssertLoggedHoweverSplit(masked, tunnel);
        CollectionAssert.AreEqual(Text("secret"), tunnel[(connectStart + connectLength - 8)..(connectStart + connectLength)]);
    }

    [TestMethod]
    public void Connect_CutOffInsideThePassword_IsLoggedMasked() =>
        CollectionAssert.AreEqual(
            (byte[])[0x10, 14, .. CurlConnect[2..12], .. "****"u8],
            SocksTunnelRedactionTestKit.Logged(CurlConnect[..^3]));

    // Two length bytes and the text, as MQTT writes a string.
    private static byte[] Text(string text) => [(byte)(text.Length >> 8), (byte)text.Length, .. SocksTestKit.Latin1Bytes(text)];
}
