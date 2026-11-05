namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>
/// A message split into its sections, and the <c>header</c> and <c>properties</c> sections read
/// (AMQP 1.0 Part 3 section 3.2).
/// </summary>
[TestClass]
public sealed class Amqp1MessageSectionTests
{
    [TestMethod]
    public void ReadAll_MessageOfSeveralSections_KeepsEachSectionsBytes()
    {
        var header = Amqp1Encoder.Encode(new Amqp1Header { Durable = true }.ToDescribed());
        var properties = Amqp1Encoder.Encode(new Amqp1Properties { To = "/queue/q" }.ToDescribed());
        var data = Amqp1Encoder.Encode(new Amqp1Described(new Amqp1Symbol("amqp:data:binary"), new byte[] { 1, 2 }));
        var footer = Amqp1Encoder.Encode(new Amqp1Described(Amqp1Descriptor.Footer, new Amqp1Map([])));
        byte[] message = [.. header, .. properties, .. data, .. footer];

        var sections = Amqp1MessageSection.ReadAll(message);

        CollectionAssert.AreEqual(new[] { Amqp1Descriptor.Header, Amqp1Descriptor.Properties, Amqp1Descriptor.Data, Amqp1Descriptor.Footer }, sections.Select(section => section.DescriptorCode).ToArray());
        CollectionAssert.AreEqual(data, sections[2].Encoded.ToArray());
        Assert.IsTrue(Amqp1Header.FromDescribed(sections[0].Described).Durable);
        Assert.AreEqual("/queue/q", Amqp1Properties.FromDescribed(sections[1].Described).To);
    }

    [TestMethod]
    [DataRow("41", DisplayName = "a value that is not described")]
    [DataRow("00A3017841", DisplayName = "an unknown symbolic descriptor")]
    [DataRow("00536F41", DisplayName = "a descriptor below the sections")]
    [DataRow("00537941", DisplayName = "a descriptor above the sections")]
    public void ReadAll_ValueThatIsNotASection_ThrowsDecodeError(string hex)
    {
        var exception = Assert.ThrowsExactly<Amqp1DecodeException>(() => Amqp1MessageSection.ReadAll(Convert.FromHexString(hex)));

        Assert.AreEqual(Amqp1ErrorCondition.DecodeError, exception.Condition);
    }

    [TestMethod]
    public void HeaderFromDescribed_TrailingFieldsOmitted_ReadsTheDefaults()
    {
        var header = Amqp1Header.FromDescribed(new Amqp1Described(Amqp1Descriptor.Header, Array.Empty<object?>()));

        Assert.IsFalse(header.Durable);
        Assert.AreEqual(Amqp1Header.DefaultPriority, header.Priority);
        Assert.IsNull(header.Ttl);
        Assert.IsFalse(header.FirstAcquirer);
        Assert.AreEqual(0u, header.DeliveryCount);
        CollectionAssert.AreEqual(Convert.FromHexString("00537045"), Amqp1Encoder.Encode(header.ToDescribed()));
    }

    [TestMethod]
    public void HeaderAndProperties_EveryField_RoundTrip()
    {
        var header = new Amqp1Header { Durable = true, Priority = 9, Ttl = 1000, FirstAcquirer = true, DeliveryCount = 2 };
        var properties = new Amqp1Properties
        {
            MessageId = 7ul,
            UserId = [117],
            To = "to",
            Subject = "subject",
            ReplyTo = "reply",
            CorrelationId = "c",
            ContentType = new Amqp1Symbol("text/plain"),
            ContentEncoding = new Amqp1Symbol("gzip"),
            AbsoluteExpiryTime = new Amqp1Timestamp(2),
            CreationTime = new Amqp1Timestamp(1),
            GroupId = "g",
            GroupSequence = 3,
            ReplyToGroupId = "rg",
        };

        var headerRead = Amqp1Header.FromDescribed((Amqp1Described)new Amqp1Decoder(Amqp1Encoder.Encode(header.ToDescribed())).ReadValue()!);
        var propertiesRead = Amqp1Properties.FromDescribed((Amqp1Described)new Amqp1Decoder(Amqp1Encoder.Encode(properties.ToDescribed())).ReadValue()!);

        Assert.AreEqual(header, headerRead);
        Assert.AreEqual(properties with { UserId = propertiesRead.UserId }, propertiesRead);
        CollectionAssert.AreEqual(properties.UserId, propertiesRead.UserId);
    }

    [TestMethod]
    public void FromDescribed_WrongDescriptor_ThrowsDecodeError()
    {
        var described = new Amqp1Described(Amqp1Descriptor.Open, Array.Empty<object?>());

        Assert.ThrowsExactly<Amqp1DecodeException>(() => Amqp1Header.FromDescribed(described));
        Assert.ThrowsExactly<Amqp1DecodeException>(() => Amqp1Properties.FromDescribed(described));
        Assert.ThrowsExactly<ArgumentNullException>(() => Amqp1Header.FromDescribed(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => Amqp1Properties.FromDescribed(null!));
    }
}
