namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>
/// Sources, targets and coordinators read from an <c>attach</c> (Part 3 sections 3.5.3 and 3.5.4;
/// Part 4 section 4.5.1).
/// </summary>
[TestClass]
public sealed class Amqp1TerminusTests
{
    [TestMethod]
    public void Read_SourceWithAddressAndDynamic_ReadsBoth()
    {
        var source = new Amqp1Described(new Amqp1Symbol("amqp:source:list"), new List<object?> { "reply", null, null, null, true });

        Assert.AreEqual(new Amqp1Terminus("reply", true, false), Amqp1Terminus.Read(source));
    }

    [TestMethod]
    public void Read_TargetWithOnlyAnAddress_IsNotDynamic()
    {
        var target = new Amqp1Described(Amqp1Descriptor.Target, new List<object?> { "orders" });

        Assert.AreEqual(new Amqp1Terminus("orders", false, false), Amqp1Terminus.Read(target));
    }

    [TestMethod]
    public void Read_AddressThatIsNotAString_IsNoAddress()
    {
        var target = new Amqp1Described(Amqp1Descriptor.Target, new List<object?> { new Amqp1Symbol("orders") });

        Assert.AreEqual(new Amqp1Terminus(null, false, false), Amqp1Terminus.Read(target));
    }

    [TestMethod]
    public void Read_Coordinator_IsACoordinator()
    {
        var coordinator = new Amqp1Described(new Amqp1Symbol("amqp:coordinator:list"), new List<object?>());

        Assert.IsTrue(Amqp1Terminus.Read(coordinator).IsCoordinator);
    }

    [TestMethod]
    public void Read_AnythingElse_IsNoAddress()
    {
        var none = new Amqp1Terminus(null, false, false);

        Assert.AreEqual(none, Amqp1Terminus.Read(null));
        Assert.AreEqual(none, Amqp1Terminus.Read("orders"));
        Assert.AreEqual(none, Amqp1Terminus.Read(new Amqp1Described(Amqp1Descriptor.Target, "orders")));
        Assert.AreEqual(none, Amqp1Terminus.Read(new Amqp1Described(Amqp1Descriptor.Open, new List<object?> { "orders" })));
    }

    [TestMethod]
    public void Target_NotDynamic_CarriesOnlyTheAddress()
    {
        var target = Amqp1Terminus.Target("orders", isDynamic: false);

        Assert.AreEqual(Amqp1Descriptor.Target, target.Descriptor);
        CollectionAssert.AreEqual(new object?[] { "orders" }, ((IReadOnlyList<object?>)target.Value!).ToArray());
    }
}
