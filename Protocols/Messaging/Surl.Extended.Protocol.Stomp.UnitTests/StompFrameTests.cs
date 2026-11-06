namespace Surl.Extended.Protocol.Stomp.UnitTests;

/// <summary>A frame's headers.</summary>
[TestClass]
public sealed class StompFrameTests
{
    [TestMethod]
    public void Header_Present_IsItsValue()
    {
        var frame = new StompFrame("SEND", new KeyValuePair<string, string>("a", "1"), new KeyValuePair<string, string>("b", "2"));

        Assert.AreEqual("2", frame.Header("b"));
        Assert.IsTrue(frame.Body.IsEmpty);
    }

    [TestMethod]
    public void Header_Absent_IsNull()
    {
        var frame = new StompFrame("SEND", new KeyValuePair<string, string>("a", "1"));

        Assert.IsNull(frame.Header("A"));
    }
}
