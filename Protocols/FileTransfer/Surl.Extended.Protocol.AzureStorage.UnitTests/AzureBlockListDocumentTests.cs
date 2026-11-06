using System.Text;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// Reading a Put Block List body, written from the "Put Block List" page's "Request body".
/// </summary>
[TestClass]
public sealed class AzureBlockListDocumentTests
{
    [TestMethod]
    public void Read_ThePagesExample_ReadsEachEntryInOrder()
    {
        const string Body = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<BlockList>\n  <Committed>first-base64-encoded-block-id</Committed>\n"
            + "  <Uncommitted>second-base64-encoded-block-id</Uncommitted>\n  <Latest>third-base64-encoded-block-id</Latest>\n</BlockList>\n";

        var refusal = AzureBlockListDocument.Read(Encoding.UTF8.GetBytes(Body), out var entries);

        Assert.IsNull(refusal);
        CollectionAssert.AreEqual(
            new[]
            {
                new AzureBlockListEntry(AzureBlockListKind.Committed, "first-base64-encoded-block-id"),
                new AzureBlockListEntry(AzureBlockListKind.Uncommitted, "second-base64-encoded-block-id"),
                new AzureBlockListEntry(AzureBlockListKind.Latest, "third-base64-encoded-block-id"),
            },
            entries);
    }

    [TestMethod]
    public void Read_AnEmptyBlockListElement_HasNoEntries()
    {
        var refusal = AzureBlockListDocument.Read("<BlockList/>"u8.ToArray(), out var entries);

        Assert.IsNull(refusal);
        Assert.IsEmpty(entries);
    }

    [TestMethod]
    [DataRow("", DisplayName = "empty")]
    [DataRow("<Other/>", DisplayName = "another root")]
    [DataRow("<BlockList><Block>QUFB</Block></BlockList>", DisplayName = "an unknown entry")]
    [DataRow("<BlockList>QUFB</BlockList>", DisplayName = "text in the root")]
    [DataRow("<BlockList><Latest><Inner/></Latest></BlockList>", DisplayName = "an element in an entry")]
    [DataRow("<BlockList></BlockList><BlockList/>", DisplayName = "a second root")]
    [DataRow("<!DOCTYPE BlockList [<!ENTITY e \"x\">]><BlockList/>", DisplayName = "a document type")]
    public void Read_ABodyThatIsNoBlockList_IsInvalidXmlDocument(string body)
    {
        var refusal = AzureBlockListDocument.Read(Encoding.UTF8.GetBytes(body), out _);

        Assert.AreSame(AzureError.InvalidXmlDocument, refusal);
    }

    [TestMethod]
    public void Read_50000Entries_IsReadAndOneMoreIsBlockListTooLong()
    {
        var most = "<BlockList>" + string.Concat(Enumerable.Repeat("<Latest>QUFB</Latest>", AzureStagedBlocks.MaxCommittedBlocks)) + "</BlockList>";
        var past = "<BlockList>" + string.Concat(Enumerable.Repeat("<Latest>QUFB</Latest>", AzureStagedBlocks.MaxCommittedBlocks + 1)) + "</BlockList>";

        var mostRefusal = AzureBlockListDocument.Read(Encoding.UTF8.GetBytes(most), out var entries);
        var pastRefusal = AzureBlockListDocument.Read(Encoding.UTF8.GetBytes(past), out _);

        Assert.IsNull(mostRefusal);
        Assert.HasCount(AzureStagedBlocks.MaxCommittedBlocks, entries);
        Assert.AreSame(AzureError.BlockListTooLong, pastRefusal);
    }
}
