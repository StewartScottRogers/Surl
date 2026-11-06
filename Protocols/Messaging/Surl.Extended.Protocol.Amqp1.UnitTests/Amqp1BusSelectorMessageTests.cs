using Surl.MessageBus;

namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>
/// A bus message as a selector and a source filter read it (ADR-0146 decisions 2.1 and 2.2): an
/// <c>amqp1</c> native form's sections, any other message's mapped fields (ADR-0107 decision 6). Native
/// forms are built with the library's codec, which BL-510's tests pin.
/// </summary>
[TestClass]
public sealed class Amqp1BusSelectorMessageTests
{
    [TestMethod]
    public void Read_NativeForm_ReadsItsHeaderPropertiesAndApplicationProperties()
    {
        var native = Amqp1Encoder.Encode(new Amqp1Header { Durable = true, Priority = 7 }.ToDescribed())
            .Concat(Amqp1Encoder.Encode(new Amqp1Properties { Subject = "order" }.ToDescribed()))
            .Concat(Amqp1Encoder.Encode(new Amqp1Described(Amqp1Descriptor.ApplicationProperties, new Amqp1Map([new(5L, "skipped"), new("size", 3L)]))))
            .ToArray();

        var message = Amqp1BusSelectorMessage.Read(new BusMessage("orders", ReadOnlyMemory<byte>.Empty) { NativeForm = new("amqp1", native) });

        Assert.AreEqual((byte)7, message.Header!.Priority);
        Assert.IsTrue(message.Header.Durable);
        Assert.AreEqual("order", message.Properties!.Subject);
        Assert.AreEqual(3L, message.ApplicationProperty("size"));
        Assert.IsNull(message.ApplicationProperty("missing"));
    }

    [TestMethod]
    public void Read_MessageAnotherProtocolPublished_ReadsItsMappedFields()
    {
        var bus = new BusMessage("news", "x"u8.ToArray()) { Headers = [new("color", "red")] };

        var message = Amqp1BusSelectorMessage.Read(bus);

        Assert.IsNull(message.Header);
        Assert.IsNull(message.Properties!.Subject);
        Assert.AreEqual("red", message.ApplicationProperty("color"));
    }

    [TestMethod]
    public void Read_MessageWithoutApplicationProperties_HasNone()
    {
        var message = Amqp1BusSelectorMessage.Read(new BusMessage("news", "x"u8.ToArray()));

        Assert.IsNull(message.ApplicationProperty("color"));
    }

    [TestMethod]
    public void Read_NativeFormThatDoesNotDecode_ReadsAsAnEmptyMessage()
    {
        var message = Amqp1BusSelectorMessage.Read(new BusMessage("news", ReadOnlyMemory<byte>.Empty) { NativeForm = new("amqp1", new byte[] { 0xFF }) });

        Assert.IsNull(message.Header);
        Assert.IsNull(message.Properties);
        Assert.IsNull(message.ApplicationProperty("color"));
    }
}
