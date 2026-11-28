namespace Surl.Mcp;

[TestClass]
public sealed class ToolDescriptionByteRuleTests
{
    [TestMethod]
    [DataRow(SetKeyTool.Description)]
    [DataRow(BrowseQueueTool.Description)]
    [DataRow(ReadFileTool.Description)]
    [DataRow(ReadKeyTool.Description)]
    [DataRow(ReadLogTool.Description)]
    [DataRow(ReadMessageTool.Description)]
    [DataRow(ReadRetainedMessageTool.Description)]
    public void Description_OfAToolAnsweringStoredBytes_NamesTheControlCharacterCaseOfBase64(string description)
    {
        StringAssert.Contains(description, "control character other than tab, CR and LF");
    }

    [TestMethod]
    [DataRow(BrowseQueueTool.Description)]
    [DataRow(ReadFileTool.Description)]
    [DataRow(ReadKeyTool.Description)]
    [DataRow(ReadLogTool.Description)]
    [DataRow(ReadMessageTool.Description)]
    [DataRow(ReadRetainedMessageTool.Description)]
    public void Description_OfAReadBackTool_SaysWhenBytesAreTextAndWhenBase64(string description)
    {
        StringAssert.Contains(description, "text when UTF-8 with no control character other than tab, CR and LF, else base64");
    }

    [TestMethod]
    public void Description_OfSetKey_SaysWhichKeysAreAnsweredAsKeyBase64()
    {
        StringAssert.Contains(
            SetKeyTool.Description,
            "key_base64 for a key that is not UTF-8 or that holds a control character other than tab, CR and LF");
    }

    [TestMethod]
    public void Description_OfSetKey_SaysThe16MiBLimitBoundsTheKeyAndValueTogether()
    {
        StringAssert.Contains(SetKeyTool.Description, "Give the key and the value as UTF-8 text or base64, at most 16 MiB together;");
        Assert.IsFalse(SetKeyTool.Description.Contains("16 MiB each", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Description_OfSetKey_SaysAServerWithNeitherARedisNorARedissListenUrlRefusesIt()
    {
        StringAssert.Contains(SetKeyTool.Description, "A server with no redis or rediss listen URL refuses it.");
    }
}
