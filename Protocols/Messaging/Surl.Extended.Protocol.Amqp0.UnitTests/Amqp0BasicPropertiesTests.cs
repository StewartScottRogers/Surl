using static Surl.Extended.Protocol.Amqp0.UnitTests.Amqp0ChannelTestBytes;
using static Surl.Extended.Protocol.Amqp0.UnitTests.Amqp0TestBytes;

namespace Surl.Extended.Protocol.Amqp0.UnitTests;

/// <summary>The basic class's properties, flags from bit 15 down (the XML's <c>basic</c> fields).</summary>
[TestClass]
public sealed class Amqp0BasicPropertiesTests
{
    [TestMethod]
    public void Encode_NoProperty_IsNoFlagAndNoBytes()
    {
        var (flags, list) = new Amqp0BasicProperties().Encode();

        Assert.AreEqual(0, flags);
        Assert.IsEmpty(list);
    }

    [TestMethod]
    public void Encode_SomeProperties_SetsTheirFlagsAndWritesThemInOrder()
    {
        var properties = new Amqp0BasicProperties { ContentType = "a", DeliveryMode = 2, Timestamp = 1 };

        var (flags, list) = properties.Encode();

        Assert.AreEqual(0x8000 | 0x1000 | 0x0040, flags);
        CollectionAssert.AreEqual(Concat(ShortString("a"), [2], LongLong(1)), list);
    }

    [TestMethod]
    public void Read_WhatEncodeWrote_IsTheSameProperties()
    {
        var properties = new Amqp0BasicProperties { Priority = 7, ReplyTo = "r", MessageId = "m", ClusterId = "c" };
        var (flags, list) = properties.Encode();

        var read = Amqp0BasicProperties.Read(flags, list);

        Assert.AreEqual(properties, read);
    }
}
