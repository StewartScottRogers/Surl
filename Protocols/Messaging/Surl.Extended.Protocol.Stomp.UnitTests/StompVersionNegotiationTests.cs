namespace Surl.Extended.Protocol.Stomp.UnitTests;

/// <summary>The specification's "Protocol Negotiation" (ADR-0118 decision 1).</summary>
[TestClass]
public sealed class StompVersionNegotiationTests
{
    [TestMethod]
    [DataRow("1.0,1.1,1.2", StompVersion.V1_2)]
    [DataRow("1.2,1.0", StompVersion.V1_2)]
    [DataRow("1.1,2.1", StompVersion.V1_1)]
    [DataRow("1.0", StompVersion.V1_0)]
    public void ChooseHighest_AcceptVersion_IsTheHighestCommonVersion(string acceptVersion, StompVersion expected)
    {
        var chosen = StompVersionNegotiation.ChooseHighest(acceptVersion);

        Assert.AreEqual(expected, chosen);
    }

    [TestMethod]
    public void ChooseHighest_NoAcceptVersion_Is10()
    {
        var chosen = StompVersionNegotiation.ChooseHighest(null);

        Assert.AreEqual(StompVersion.V1_0, chosen);
    }

    [TestMethod]
    [DataRow("2.0,2.1")]
    [DataRow("")]
    public void ChooseHighest_NoCommonVersion_IsNull(string acceptVersion)
    {
        var chosen = StompVersionNegotiation.ChooseHighest(acceptVersion);

        Assert.IsNull(chosen);
    }

    [TestMethod]
    [DataRow(StompVersion.V1_0, "1.0")]
    [DataRow(StompVersion.V1_1, "1.1")]
    [DataRow(StompVersion.V1_2, "1.2")]
    public void Text_Version_IsItsHeaderValue(StompVersion version, string expected)
    {
        Assert.AreEqual(expected, StompVersionNegotiation.Text(version));
    }
}
