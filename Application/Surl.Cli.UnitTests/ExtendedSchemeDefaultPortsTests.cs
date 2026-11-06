namespace Surl.Cli;

[TestClass]
public sealed class ExtendedSchemeDefaultPortsTests
{
    [TestMethod]
    [DataRow("s3", 443)]
    [DataRow("az", 443)]
    [DataRow("abfs", 80)]
    [DataRow("abfss", 443)]
    [DataRow("gs", 443)]
    [DataRow("oci", 443)]
    [DataRow("grpc", 80)]
    [DataRow("grpcs", 443)]
    [DataRow("signalr", 80)]
    [DataRow("signalrs", 443)]
    [DataRow("socketio", 80)]
    [DataRow("socketios", 443)]
    [DataRow("mcp", 80)]
    [DataRow("mcps", 443)]
    [DataRow("webtransport", 443)]
    [DataRow("coap", 5683)]
    [DataRow("coaps", 5684)]
    [DataRow("dns", 53)]
    [DataRow("mqtt5", 1883)]
    [DataRow("mqtt5s", 8883)]
    [DataRow("mqtt5+ws", 80)]
    [DataRow("amqp", 5672)]
    [DataRow("amqp1", 5672)]
    [DataRow("amqps", 5671)]
    [DataRow("amqp1s", 5671)]
    [DataRow("kafka", 9092)]
    [DataRow("kafkas", 9093)]
    [DataRow("nats", 4222)]
    [DataRow("natss", 4222)]
    [DataRow("stomp", 61613)]
    [DataRow("stomps", 61614)]
    [DataRow("xmpp", 5222)]
    [DataRow("xmpps", 5223)]
    [DataRow("redis", 6379)]
    [DataRow("rediss", 6379)]
    public void TryGetDefaultPort_AdrSchemes_GiveTheAdrsPort(string scheme, int port)
    {
        var found = ExtendedSchemeDefaultPorts.TryGetDefaultPort(scheme, out var defaultPort);

        Assert.IsTrue(found);
        Assert.AreEqual(port, defaultPort);
    }

    [TestMethod]
    public void Schemes_AreTheAdrsThirtyFiveSchemesWithAPort()
    {
        var schemes = ExtendedSchemeDefaultPorts.Schemes;

        Assert.HasCount(35, schemes);
    }

    [TestMethod]
    [DataRow("mcp+stdio", DisplayName = "mcp+stdio names no port")]
    [DataRow("http", DisplayName = "An original scheme is upstream curl's table's")]
    [DataRow("nosuch")]
    public void TryGetDefaultPort_SchemeWithoutARow_ReturnsFalseAndZero(string scheme)
    {
        var found = ExtendedSchemeDefaultPorts.TryGetDefaultPort(scheme, out var defaultPort);

        Assert.IsFalse(found);
        Assert.AreEqual(0, defaultPort);
    }

    [TestMethod]
    public void Schemes_ShadowNoOriginalScheme()
    {
        var shadowed = ExtendedSchemeDefaultPorts.Schemes.Intersect(SchemeDefaultPorts.Schemes, StringComparer.Ordinal);

        Assert.IsEmpty(shadowed);
    }
}
