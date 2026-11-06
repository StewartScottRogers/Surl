namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>
/// Addresses mapped to nodes (ADR-0107 decision 4, amending ADR-0100 decision 3.2).
/// </summary>
[TestClass]
public sealed class Amqp1NodeAddressTests
{
    [TestMethod]
    [DataRow("orders", "/queue/orders", DisplayName = "a bare name is a queue")]
    [DataRow("/orders", "/queue/orders", DisplayName = "one leading slash is dropped")]
    [DataRow("//orders", "/queue//orders", DisplayName = "only one leading slash is dropped")]
    [DataRow("queue/orders", "/queue/orders", DisplayName = "queue/")]
    [DataRow("/queue/orders", "/queue/orders", DisplayName = "/queue/")]
    [DataRow("topic/news", "/topic/news", DisplayName = "topic/")]
    [DataRow("/topic/news/+", "/topic/news/+", DisplayName = "a receiver's wildcard")]
    public void Canonical_AddressForReceiving_IsTheNodesCanonicalAddress(string address, string expected)
    {
        Assert.AreEqual(expected, Amqp1NodeAddress.Canonical(address, forSending: false));
    }

    [TestMethod]
    [DataRow(null, false, DisplayName = "no address")]
    [DataRow("", false, DisplayName = "empty")]
    [DataRow("/", false, DisplayName = "a lone slash")]
    [DataRow("queue/", false, DisplayName = "a queue with no name")]
    [DataRow("/topic/", false, DisplayName = "a topic with no name")]
    [DataRow("/topic/news/#", true, DisplayName = "a sender's # wildcard")]
    [DataRow("/topic/+/news", true, DisplayName = "a sender's + wildcard")]
    public void Canonical_AddressNamingNoNode_IsNull(string? address, bool forSending)
    {
        Assert.IsNull(Amqp1NodeAddress.Canonical(address, forSending));
    }

    [TestMethod]
    public void Canonical_SendersTopicWithoutWildcard_IsTheTopic()
    {
        Assert.AreEqual("/topic/news", Amqp1NodeAddress.Canonical("topic/news", forSending: true));
    }

    [TestMethod]
    public void IsTopic_CanonicalAddresses_TellsTopicFromQueue()
    {
        Assert.IsTrue(Amqp1NodeAddress.IsTopic("/topic/news"));
        Assert.IsFalse(Amqp1NodeAddress.IsTopic("/queue/orders"));
        Assert.ThrowsExactly<ArgumentNullException>(() => Amqp1NodeAddress.IsTopic(null!));
    }
}
