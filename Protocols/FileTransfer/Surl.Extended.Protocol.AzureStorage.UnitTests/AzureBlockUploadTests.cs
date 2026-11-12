using System.Security.Cryptography;
using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// The Azure server's Put Block, Put Block List and Get Block List, whose requests are written from
/// the REST API's "Put Block", "Put Block List" and "Get Block List" pages (ADR-0109, decisions 1, 4
/// and 7).
/// </summary>
[TestClass]
public sealed class AzureBlockUploadTests
{
    // Base64 of "AAA", "BBB", "CCC" and "ZZZ": block IDs of equal length, as the page requires.
    private const string BlockA = "QUFB";
    private const string BlockB = "QkJC";
    private const string BlockC = "Q0ND";
    private const string BlockZ = "Wlpa";

    private const string BlockListHead = "<?xml version=\"1.0\" encoding=\"utf-8\"?><BlockList>";

    [TestMethod]
    public async Task ServeAsync_PutBlock_Answers201WithTheBlocksContentMd5()
    {
        var harness = Writable();

        var response = await PutBlockAsync(harness, BlockA, "one ");

        Assert.AreEqual(
            "HTTP/1.1 201 Created\r\n" + AzureServerHarness.CommonFields(response)
                + $"Content-MD5: {Convert.ToBase64String(MD5.HashData("one "u8))}\r\nx-ms-request-server-encrypted: false\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
            response);
    }

    [TestMethod]
    public async Task ServeAsync_ThreeBlocksCommittedInOrder_AreReadBackWithGetBlob()
    {
        var harness = Writable();
        await PutBlockAsync(harness, BlockA, "one ");
        await PutBlockAsync(harness, BlockB, "two ");
        await PutBlockAsync(harness, BlockC, "three");

        var committed = await PutBlockListAsync(harness, $"<Uncommitted>{BlockA}</Uncommitted><Latest>{BlockB}</Latest><Latest>{BlockC}</Latest>");
        var read = await harness.SendWithBearerAsync("GET", "/photos/big.bin");

        Assert.AreEqual(
            "HTTP/1.1 201 Created\r\n" + AzureServerHarness.CommonFields(committed)
                + $"ETag: {ETagOf("one two three")}\r\nLast-Modified: {AzureServerHarness.NowHttpDate}\r\nx-ms-request-server-encrypted: false\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
            committed);
        StringAssert.StartsWith(read, "HTTP/1.1 200 OK\r\n");
        StringAssert.EndsWith(read, "\r\n\r\none two three");
    }

    [TestMethod]
    public async Task ServeAsync_BlocksCommittedInAnotherOrder_AreWrittenInTheListsOrder()
    {
        var harness = Writable();
        await PutBlockAsync(harness, BlockA, "one ");
        await PutBlockAsync(harness, BlockB, "two ");

        await PutBlockListAsync(harness, $"<Latest>{BlockB}</Latest><Latest>{BlockA}</Latest><Latest>{BlockB}</Latest>");

        Assert.AreEqual("two one two ", harness.ReadFile("photos/big.bin"));
    }

    [TestMethod]
    public async Task ServeAsync_StagedBlob_IsInvisibleUntilCommitted()
    {
        var harness = Writable();
        await PutBlockAsync(harness, BlockA, "one ");

        var read = await harness.SendWithBearerAsync("GET", "/photos/big.bin");
        var listed = await harness.SendWithBearerAsync("GET", "/photos?restype=container&comp=list");

        StringAssert.Contains(read, "<Code>BlobNotFound</Code>");
        Assert.DoesNotContain("big.bin", listed);
        Assert.AreEqual(ContentEntryKind.None, harness.Store.GetEntryKind(harness.Store.MapRequestPath("/photos/big.bin")));
    }

    [TestMethod]
    public async Task ServeAsync_BlockPutAgain_ReplacesTheUncommittedBlock()
    {
        var harness = Writable();
        await PutBlockAsync(harness, BlockA, "first");
        await PutBlockAsync(harness, BlockA, "one ");

        var response = await GetBlockListAsync(harness, "uncommitted");

        StringAssert.EndsWith(response, $"{BlockListHead}<UncommittedBlocks><Block><Name>{BlockA}</Name><Size>4</Size></Block></UncommittedBlocks></BlockList>");
    }

    [TestMethod]
    public async Task ServeAsync_CommittedAndLatestEntries_ReuseTheCommittedBlocks()
    {
        var harness = Writable();
        await PutBlockAsync(harness, BlockA, "one ");
        await PutBlockAsync(harness, BlockB, "two ");
        await PutBlockListAsync(harness, $"<Latest>{BlockA}</Latest><Latest>{BlockB}</Latest>");
        await PutBlockAsync(harness, BlockC, "three ");

        await PutBlockListAsync(harness, $"<Committed>{BlockB}</Committed><Uncommitted>{BlockC}</Uncommitted><Latest>{BlockA}</Latest>");

        Assert.AreEqual("two three one ", harness.ReadFile("photos/big.bin"));
    }

    [TestMethod]
    public async Task ServeAsync_EmptyBlocks_AreCommittedAndCommittedAgain()
    {
        var harness = Writable();
        await PutBlockAsync(harness, BlockZ, string.Empty);
        await PutBlockAsync(harness, BlockA, "one");
        await PutBlockListAsync(harness, $"<Latest>{BlockZ}</Latest><Latest>{BlockA}</Latest>");

        var response = await PutBlockListAsync(harness, $"<Committed>{BlockA}</Committed><Committed>{BlockZ}</Committed>");

        StringAssert.StartsWith(response, "HTTP/1.1 201 Created\r\n");
        Assert.AreEqual("one", harness.ReadFile("photos/big.bin"));
    }

    [TestMethod]
    public async Task ServeAsync_CommittedListOfABlobRewrittenByPutBlob_IsForgotten()
    {
        var harness = Writable();
        await PutBlockAsync(harness, BlockA, "one ");
        await PutBlockListAsync(harness, $"<Latest>{BlockA}</Latest>");
        await harness.SendWithBearerAndBodyAsync("PUT", "/photos/big.bin", "rewritten", "x-ms-blob-type: BlockBlob");

        var listed = await GetBlockListAsync(harness, "all");
        var committed = await PutBlockListAsync(harness, $"<Committed>{BlockA}</Committed>");

        StringAssert.EndsWith(listed, $"{BlockListHead}<CommittedBlocks></CommittedBlocks><UncommittedBlocks></UncommittedBlocks></BlockList>");
        StringAssert.Contains(committed, "<Code>InvalidBlockList</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_PutBlobAfterStagedBlocks_DiscardsTheUncommittedBlocks()
    {
        var harness = Writable();
        await PutBlockAsync(harness, BlockA, "one ");

        var put = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/big.bin", "whole", "x-ms-blob-type: BlockBlob");
        var uncommitted = await GetBlockListAsync(harness, "uncommitted");
        var committed = await PutBlockListAsync(harness, $"<Uncommitted>{BlockA}</Uncommitted>");

        StringAssert.StartsWith(put, "HTTP/1.1 201 Created\r\n");
        StringAssert.EndsWith(uncommitted, $"{BlockListHead}<UncommittedBlocks></UncommittedBlocks></BlockList>");
        Assert.AreEqual(
            AzureServerHarness.ExpectedError(committed, "400 Bad Request", "InvalidBlockList", "The specified block list is invalid."),
            committed);
        Assert.AreEqual("whole", harness.ReadFile("photos/big.bin"));
    }

    [TestMethod]
    public async Task ServeAsync_DeleteBlobAfterStagedBlocks_DiscardsTheUncommittedBlocks()
    {
        var harness = Writable();
        harness.WriteFile("photos/big.bin", "hello");
        await PutBlockAsync(harness, BlockA, "one ");

        var deleted = await harness.SendWithBearerAsync("DELETE", "/photos/big.bin");
        var uncommitted = await GetBlockListAsync(harness, "uncommitted");
        var committed = await PutBlockListAsync(harness, $"<Uncommitted>{BlockA}</Uncommitted>");

        StringAssert.StartsWith(deleted, "HTTP/1.1 202 Accepted\r\n");
        StringAssert.Contains(uncommitted, "<Code>BlobNotFound</Code>");
        Assert.AreEqual(
            AzureServerHarness.ExpectedError(committed, "400 Bad Request", "InvalidBlockList", "The specified block list is invalid."),
            committed);
        Assert.AreEqual(ContentEntryKind.None, harness.Store.GetEntryKind(harness.Store.MapRequestPath("/photos/big.bin")));
    }

    [TestMethod]
    public async Task ServeAsync_RefusedDeleteBlob_KeepsTheUncommittedBlocks()
    {
        var harness = Writable();
        await PutBlockAsync(harness, BlockA, "one ");

        var deleted = await harness.SendWithBearerAsync("DELETE", "/photos/big.bin");
        var uncommitted = await GetBlockListAsync(harness, "uncommitted");

        StringAssert.Contains(deleted, "<Code>BlobNotFound</Code>");
        StringAssert.EndsWith(uncommitted, $"{BlockListHead}<UncommittedBlocks><Block><Name>{BlockA}</Name><Size>4</Size></Block></UncommittedBlocks></BlockList>");
    }

    [TestMethod]
    [DataRow("Q", DisplayName = "not base64")]
    [DataRow("", DisplayName = "empty")]
    [DataRow("QUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFB", DisplayName = "past 64 bytes")]
    public async Task ServeAsync_PutBlockWithAMalformedId_Is400InvalidBlockId(string blockId)
    {
        var harness = Writable();

        var response = await PutBlockAsync(harness, blockId, "one ");

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(response, "400 Bad Request", "InvalidBlockId", "The specified block ID is invalid. The block ID must be Base64-encoded."),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_PutBlockWithAnIdOfAnotherLength_Is400InvalidBlockId()
    {
        var harness = Writable();
        await PutBlockAsync(harness, BlockA, "one ");

        var response = await PutBlockAsync(harness, "QUFBQQ==", "two ");

        StringAssert.Contains(response, "<Code>InvalidBlockId</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_PutBlockWithAnIdOfAnotherLengthThanTheCommittedBlocks_Is400InvalidBlockId()
    {
        var harness = Writable();
        await PutBlockAsync(harness, BlockA, "one ");
        await PutBlockListAsync(harness, $"<Latest>{BlockA}</Latest>");

        var response = await PutBlockAsync(harness, "QUFBQQ==", "two ");

        StringAssert.Contains(response, "<Code>InvalidBlockId</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_PutBlockWithoutABlockId_Is400MissingRequiredQueryParameter()
    {
        var harness = Writable();

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/big.bin?comp=block", "one ");

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(response, "400 Bad Request", "MissingRequiredQueryParameter", "A query parameter that's mandatory for this request is not specified.", "<QueryParameterName>blockid</QueryParameterName>"),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_PutBlockListNamingAnUnstagedBlock_Is400InvalidBlockListAndWritesNothing()
    {
        var harness = Writable();
        await PutBlockAsync(harness, BlockA, "one ");

        var response = await PutBlockListAsync(harness, $"<Latest>{BlockA}</Latest><Latest>{BlockB}</Latest>");

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(response, "400 Bad Request", "InvalidBlockList", "The specified block list is invalid."),
            response);
        Assert.AreEqual(ContentEntryKind.None, harness.Store.GetEntryKind(harness.Store.MapRequestPath("/photos/big.bin")));
    }

    [TestMethod]
    [DataRow("Committed", DisplayName = "an uncommitted block named as committed")]
    [DataRow("Uncommitted", DisplayName = "a committed block named as uncommitted")]
    public async Task ServeAsync_PutBlockListLookingInTheWrongList_Is400InvalidBlockList(string kind)
    {
        var harness = Writable();
        await PutBlockAsync(harness, BlockA, "one ");
        await PutBlockListAsync(harness, $"<Latest>{BlockA}</Latest>");
        await PutBlockAsync(harness, BlockB, "two ");
        var named = kind == "Committed" ? BlockB : BlockA;

        var response = await PutBlockListAsync(harness, $"<{kind}>{named}</{kind}>");

        StringAssert.Contains(response, "<Code>InvalidBlockList</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_GetBlockList_WritesThePagesBlockListForEachType()
    {
        var harness = Writable();
        await PutBlockAsync(harness, BlockA, "one ");
        await PutBlockAsync(harness, BlockB, "two ");
        await PutBlockListAsync(harness, $"<Latest>{BlockA}</Latest><Latest>{BlockB}</Latest>");
        await PutBlockAsync(harness, BlockC, "three");
        const string Committed = $"<CommittedBlocks><Block><Name>{BlockA}</Name><Size>4</Size></Block><Block><Name>{BlockB}</Name><Size>4</Size></Block></CommittedBlocks>";
        const string Uncommitted = $"<UncommittedBlocks><Block><Name>{BlockC}</Name><Size>5</Size></Block></UncommittedBlocks>";

        var byDefault = await harness.SendWithBearerAsync("GET", "/photos/big.bin?comp=blocklist");
        var committed = await GetBlockListAsync(harness, "committed");
        var uncommitted = await GetBlockListAsync(harness, "UNCOMMITTED");
        var all = await GetBlockListAsync(harness, "all");

        var body = $"{BlockListHead}{Committed}</BlockList>";
        Assert.AreEqual(
            "HTTP/1.1 200 OK\r\n" + AzureServerHarness.CommonFields(byDefault)
                + $"Last-Modified: {AzureServerHarness.NowHttpDate}\r\nETag: {ETagOf("one two ")}\r\nContent-Type: application/xml\r\nx-ms-blob-content-length: 8\r\n"
                + $"Content-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}",
            byDefault);
        StringAssert.EndsWith(committed, body);
        StringAssert.EndsWith(uncommitted, $"{BlockListHead}{Uncommitted}</BlockList>");
        StringAssert.EndsWith(all, $"{BlockListHead}{Committed}{Uncommitted}</BlockList>");
    }

    [TestMethod]
    public async Task ServeAsync_GetBlockListOfAStagedBlobOnly_HasNoDateOrTag()
    {
        var harness = Writable();
        await PutBlockAsync(harness, BlockA, "one ");

        var response = await GetBlockListAsync(harness, "all");

        var body = $"{BlockListHead}<CommittedBlocks></CommittedBlocks><UncommittedBlocks><Block><Name>{BlockA}</Name><Size>4</Size></Block></UncommittedBlocks></BlockList>";
        Assert.AreEqual(
            "HTTP/1.1 200 OK\r\n" + AzureServerHarness.CommonFields(response)
                + $"Content-Type: application/xml\r\nx-ms-blob-content-length: 0\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}",
            response);
    }

    [TestMethod]
    public async Task ServeAsync_GetBlockListOfABlobPutWhole_HasNoCommittedBlocks()
    {
        var harness = Writable();
        harness.WriteFile("photos/big.bin", "hello");

        var response = await GetBlockListAsync(harness, "committed");

        StringAssert.Contains(response, "x-ms-blob-content-length: 5\r\n");
        StringAssert.EndsWith(response, $"{BlockListHead}<CommittedBlocks></CommittedBlocks></BlockList>");
    }

    [TestMethod]
    public async Task ServeAsync_GetBlockListOfNothing_Is404BlobNotFound()
    {
        var harness = Writable();

        var response = await GetBlockListAsync(harness, "all");

        StringAssert.Contains(response, "<Code>BlobNotFound</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_GetBlockListOfANameTheStoreCannotHold_Is404BlobNotFound()
    {
        var harness = Writable();

        var response = await harness.SendWithBearerAsync("GET", "/photos/a%00b?comp=blocklist");

        StringAssert.Contains(response, "<Code>BlobNotFound</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_GetBlockListOfAnotherType_Is400InvalidQueryParameterValue()
    {
        var harness = Writable();

        var response = await GetBlockListAsync(harness, "Every");

        StringAssert.Contains(response, "<QueryParameterName>blocklisttype</QueryParameterName><QueryParameterValue>Every</QueryParameterValue>");
    }

    [TestMethod]
    public async Task ServeAsync_BlockWritesWithUploadsOff_Are403AuthorizationPermissionMismatch()
    {
        var harness = Writable(new ContentExposureOptions { ListDirectories = true });

        var block = await PutBlockAsync(harness, BlockA, "one ");
        var list = await PutBlockListAsync(harness, string.Empty);

        StringAssert.Contains(block, "<Code>AuthorizationPermissionMismatch</Code>");
        StringAssert.Contains(list, "<Code>AuthorizationPermissionMismatch</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_BlockWritesOfANameTheStoreCannotHold_Are400InvalidResourceName()
    {
        var harness = Writable();

        var block = await harness.SendWithBearerAndBodyAsync("PUT", $"/photos/a%00b?comp=block&blockid={BlockA}", "one ");
        var list = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a%00b?comp=blocklist", $"{BlockListHead}</BlockList>");

        StringAssert.Contains(block, "<Code>InvalidResourceName</Code>");
        StringAssert.Contains(list, "<Code>InvalidResourceName</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_BlockWritesWhoseFolderIsAFile_Are409InvalidResourceName()
    {
        var harness = Writable();
        harness.WriteFile("photos/a", "file");

        var block = await harness.SendWithBearerAndBodyAsync("PUT", $"/photos/a/b?comp=block&blockid={BlockA}", "one ");
        var list = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a/b?comp=blocklist", $"{BlockListHead}</BlockList>");

        StringAssert.StartsWith(block, "HTTP/1.1 409 Conflict\r\n");
        StringAssert.StartsWith(list, "HTTP/1.1 409 Conflict\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_BlockWritesOverAFolder_Are409InvalidResourceName()
    {
        var harness = Writable();
        harness.CreateFolder("photos/dir");

        var block = await harness.SendWithBearerAndBodyAsync("PUT", $"/photos/dir?comp=block&blockid={BlockA}", "one ");
        var list = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/dir?comp=blocklist", $"{BlockListHead}</BlockList>");

        StringAssert.StartsWith(block, "HTTP/1.1 409 Conflict\r\n");
        StringAssert.StartsWith(list, "HTTP/1.1 409 Conflict\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_EmptyBlockListInNewFolders_WritesAnEmptyBlob()
    {
        var harness = Writable();

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/2026/empty.bin?comp=blocklist", $"{BlockListHead}</BlockList>", "x-ms-blob-content-type: text/plain");
        var read = await harness.SendWithBearerAsync("HEAD", "/photos/2026/empty.bin");

        StringAssert.Contains(response, $"ETag: {ETagOf(string.Empty)}\r\n");
        StringAssert.Contains(read, "Content-Length: 0\r\nContent-Type: text/plain\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_BlockWritesWithoutALength_Are411MissingContentLengthHeader()
    {
        var harness = Writable();

        var block = await harness.SendWithBearerAsync("PUT", $"/photos/big.bin?comp=block&blockid={BlockA}");
        var list = await harness.SendWithBearerAsync("PUT", "/photos/big.bin?comp=blocklist");

        StringAssert.StartsWith(block, "HTTP/1.1 411 Length Required\r\n");
        StringAssert.StartsWith(list, "HTTP/1.1 411 Length Required\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_BlockWritesCutShort_Are400InvalidInput()
    {
        var harness = Writable();

        var block = await harness.SendRawAsync($"PUT /photos/big.bin?comp=block&blockid={BlockA} HTTP/1.1\r\nHost: h\r\n{BearerFields()}Content-Length: 10\r\nConnection: close\r\n\r\none");
        var list = await harness.SendRawAsync($"PUT /photos/big.bin?comp=blocklist HTTP/1.1\r\nHost: h\r\n{BearerFields()}Content-Length: 100\r\nConnection: close\r\n\r\n<BlockList>");

        StringAssert.Contains(block, "<Code>InvalidInput</Code>");
        StringAssert.Contains(list, "<Code>InvalidInput</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_BlockWritesWithContentMd5_AreCheckedAgainstTheBody()
    {
        var harness = Writable();
        var blockMd5 = Convert.ToBase64String(MD5.HashData("one "u8));
        var listBody = $"{BlockListHead}<Latest>{BlockA}</Latest></BlockList>";
        var listMd5 = Convert.ToBase64String(MD5.HashData(Encoding.UTF8.GetBytes(listBody)));

        var malformed = await PutBlockAsync(harness, BlockA, "one ", "Content-MD5: nope");
        var mismatched = await PutBlockAsync(harness, BlockA, "two ", $"Content-MD5: {blockMd5}");
        var matched = await PutBlockAsync(harness, BlockA, "one ", $"Content-MD5: {blockMd5}");
        var listMismatched = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/big.bin?comp=blocklist", listBody, $"Content-MD5: {blockMd5}");
        var listMatched = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/big.bin?comp=blocklist", listBody, $"Content-MD5: {listMd5}");

        StringAssert.Contains(malformed, "<Code>InvalidMd5</Code>");
        StringAssert.Contains(mismatched, "<Code>Md5Mismatch</Code>");
        StringAssert.StartsWith(matched, "HTTP/1.1 201 Created\r\n");
        StringAssert.Contains(listMismatched, "<Code>Md5Mismatch</Code>");
        StringAssert.StartsWith(listMatched, "HTTP/1.1 201 Created\r\n");
        Assert.AreEqual("one ", harness.ReadFile("photos/big.bin"));
    }

    [TestMethod]
    public async Task ServeAsync_PutBlockAnnouncedPastTheUploadLimit_Is413RequestBodyTooLarge()
    {
        var harness = Writable(new ContentExposureOptions { AllowUploads = true, MaxUploadBytes = 3 });

        var response = await PutBlockAsync(harness, BlockA, "one ");

        StringAssert.StartsWith(response, "HTTP/1.1 413 ");
        StringAssert.Contains(response, "<Code>RequestBodyTooLarge</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_BlocksTogetherPastTheUploadLimit_Are413AndDiscarded()
    {
        var harness = Writable(new ContentExposureOptions { AllowUploads = true, MaxUploadBytes = 6 });
        await PutBlockAsync(harness, BlockA, "one ");

        var response = await PutBlockAsync(harness, BlockB, "two ");
        var listed = await GetBlockListAsync(harness, "all");

        StringAssert.Contains(response, "<Code>RequestBodyTooLarge</Code>");
        StringAssert.Contains(listed, "<Code>BlobNotFound</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_BlockListPastTheUploadLimit_Is413AndKeepsTheBlob()
    {
        var harness = Writable(new ContentExposureOptions { AllowUploads = true, MaxUploadBytes = 6 });
        await PutBlockAsync(harness, BlockA, "one ");
        await PutBlockListAsync(harness, $"<Latest>{BlockA}</Latest>");
        await PutBlockAsync(harness, BlockB, "two ");

        var response = await PutBlockListAsync(harness, $"<Committed>{BlockA}</Committed><Latest>{BlockB}</Latest>");

        StringAssert.Contains(response, "<Code>RequestBodyTooLarge</Code>");
        Assert.AreEqual("one ", harness.ReadFile("photos/big.bin"));
    }

    [TestMethod]
    public async Task ServeAsync_PutBlockListWithTheIndexFull_Is503ServerBusyAndTheBlobIsRemoved()
    {
        var harness = Writable(maxIndexEntries: 1);
        await harness.SendWithBearerAndBodyAsync("PUT", "/photos/first", "x", "x-ms-blob-type: BlockBlob");
        await PutBlockAsync(harness, BlockA, "one ");

        var response = await PutBlockListAsync(harness, $"<Latest>{BlockA}</Latest>");

        StringAssert.Contains(response, "<Code>ServerBusy</Code>");
        Assert.AreEqual(ContentEntryKind.None, harness.Store.GetEntryKind(harness.Store.MapRequestPath("/photos/big.bin")));
    }

    [TestMethod]
    public async Task ServeAsync_PutBlockListAnnouncedPast4MiB_Is400InvalidXmlDocument()
    {
        var harness = Writable();

        var response = await harness.SendRawAsync(
            $"PUT /photos/big.bin?comp=blocklist HTTP/1.1\r\nHost: h\r\n{BearerFields()}Content-Length: {AzureBlockListDocument.MaxBodyBytes + 1}\r\nConnection: close\r\n\r\n");

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(response, "400 Bad Request", "InvalidXmlDocument", "XML specified is not syntactically valid."),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_PutBlockListWithAnEmptyBody_Is400InvalidXmlDocument()
    {
        var harness = Writable();

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/big.bin?comp=blocklist", string.Empty);

        StringAssert.Contains(response, "<Code>InvalidXmlDocument</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_ChunkedPutBlockListPast4MiB_Is400InvalidXmlDocument()
    {
        var harness = Writable();
        var size = AzureBlockListDocument.MaxBodyBytes + 1;

        var response = await harness.SendRawAsync(
            $"PUT /photos/big.bin?comp=blocklist HTTP/1.1\r\nHost: h\r\n{BearerFields()}Transfer-Encoding: chunked\r\nConnection: close\r\n\r\n{size:x}\r\n{new string(' ', size)}\r\n0\r\n\r\n");

        StringAssert.Contains(response, "<Code>InvalidXmlDocument</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_PutBlockListPast50000Entries_Is400BlockListTooLong()
    {
        var harness = Writable();
        var entries = string.Concat(Enumerable.Repeat($"<Latest>{BlockA}</Latest>", AzureStagedBlocks.MaxCommittedBlocks + 1));

        var response = await PutBlockListAsync(harness, entries);

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(response, "400 Bad Request", "BlockListTooLong", "The block list may not contain more than 50,000 blocks."),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_PutBlockPast100000UncommittedBlocks_Is409BlockCountExceedsLimit()
    {
        var harness = Writable();
        await PutBlockAsync(harness, "MDAwMDAw", "x");
        var blocks = harness.Server.StagingArea.Find<AzureStagedBlocks>("blocks photos/big.bin")!;
        for (var block = 1; block < AzureStagedBlocks.MaxUncommittedBlocks; block++)
        {
            blocks.Stage(new AzureBlock(Convert.ToBase64String(Encoding.ASCII.GetBytes($"{block:D6}")), 0, 1), AzureServerHarness.Now);
        }

        var past = await PutBlockAsync(harness, Convert.ToBase64String("999999"u8), "x");
        var replaced = await PutBlockAsync(harness, "MDAwMDAw", "y");

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(past, "409 Conflict", "BlockCountExceedsLimit", "The uncommitted block count cannot exceed the maximum limit of 100,000 blocks."),
            past);
        StringAssert.StartsWith(replaced, "HTTP/1.1 201 Created\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_PutBlockWithTheStagingAreaFull_Is503ServerBusy()
    {
        var harness = Writable();
        await FillTheStagingAreaAsync(harness);

        var response = await PutBlockAsync(harness, BlockA, "one ");

        StringAssert.Contains(response, "<Code>ServerBusy</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_PutBlockListWithTheStagingAreaFull_CommitsWithoutRememberingTheList()
    {
        var harness = Writable();
        await FillTheStagingAreaAsync(harness);

        var committed = await PutBlockListAsync(harness, string.Empty);
        var listed = await GetBlockListAsync(harness, "committed");

        StringAssert.StartsWith(committed, "HTTP/1.1 201 Created\r\n");
        StringAssert.EndsWith(listed, $"{BlockListHead}<CommittedBlocks></CommittedBlocks></BlockList>");
    }

    [TestMethod]
    [DataRow("GET", "/photos/big.bin?comp=block&blockid=QUFB", "InvalidQueryParameterValue", DisplayName = "Put Block's comp on a read")]
    [DataRow("PUT", "/photos?comp=blocklist", "InvalidUri", DisplayName = "a block list of a container")]
    [DataRow("GET", "/photos/big.bin?comp=blocklist&snapshot=2026-11-05", "InvalidQueryParameterValue", DisplayName = "a snapshot's block list")]
    public async Task ServeAsync_BlockRequestTheServerDoesNotServe_IsRefused(string method, string target, string code)
    {
        var harness = Writable();

        var response = await harness.SendWithBearerAsync(method, target);

        StringAssert.Contains(response, $"<Code>{code}</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_PutBlockWithALease_Is400UnsupportedHeader()
    {
        var harness = Writable();

        var response = await PutBlockAsync(harness, BlockA, "one ", "x-ms-lease-id: 1f6b2a6e-0000-0000-0000-000000000000");

        StringAssert.Contains(response, "<Code>UnsupportedHeader</Code><Message>");
        StringAssert.Contains(response, "<HeaderName>x-ms-lease-id</HeaderName>");
    }

    [TestMethod]
    [DataRow("If-Match: \"0123\"", DisplayName = "If-Match failing")]
    [DataRow("If-Unmodified-Since: Wed, 04 Nov 2026 12:00:00 GMT", DisplayName = "If-Unmodified-Since failing")]
    [DataRow("If-None-Match: \"5eb63bbbe01eeed093cb22bb8f5acdc3\"", DisplayName = "If-None-Match matching")]
    [DataRow("If-Modified-Since: Thu, 05 Nov 2026 12:00:00 GMT", DisplayName = "If-Modified-Since unmodified")]
    public async Task ServeAsync_PutBlockListWithAFailingCondition_Is412ConditionNotMetLeavingTheBlobAndStagedBlocks(string condition)
    {
        var harness = Writable();
        harness.WriteFile("photos/big.bin", "hello world");
        await PutBlockAsync(harness, BlockA, "one ");

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/big.bin?comp=blocklist", $"{BlockListHead}<Uncommitted>{BlockA}</Uncommitted></BlockList>", condition);

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(response, "412 Precondition Failed", "ConditionNotMet", "The condition specified using HTTP conditional header(s) is not met."),
            response);
        Assert.AreEqual("hello world", harness.ReadFile("photos/big.bin"));
        StringAssert.StartsWith(await PutBlockListAsync(harness, $"<Uncommitted>{BlockA}</Uncommitted>"), "HTTP/1.1 201 Created\r\n");
        Assert.AreEqual("one ", harness.ReadFile("photos/big.bin"));
    }

    [TestMethod]
    public async Task ServeAsync_PutBlockListIfNoneMatchAnyOnAnExistingBlob_Is409BlobAlreadyExists()
    {
        var harness = Writable();
        harness.WriteFile("photos/big.bin", "hello world");
        await PutBlockAsync(harness, BlockA, "one ");

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/big.bin?comp=blocklist", $"{BlockListHead}<Uncommitted>{BlockA}</Uncommitted></BlockList>", "If-None-Match: *");

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(response, "409 Conflict", "BlobAlreadyExists", "The specified blob already exists."),
            response);
        Assert.AreEqual("hello world", harness.ReadFile("photos/big.bin"));
    }

    [TestMethod]
    public async Task ServeAsync_PutBlockListIfNoneMatchAnyOnAMissingBlob_IsCommitted()
    {
        var harness = Writable();
        await PutBlockAsync(harness, BlockA, "one ");

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/big.bin?comp=blocklist", $"{BlockListHead}<Uncommitted>{BlockA}</Uncommitted></BlockList>", "If-None-Match: *");

        StringAssert.StartsWith(response, "HTTP/1.1 201 Created\r\n");
        Assert.AreEqual("one ", harness.ReadFile("photos/big.bin"));
    }

    private static async Task FillTheStagingAreaAsync(AzureServerHarness harness)
    {
        for (var file = 0; file < AzureStagingArea.MaxStagings; file++)
        {
            harness.WriteFile($"photos/{file}.txt", string.Empty);
        }

        harness.Scheme = AzureStorageProtocolServer.DataLakeScheme;
        for (var file = 0; file < AzureStagingArea.MaxStagings; file++)
        {
            await harness.SendWithBearerAndBodyAsync("PATCH", $"/photos/{file}.txt?action=append&position=0", "x");
        }

        harness.Scheme = AzureStorageProtocolServer.BlobScheme;
    }

    private static Task<string> PutBlockAsync(AzureServerHarness harness, string blockId, string body, params string[] fields) =>
        harness.SendWithBearerAndBodyAsync("PUT", $"/photos/big.bin?comp=block&blockid={Uri.EscapeDataString(blockId)}", body, fields);

    private static Task<string> PutBlockListAsync(AzureServerHarness harness, string entries) =>
        harness.SendWithBearerAndBodyAsync("PUT", "/photos/big.bin?comp=blocklist", $"{BlockListHead}{entries}</BlockList>");

    private static Task<string> GetBlockListAsync(AzureServerHarness harness, string listType) =>
        harness.SendWithBearerAsync("GET", $"/photos/big.bin?comp=blocklist&blocklisttype={listType}");

    private static string ETagOf(string content) => $"\"{Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(content)))}\"";

    private static string BearerFields() => string.Concat(AzureServerHarness.Bearer.Select(field => field + "\r\n"));

    private static AzureServerHarness Writable(ContentExposureOptions? exposure = null, int maxIndexEntries = ObjectMetadataIndex.DefaultMaxEntries)
    {
        var harness = new AzureServerHarness(exposure: exposure ?? AzureServerHarness.Writable, maxIndexEntries: maxIndexEntries);
        harness.CreateFolder("photos");

        return harness;
    }
}
