namespace Surl.Extended.Protocol.Amqp0.UnitTests;

/// <summary>
/// Field tables looked up by name.
/// </summary>
[TestClass]
public sealed class Amqp0FieldTableTests
{
    [TestMethod]
    public void Find_RepeatedName_ReturnsTheFirst()
    {
        var table = new Amqp0FieldTable([new("LOGIN", Amqp0FieldValue.UInt8(1)), new("LOGIN", Amqp0FieldValue.UInt8(2))]);

        Assert.AreEqual((byte)1, table.Find("LOGIN")!.Value);
    }

    [TestMethod]
    public void Find_NameOfAnotherCase_IsNotFound()
    {
        var table = new Amqp0FieldTable([new("LOGIN", Amqp0FieldValue.Void)]);

        Assert.IsNull(table.Find("login"));
    }
}
