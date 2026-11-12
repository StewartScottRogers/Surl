namespace Surl.Cli;

/// <summary>
/// <see cref="ConfiguredQueueReader"/> reading <c>&lt;name&gt;[;durable]</c> items (ADR-0101 decisions 3 to 5,
/// ADR-0102 decision 3.2).
/// </summary>
[TestClass]
public sealed class ConfiguredQueueReaderTests
{
    [TestMethod]
    public void Read_NameAlone_IsNotDurable() =>
        Assert.AreEqual(new ConfiguredQueue("orders", IsDurable: false), Accepted("orders"));

    [TestMethod]
    public void Read_Durable_IsDurable() =>
        Assert.AreEqual(new ConfiguredQueue("orders", IsDurable: true), Accepted("orders;DURABLE"));

    [TestMethod]
    public void Read_PercentEncodedName_IsDecoded() =>
        Assert.AreEqual("a;b", Accepted("a%3Bb").Name);

    [TestMethod]
    [DataRow("", "the name is empty", DisplayName = "Empty name")]
    [DataRow("q;", "an empty field", DisplayName = "Trailing semicolon")]
    [DataRow("q;durable=yes", "flag durable takes no value", DisplayName = "Durable with a value")]
    [DataRow("q;durable;durable", "key durable is given twice", DisplayName = "Durable twice")]
    [DataRow("q;retain", "unknown flag retain", DisplayName = "Unknown flag")]
    [DataRow("q;qos=1", "unknown key qos", DisplayName = "Unknown key")]
    [DataRow("q%7F", "a control character", DisplayName = "Control character")]
    public void Read_MalformedItem_IsRefusedWithTheAdrsText(string argument, string expected) =>
        Assert.AreEqual(expected, ConfiguredQueueReader.Read(argument, out _));

    [TestMethod]
    public void QueueGivenTwice_NamesTheQueue() =>
        Assert.AreEqual("queue orders is given twice", ConfiguredQueueReader.QueueGivenTwice("orders"));

    [TestMethod]
    public void Reading_ArgumentType_IsNamedByTheShape() =>
        Assert.AreEqual(ConfiguredQueueReader.Shape.Reading.Type, ConfiguredQueueReader.Reading.Type);

    private static ConfiguredQueue Accepted(string argument)
    {
        Assert.IsNull(ConfiguredQueueReader.Read(argument, out var queue));
        return queue;
    }
}
