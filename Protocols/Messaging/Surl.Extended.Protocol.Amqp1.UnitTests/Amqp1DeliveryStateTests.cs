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
    public void Declared_CarriesTheTransactionId()
    {
        // declared (0x33), list8 of one field: the binary 00 00 00 01 (Part 4 section 4.5.5).
        Assert.AreEqual("005333" + "C00701" + "A00400000001", Convert.ToHexString(Amqp1Encoder.Encode(Amqp1DeliveryState.Declared([0, 0, 0, 1]))));
        Assert.ThrowsExactly<ArgumentNullException>(() => Amqp1DeliveryState.Declared(null!));
    }

    [TestMethod]
    public void TransactionalState_CarriesTheTransactionIdAndTheOutcome()
    {
        // transactional-state (0x34), list8 of two fields: the binary 00 00 00 01 and accepted (Part 4 section 4.5.6).
        var expected = "005334" + "C00B02" + "A00400000001" + "00532445";

        Assert.AreEqual(expected, Convert.ToHexString(Amqp1Encoder.Encode(Amqp1DeliveryState.TransactionalState([0, 0, 0, 1], Amqp1DeliveryState.Accepted))));
        Assert.ThrowsExactly<ArgumentNullException>(() => Amqp1DeliveryState.TransactionalState(null!, Amqp1DeliveryState.Accepted));
        Assert.ThrowsExactly<ArgumentNullException>(() => Amqp1DeliveryState.TransactionalState([1], null!));
    }

    [TestMethod]
    public void TransactionIdOf_TransactionalStates_IsItsBinaryTxnIdOrNull()
    {
        var symbolDescriptor = new Amqp1Described(new Amqp1Symbol("amqp:transactional-state:list"), new List<object?> { new byte[] { 0, 0, 0, 7 } });

        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 7 }, Amqp1DeliveryState.TransactionIdOf(symbolDescriptor));
        Assert.IsTrue(Amqp1DeliveryState.IsTransactional(symbolDescriptor));
        Assert.IsNull(Amqp1DeliveryState.TransactionIdOf(new Amqp1Described(Amqp1Descriptor.TransactionalState, "not a list")));
        Assert.IsNull(Amqp1DeliveryState.TransactionIdOf(new Amqp1Described(Amqp1Descriptor.TransactionalState, new List<object?>())));
        Assert.IsNull(Amqp1DeliveryState.TransactionIdOf(Amqp1DeliveryState.Accepted));
        Assert.IsFalse(Amqp1DeliveryState.IsTransactional(null));
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
