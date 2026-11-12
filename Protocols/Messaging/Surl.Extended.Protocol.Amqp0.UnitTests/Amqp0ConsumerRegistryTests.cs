namespace Surl.Extended.Protocol.Amqp0.UnitTests;

[TestClass]
public sealed class Amqp0ConsumerRegistryTests
{
    [TestMethod]
    public void EndConsumersOf_Queue_EndsOnlyItsConsumersAndForgetsThem()
    {
        var registry = new Amqp0ConsumerRegistry();
        List<string> ended = [];
        registry.Add(new("a", this, () => ended.Add("a1")));
        registry.Add(new("a", this, () => ended.Add("a2")));
        registry.Add(new("b", this, () => ended.Add("b1")));

        registry.EndConsumersOf("a");
        registry.EndConsumersOf("a");

        CollectionAssert.AreEqual(new[] { "a1", "a2" }, ended);
    }

    [TestMethod]
    public void Remove_RecordedAndNotRecorded_ForgetsTheOneAndIgnoresTheOther()
    {
        var registry = new Amqp0ConsumerRegistry();
        List<string> ended = [];
        var removed = new Amqp0ConsumerRegistration("a", this, () => ended.Add("removed"));
        registry.Add(removed);
        registry.Add(new("a", this, () => ended.Add("kept")));

        registry.Remove(removed);
        registry.Remove(removed);
        registry.EndConsumersOf("a");

        CollectionAssert.AreEqual(new[] { "kept" }, ended);
    }

    [TestMethod]
    public void RemoveOwnedBy_Owner_ForgetsOnlyItsRegistrations()
    {
        var registry = new Amqp0ConsumerRegistry();
        var otherOwner = new object();
        List<string> ended = [];
        registry.Add(new("a", this, () => ended.Add("mine")));
        registry.Add(new("a", otherOwner, () => ended.Add("theirs")));

        registry.RemoveOwnedBy(this);
        registry.EndConsumersOf("a");

        CollectionAssert.AreEqual(new[] { "theirs" }, ended);
    }
}
