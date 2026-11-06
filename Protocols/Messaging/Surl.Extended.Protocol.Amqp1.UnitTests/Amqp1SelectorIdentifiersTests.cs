using static Surl.Extended.Protocol.Amqp1.UnitTests.Amqp1SelectorTests;

namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>
/// What each identifier reads (ADR-0146 decision 2.2): the JMS header identifiers the AMQP fields the
/// AMQP JMS Mapping draft names, and any other identifier an application property.
/// </summary>
[TestClass]
public sealed class Amqp1SelectorIdentifiersTests
{
    private static readonly UnitTestAmqp1SelectorMessage FullMessage = new()
    {
        Header = new() { Durable = true, Priority = 7 },
        Properties = new()
        {
            MessageId = "ID:order-1",
            CorrelationId = "reply-9",
            Subject = "invoice",
            CreationTime = new Amqp1Timestamp(1_700_000_000_000),
            AbsoluteExpiryTime = new Amqp1Timestamp(1_700_000_060_000),
        },
        ApplicationProperties = { ["JMSXGroupID"] = "group-1" },
    };

    private static readonly UnitTestAmqp1SelectorMessage EmptyMessage = new();

    [TestMethod]
    [DataRow("JMSMessageID = 'ID:order-1'", DisplayName = "JMSMessageID is the message-id")]
    [DataRow("JMSCorrelationID = 'reply-9'", DisplayName = "JMSCorrelationID is the correlation-id")]
    [DataRow("JMSType = 'invoice'", DisplayName = "JMSType is the subject")]
    [DataRow("JMSPriority = 7", DisplayName = "JMSPriority is the header's priority")]
    [DataRow("JMSDeliveryMode = 'PERSISTENT'", DisplayName = "a durable header is PERSISTENT")]
    [DataRow("JMSTimestamp = 1700000000000", DisplayName = "JMSTimestamp is the creation-time in milliseconds")]
    [DataRow("JMSExpiration = 1700000060000", DisplayName = "JMSExpiration is the absolute-expiry-time in milliseconds")]
    [DataRow("JMSXGroupID = 'group-1'", DisplayName = "any other identifier is an application property")]
    public void Read_HeaderIdentifier_ReadsItsField(string selector)
    {
        AssertEvaluates(selector, "TRUE", FullMessage);
    }

    [TestMethod]
    [DataRow("JMSMessageID IS NULL", "TRUE")]
    [DataRow("JMSCorrelationID IS NULL", "TRUE")]
    [DataRow("JMSType IS NULL", "TRUE")]
    [DataRow("JMSTimestamp IS NULL", "TRUE")]
    [DataRow("JMSExpiration IS NULL", "TRUE")]
    [DataRow("JMSPriority = 4", "TRUE", DisplayName = "JMSPriority is 4 with no header")]
    [DataRow("JMSDeliveryMode = 'NON_PERSISTENT'", "TRUE", DisplayName = "no header is NON_PERSISTENT")]
    public void Read_MessageWithoutHeaderOrProperties_ReadsDefaultsAndNull(string selector, string expected)
    {
        AssertEvaluates(selector, expected, EmptyMessage);
    }

    [TestMethod]
    public void Read_HeaderNotDurable_IsNonPersistent()
    {
        var message = new UnitTestAmqp1SelectorMessage { Header = new() { Durable = false } };

        AssertEvaluates("JMSDeliveryMode = 'NON_PERSISTENT'", "TRUE", message);
        AssertEvaluates("JMSPriority = 4", "TRUE", message);
    }

    [TestMethod]
    public void Read_HeaderIdentifiers_AreCaseSensitive()
    {
        AssertEvaluates("jmspriority IS NULL", "TRUE", FullMessage);
    }

    [TestMethod]
    [DataRow(12UL, "ID:AMQP_ULONG:12")]
    [DataRow(new byte[] { 0x0A, 0xFF }, "ID:AMQP_BINARY:0AFF")]
    public void Read_MessageIdNotAString_IsPrefixedText(object messageId, string expected)
    {
        var message = new UnitTestAmqp1SelectorMessage { Properties = new() { MessageId = messageId, CorrelationId = messageId } };

        AssertEvaluates($"JMSMessageID = '{expected}'", "TRUE", message);
        AssertEvaluates($"JMSCorrelationID = '{expected}'", "TRUE", message);
    }

    [TestMethod]
    public void Read_MessageIdUuid_IsPrefixedText()
    {
        var uuid = new Guid("0f8fad5b-d9cb-469f-a165-70867728950e");
        var message = new UnitTestAmqp1SelectorMessage { Properties = new() { MessageId = uuid } };

        AssertEvaluates("JMSMessageID = 'ID:AMQP_UUID:0f8fad5b-d9cb-469f-a165-70867728950e'", "TRUE", message);
    }

    [TestMethod]
    public void Read_MessageIdOfAnotherType_IsNull()
    {
        var message = new UnitTestAmqp1SelectorMessage { Properties = new() { MessageId = 5L } };

        AssertEvaluates("JMSMessageID IS NULL", "TRUE", message);
        AssertEvaluates("JMSTimestamp IS NULL AND JMSExpiration IS NULL", "TRUE", message);
    }

    [TestMethod]
    [DataRow((sbyte)-3, "p = -3")]
    [DataRow((byte)3, "p = 3")]
    [DataRow((short)-300, "p = -300")]
    [DataRow((ushort)300, "p = 300")]
    [DataRow(-70000, "p = -70000")]
    [DataRow(70000U, "p = 70000")]
    [DataRow(5_000_000_000L, "p = 5000000000")]
    [DataRow(5_000_000_000UL, "p = 5000000000", DisplayName = "a ulong that fits is exact")]
    [DataRow(ulong.MaxValue, "p > 1.8E19 AND p < 1.9E19", DisplayName = "a ulong past long is approximate")]
    [DataRow(1.5F, "p = 1.5", DisplayName = "a float is approximate")]
    [DataRow(1.5, "p = 1.5")]
    [DataRow(true, "p")]
    [DataRow("text", "p = 'text'")]
    public void Read_ApplicationProperty_IsNormalised(object value, string selector)
    {
        var message = new UnitTestAmqp1SelectorMessage { ApplicationProperties = { ["p"] = value } };

        AssertEvaluates(selector, "TRUE", message);
    }

    [TestMethod]
    public void Read_ApplicationPropertySymbol_IsAString()
    {
        var message = new UnitTestAmqp1SelectorMessage { ApplicationProperties = { ["p"] = new Amqp1Symbol("text") } };

        AssertEvaluates("p = 'text'", "TRUE", message);
    }

    [TestMethod]
    public void Read_ApplicationPropertyTimestamp_IsItsMilliseconds()
    {
        var message = new UnitTestAmqp1SelectorMessage { ApplicationProperties = { ["p"] = new Amqp1Timestamp(42) } };

        AssertEvaluates("p = 42", "TRUE", message);
    }

    [TestMethod]
    public void Read_ApplicationPropertyOfAnotherType_IsNull()
    {
        var message = new UnitTestAmqp1SelectorMessage { ApplicationProperties = { ["p"] = Guid.Empty, ["q"] = null, ["r"] = 'x', ["t"] = 1.5m } };

        AssertEvaluates("p IS NULL", "TRUE", message);
        AssertEvaluates("q IS NULL", "TRUE", message);
        AssertEvaluates("r IS NULL AND t IS NULL", "TRUE", message);
    }
}
