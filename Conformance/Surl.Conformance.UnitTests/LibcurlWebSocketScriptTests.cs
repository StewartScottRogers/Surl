namespace Surl.Conformance;

[TestClass]
public sealed class LibcurlWebSocketScriptTests
{
    [TestMethod]
    public void Parse_OptionsUrlAndSteps_ReadsEach()
    {
        var script = LibcurlWebSocketScript.Parse(["--library", "lib.dll", "--recv-timeout", "60000", "ws://h/echo", "send:TEXT+CONT:hel", "recv"]);

        Assert.AreEqual("lib.dll", script.LibraryPath);
        Assert.AreEqual(TimeSpan.FromMinutes(1), script.ReceiveTimeout);
        Assert.AreEqual("ws://h/echo", script.Url);
        Assert.HasCount(2, script.Steps);
        Assert.IsFalse(script.Steps[0].IsReceive);
        Assert.AreEqual(5u, script.Steps[0].Flags);
        CollectionAssert.AreEqual("hel"u8.ToArray(), script.Steps[0].Payload);
        Assert.IsTrue(script.Steps[1].IsReceive);
    }

    [TestMethod]
    public void Parse_UrlOnly_TakesTheDefaults()
    {
        var script = LibcurlWebSocketScript.Parse(["ws://h/echo"]);

        Assert.IsNull(script.LibraryPath);
        Assert.AreEqual(TimeSpan.FromSeconds(5), script.ReceiveTimeout);
        Assert.IsEmpty(script.Steps);
    }

    [TestMethod]
    public void Parse_NoUrl_Throws()
    {
        var exception = Assert.ThrowsExactly<FormatException>(() => LibcurlWebSocketScript.Parse(["--recv-timeout", "10"]));

        StringAssert.Contains(exception.Message, "URL is required");
    }

    [TestMethod]
    public void Parse_OptionWithoutItsValue_Throws()
    {
        var exception = Assert.ThrowsExactly<FormatException>(() => LibcurlWebSocketScript.Parse(["ws://h/", "--library"]));

        StringAssert.Contains(exception.Message, "--library needs a value");
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("soon")]
    public void Parse_TimeoutNotAPositiveNumber_Throws(string timeout)
    {
        var exception = Assert.ThrowsExactly<FormatException>(() => LibcurlWebSocketScript.Parse(["--recv-timeout", timeout, "ws://h/"]));

        StringAssert.Contains(exception.Message, "--recv-timeout");
    }

    [TestMethod]
    public void Parse_NullArguments_ThrowArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => LibcurlWebSocketScript.Parse(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => LibcurlWebSocketScript.ParseStep(null!));
    }

    [TestMethod]
    public void ParseStep_EscapedPayload_IsUnescaped()
    {
        var step = LibcurlWebSocketScript.ParseStep(@"send:CLOSE:\x03\xE8bye");

        Assert.AreEqual(8u, step.Flags);
        CollectionAssert.AreEqual(new byte[] { 0x03, 0xE8, (byte)'b', (byte)'y', (byte)'e' }, step.Payload);
    }

    [TestMethod]
    public void ParseStep_PayloadWithColons_KeepsThemInThePayload()
    {
        var step = LibcurlWebSocketScript.ParseStep("send:TEXT:a:b");

        CollectionAssert.AreEqual("a:b"u8.ToArray(), step.Payload);
    }

    [TestMethod]
    public void ParseStep_RepeatedSend_RepeatsThePayload()
    {
        var step = LibcurlWebSocketScript.ParseStep("send*3:BINARY:ab");

        Assert.AreEqual(2u, step.Flags);
        CollectionAssert.AreEqual("ababab"u8.ToArray(), step.Payload);
    }

    [TestMethod]
    [DataRow("send*0:BINARY:a")]
    [DataRow("send*x:BINARY:a")]
    public void ParseStep_RepeatCountNotPositive_Throws(string step)
    {
        var exception = Assert.ThrowsExactly<FormatException>(() => LibcurlWebSocketScript.ParseStep(step));

        StringAssert.Contains(exception.Message, "not a positive number");
    }

    [TestMethod]
    [DataRow("receive")]
    [DataRow("send:TEXT")]
    [DataRow("post:TEXT:a")]
    public void ParseStep_NoneOfTheForms_Throws(string step)
    {
        var exception = Assert.ThrowsExactly<FormatException>(() => LibcurlWebSocketScript.ParseStep(step));

        StringAssert.Contains(exception.Message, "is neither recv");
    }

    [TestMethod]
    [DataRow("OFFSET")]
    [DataRow("URGENT")]
    public void ParseStep_FlagAScriptMayNotName_Throws(string flag)
    {
        var exception = Assert.ThrowsExactly<FormatException>(() => LibcurlWebSocketScript.ParseStep($"send:{flag}:a"));

        StringAssert.Contains(exception.Message, $"names flag '{flag}'");
    }
}
