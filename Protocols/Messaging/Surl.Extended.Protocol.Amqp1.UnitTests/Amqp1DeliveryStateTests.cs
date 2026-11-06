namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>Delivery states (AMQP 1.0 Part 3 section 3.4), their bytes written by hand from it.</summary>
[TestClass]
public sealed class Amqp1DeliveryStateTests
{
    [TestMethod]
    public void Accepted_IsTheEmptyAcceptedList()
    {
        // 0x00 0x53 0x24: descriptor smallulong 0x24; 0x45: list0.
        Assert.AreEqual("00532445", Convert.ToHexString(Amqp1Encoder.Encode(Amqp1DeliveryState.Accepted)));
    }

    [TestMethod]
    public void Rejected_CarriesTheError()
    {
        // rejected (0x25), list8 of one field: error (0x1D) with condition amqp:not-found.
        var expected = "005325" + "C01701" + "00531D" + "C01101" + "A30E616D71703A6E6F742D666F756E64";

        Assert.AreEqual(expected, Convert.ToHexString(Amqp1Encoder.Encode(Amqp1DeliveryState.Rejected(new Amqp1Error(Amqp1ErrorCondition.NotFound)))));
        Assert.ThrowsExactly<ArgumentNullException>(() => Amqp1DeliveryState.Rejected(null!));
    }

    [TestMethod]
    [DataRow(Amqp1Descriptor.Received, Amqp1Outcome.Received)]
    [DataRow(Amqp1Descriptor.Accepted, Amqp1Outcome.Accepted)]
    [DataRow(Amqp1Descriptor.Rejected, Amqp1Outcome.Rejected)]
    [DataRow(Amqp1Descriptor.Released, Amqp1Outcome.Released)]
    [DataRow(Amqp1Descriptor.Modified, Amqp1Outcome.Modified)]
    [DataRow(Amqp1Descriptor.Source, Amqp1Outcome.None)]
    [DataRow(Amqp1Descriptor.Open, Amqp1Outcome.None)]
    public void OutcomeOf_DescribedState_IsItsOutcome(ulong descriptor, Amqp1Outcome expected)
    {
        Assert.AreEqual(expected, Amqp1DeliveryState.OutcomeOf(new Amqp1Described(descriptor, new List<object?>())));
    }

    [TestMethod]
    public void OutcomeOf_SymbolDescriptorOrNoState_IsReadOrNone()
    {
        Assert.AreEqual(Amqp1Outcome.Released, Amqp1DeliveryState.OutcomeOf(new Amqp1Described(new Amqp1Symbol("amqp:released:list"), new List<object?>())));
        Assert.AreEqual(Amqp1Outcome.None, Amqp1DeliveryState.OutcomeOf(null));
        Assert.AreEqual(Amqp1Outcome.None, Amqp1DeliveryState.OutcomeOf("accepted"));
    }

    [TestMethod]
    public void IsUndeliverableHere_OnlyAModifiedSayingSo_IsTrue()
    {
        Assert.IsTrue(Amqp1DeliveryState.IsUndeliverableHere(new Amqp1Described(Amqp1Descriptor.Modified, new List<object?> { true, true })));
        Assert.IsFalse(Amqp1DeliveryState.IsUndeliverableHere(new Amqp1Described(Amqp1Descriptor.Modified, new List<object?> { true })));
        Assert.IsFalse(Amqp1DeliveryState.IsUndeliverableHere(new Amqp1Described(Amqp1Descriptor.Modified, "not a list")));
        Assert.IsFalse(Amqp1DeliveryState.IsUndeliverableHere(new Amqp1Described(Amqp1Descriptor.Released, new List<object?> { true, true })));
    }
}
