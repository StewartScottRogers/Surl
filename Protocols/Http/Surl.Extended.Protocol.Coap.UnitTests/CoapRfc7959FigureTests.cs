using Surl.Content;
using static Surl.Extended.Protocol.Coap.CoapTestExchange;

namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// RFC 7959 section 3's block-wise exchanges (ADR-0097 decision 4), each request and its answer
/// written as the figure writes them, served from a content-store file of the figure's size; and
/// the refusals sections 2.5, 2.9 and 4 describe.
/// </summary>
[TestClass]
public sealed class CoapRfc7959FigureTests
{
    private const int Szx128 = 3;
    private const int Szx32 = 1;

    [TestMethod]
    public async Task Figure2_SimpleBlockwiseGet_IsServedIn128ByteBlocksWithSize2OnTheFirst()
    {
        var body = Body(300);
        await using var exchange = new CoapTestExchange(new CoapTestStore().WithFile("status", body), Szx128);

        var first = await exchange.SendAsync(CoapCode.Get, "status");
        var second = await exchange.SendAsync(CoapCode.Get, "status", Block(CoapOptionNumber.Block2, "1/0/128"));
        var third = await exchange.SendAsync(CoapCode.Get, "status", Block(CoapOptionNumber.Block2, "2/0/128"));

        Assert.AreEqual(CoapCode.Content, first.Code);
        Assert.AreEqual("0/1/128", BlockOf(first, CoapOptionNumber.Block2));
        Assert.AreEqual(300u, UIntOf(first, CoapOptionNumber.Size2));
        Assert.AreEqual(body[..128], PayloadOf(first));
        Assert.AreEqual("1/1/128", BlockOf(second, CoapOptionNumber.Block2));
        Assert.IsNull(UIntOf(second, CoapOptionNumber.Size2));
        Assert.AreEqual(body[128..256], PayloadOf(second));
        Assert.AreEqual("2/0/128", BlockOf(third, CoapOptionNumber.Block2));
        Assert.AreEqual(body[256..], PayloadOf(third));
        CollectionAssert.AreEqual(CoapTestStore.EntityTagOf(300), third.Options.Single(option => option.Number == CoapOptionNumber.ETag).Value.ToArray());
    }

    [TestMethod]
    public async Task Figure3_BlockwiseGetWithEarlyNegotiation_IsServedInTheClients64ByteBlocks()
    {
        var body = Body(380);
        await using var exchange = new CoapTestExchange(new CoapTestStore().WithFile("status", body));
        var answers = new List<CoapResponse>();

        for (var number = 0; number <= 5; number++)
        {
            answers.Add(await exchange.SendAsync(CoapCode.Get, "status", Block(CoapOptionNumber.Block2, $"{number}/0/64")));
        }

        CollectionAssert.AreEqual(
            new[] { "0/1/64", "1/1/64", "2/1/64", "3/1/64", "4/1/64", "5/0/64" },
            answers.Select(answer => BlockOf(answer, CoapOptionNumber.Block2)).ToArray());
        Assert.AreEqual(380u, UIntOf(answers[0], CoapOptionNumber.Size2));
        Assert.AreEqual(body, string.Concat(answers.Select(PayloadOf)));
    }

    [TestMethod]
    public async Task Figure4_BlockwiseGetWithLateNegotiation_SwitchesTo64ByteBlocksAtTheRightOffset()
    {
        var body = Body(380);
        await using var exchange = new CoapTestExchange(new CoapTestStore().WithFile("status", body), Szx128);

        var first = await exchange.SendAsync(CoapCode.Get, "status");
        var rest = new List<CoapResponse>();
        for (var number = 2; number <= 5; number++)
        {
            rest.Add(await exchange.SendAsync(CoapCode.Get, "status", Block(CoapOptionNumber.Block2, $"{number}/0/64")));
        }

        Assert.AreEqual("0/1/128", BlockOf(first, CoapOptionNumber.Block2));
        CollectionAssert.AreEqual(new[] { "2/1/64", "3/1/64", "4/1/64", "5/0/64" }, rest.Select(answer => BlockOf(answer, CoapOptionNumber.Block2)).ToArray());
        Assert.AreEqual(body, PayloadOf(first) + string.Concat(rest.Select(PayloadOf)));
    }

    [TestMethod]
    public async Task Figure6_SimpleAtomicBlockwisePut_ContinuesThenChangesTheFileOnlyAtTheLastBlock()
    {
        var store = new CoapTestStore(CoapTestStore.Uploads).WithFile("options", "old");
        var body = Body(306);
        await using var exchange = new CoapTestExchange(store);

        var first = await exchange.SendWithBodyAsync(CoapCode.Put, "options", body[..128], Block(CoapOptionNumber.Block1, "0/1/128"));
        var second = await exchange.SendWithBodyAsync(CoapCode.Put, "options", body[128..256], Block(CoapOptionNumber.Block1, "1/1/128"));
        var beforeTheLast = store.Contents("options");
        var last = await exchange.SendWithBodyAsync(CoapCode.Put, "options", body[256..], Block(CoapOptionNumber.Block1, "2/0/128"));

        Assert.AreEqual(CoapCode.Continue, first.Code);
        Assert.AreEqual("0/1/128", BlockOf(first, CoapOptionNumber.Block1));
        Assert.AreEqual(CoapCode.Continue, second.Code);
        Assert.AreEqual("1/1/128", BlockOf(second, CoapOptionNumber.Block1));
        Assert.AreEqual("old", beforeTheLast);
        Assert.AreEqual(CoapCode.Changed, last.Code);
        Assert.AreEqual("2/0/128", BlockOf(last, CoapOptionNumber.Block1));
        Assert.AreEqual(body, store.Contents("options"));
    }

    [TestMethod]
    public async Task Figure7_AtomicBlockwisePutWithNegotiation_AsksFor32ByteBlocksAndKeepsTheFirst128Bytes()
    {
        var store = new CoapTestStore(CoapTestStore.Uploads);
        var body = Body(170);
        await using var exchange = new CoapTestExchange(store, Szx32);

        var first = await exchange.SendWithBodyAsync(CoapCode.Put, "options", body[..128], Block(CoapOptionNumber.Block1, "0/1/128"));
        var fourth = await exchange.SendWithBodyAsync(CoapCode.Put, "options", body[128..160], Block(CoapOptionNumber.Block1, "4/1/32"));
        var last = await exchange.SendWithBodyAsync(CoapCode.Put, "options", body[160..], Block(CoapOptionNumber.Block1, "5/0/32"));

        Assert.AreEqual("2.31 0/1/32", $"{first.Code} {BlockOf(first, CoapOptionNumber.Block1)}");
        Assert.AreEqual("2.31 4/1/32", $"{fourth.Code} {BlockOf(fourth, CoapOptionNumber.Block1)}");
        Assert.AreEqual("2.01 5/0/32", $"{last.Code} {BlockOf(last, CoapOptionNumber.Block1)}");
        Assert.AreEqual(body, store.Contents("options"));
    }

    [TestMethod]
    public async Task BlockwisePut_ABlockOutOfOrder_Is408AndDiscardsTheUpload()
    {
        var store = new CoapTestStore(CoapTestStore.Uploads);
        var body = Body(384);
        await using var exchange = new CoapTestExchange(store);

        var first = await exchange.SendWithBodyAsync(CoapCode.Put, "options", body[..128], Block(CoapOptionNumber.Block1, "0/1/128"));
        var skipped = await exchange.SendWithBodyAsync(CoapCode.Put, "options", body[256..], Block(CoapOptionNumber.Block1, "2/0/128"));
        var late = await exchange.SendWithBodyAsync(CoapCode.Put, "options", body[128..256], Block(CoapOptionNumber.Block1, "1/1/128"));

        Assert.AreEqual(CoapCode.Continue, first.Code);
        Assert.AreEqual(CoapCode.RequestEntityIncomplete, skipped.Code);
        Assert.AreEqual(CoapCode.RequestEntityIncomplete, late.Code);
        Assert.IsEmpty(store.FileSystem.EnumerateDirectoryEntryNames(InMemoryContentFileSystem.RootPath));
    }

    [TestMethod]
    public async Task BlockwisePut_ATotalOverTheLimit_Is413WithSize1AndDiscardsTheUpload()
    {
        var store = new CoapTestStore(new ContentExposureOptions { AllowUploads = true, MaxUploadBytes = 200 });
        var body = Body(256);
        await using var exchange = new CoapTestExchange(store);

        var first = await exchange.SendWithBodyAsync(CoapCode.Put, "options", body[..128], Block(CoapOptionNumber.Block1, "0/1/128"));
        var second = await exchange.SendWithBodyAsync(CoapCode.Put, "options", body[128..], Block(CoapOptionNumber.Block1, "1/1/128"));

        Assert.AreEqual(CoapCode.Continue, first.Code);
        Assert.AreEqual(CoapCode.RequestEntityTooLarge, second.Code);
        Assert.AreEqual(200u, UIntOf(second, CoapOptionNumber.Size1));
        Assert.IsEmpty(store.FileSystem.EnumerateDirectoryEntryNames(InMemoryContentFileSystem.RootPath));
    }

    [TestMethod]
    public async Task BlockwisePut_ASize1OverTheLimitOnBlock0_Is413WithSize1BeforeAByteIsWritten()
    {
        var store = new CoapTestStore(new ContentExposureOptions { AllowUploads = true, MaxUploadBytes = 200 });
        await using var exchange = new CoapTestExchange(store);

        var answer = await exchange.SendWithBodyAsync(
            CoapCode.Put,
            "options",
            Body(128),
            Block(CoapOptionNumber.Block1, "0/1/128"),
            CoapOption.FromUInt(CoapOptionNumber.Size1, 201));

        Assert.AreEqual(CoapCode.RequestEntityTooLarge, answer.Code);
        Assert.AreEqual(200u, UIntOf(answer, CoapOptionNumber.Size1));
        Assert.IsEmpty(store.FileSystem.EnumerateDirectoryEntryNames(InMemoryContentFileSystem.RootPath));
    }

    [TestMethod]
    public async Task Get_WithSize2Zero_GetsSize2WithTheLength()
    {
        await using var exchange = new CoapTestExchange(new CoapTestStore().WithFile("small", Body(10)).WithFile("large", Body(40)));

        var whole = await exchange.SendAsync(CoapCode.Get, "small", CoapOption.FromUInt(CoapOptionNumber.Size2, 0));
        var block = await exchange.SendAsync(CoapCode.Get, "large", Block(CoapOptionNumber.Block2, "1/0/16"), CoapOption.FromUInt(CoapOptionNumber.Size2, 0));

        Assert.AreEqual(10u, UIntOf(whole, CoapOptionNumber.Size2));
        Assert.IsNull(BlockOf(whole, CoapOptionNumber.Block2));
        Assert.AreEqual(Body(10), PayloadOf(whole));
        Assert.AreEqual(40u, UIntOf(block, CoapOptionNumber.Size2));
        Assert.AreEqual("1/1/16", BlockOf(block, CoapOptionNumber.Block2));
        Assert.AreEqual(Body(40)[16..32], PayloadOf(block));
    }
}
