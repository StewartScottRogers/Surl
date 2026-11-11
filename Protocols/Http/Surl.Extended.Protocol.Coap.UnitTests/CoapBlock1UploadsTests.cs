using Surl.Content;
using static Surl.Extended.Protocol.Coap.CoapTestExchange;

namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// <see cref="CoapBlock1Uploads"/>: one flow's atomic Block1 uploads (RFC 7959 sections 2.5 and
/// 2.9; ADR-0097 decisions 4 and 7), through <see cref="CoapResourceAnswerer"/>.
/// </summary>
[TestClass]
public sealed class CoapBlock1UploadsTests
{
    [TestMethod]
    public async Task ContinueAsync_ABlockRepeatedWithANewMessageId_IsAnswered231AgainAndNotWrittenTwice()
    {
        var store = new CoapTestStore(CoapTestStore.Uploads);
        var body = Body(40);
        await using var exchange = new CoapTestExchange(store);

        await exchange.SendWithBodyAsync(CoapCode.Put, "f", body[..16], Block(CoapOptionNumber.Block1, "0/1/16"));
        await exchange.SendWithBodyAsync(CoapCode.Put, "f", body[16..32], Block(CoapOptionNumber.Block1, "1/1/16"));
        var repeated = await exchange.SendWithBodyAsync(CoapCode.Put, "f", body[16..32], Block(CoapOptionNumber.Block1, "1/1/16"));
        var last = await exchange.SendWithBodyAsync(CoapCode.Put, "f", body[32..], Block(CoapOptionNumber.Block1, "2/0/16"));

        Assert.AreEqual("2.31 1/1/16", $"{repeated.Code} {BlockOf(repeated, CoapOptionNumber.Block1)}");
        Assert.AreEqual(CoapCode.Created, last.Code);
        Assert.AreEqual(body, store.Contents("f"));
    }

    [TestMethod]
    public async Task BeginAsync_ANewBlock0_StartsTheUploadAgain()
    {
        var store = new CoapTestStore(CoapTestStore.Uploads);
        await using var replaced = new CoapTestExchange(store);
        await replaced.SendWithBodyAsync(CoapCode.Put, "f", Body(16), Block(CoapOptionNumber.Block1, "0/1/16"));
        await replaced.SendWithBodyAsync(CoapCode.Put, "f", Body(16), Block(CoapOptionNumber.Block1, "1/1/16"));

        var again = await replaced.SendWithBodyAsync(CoapCode.Put, "f", "0123456789abcdef", Block(CoapOptionNumber.Block1, "0/1/16"));
        var last = await replaced.SendWithBodyAsync(CoapCode.Put, "f", "end", Block(CoapOptionNumber.Block1, "1/0/16"));

        Assert.AreEqual(CoapCode.Continue, again.Code);
        Assert.AreEqual(CoapCode.Created, last.Code);
        Assert.AreEqual("0123456789abcdefend", store.Contents("f"));
        Assert.HasCount(1, store.FileSystem.EnumerateDirectoryEntryNames(InMemoryContentFileSystem.RootPath));
    }

    [TestMethod]
    public async Task ContinueAsync_TheLastBlockRepeated_IsNotTakenForARetransmission()
    {
        await using var exchange = new CoapTestExchange(new CoapTestStore(CoapTestStore.Uploads));

        await exchange.SendWithBodyAsync(CoapCode.Put, "f", Body(16), Block(CoapOptionNumber.Block1, "0/1/16"));
        await exchange.SendWithBodyAsync(CoapCode.Put, "f", Body(16), Block(CoapOptionNumber.Block1, "1/1/16"));
        var repeatedWithoutMore = await exchange.SendWithBodyAsync(CoapCode.Put, "f", Body(16), Block(CoapOptionNumber.Block1, "1/0/16"));
        var repeatedWithAnotherLength = await SendAfterTwoBlocksAsync(exchange, Body(8));

        Assert.AreEqual(CoapCode.RequestEntityIncomplete, repeatedWithoutMore.Code);
        Assert.AreEqual(CoapCode.BadRequest, repeatedWithAnotherLength.Code);
    }

    [TestMethod]
    public async Task ContinueAsync_WithNoUploadOpen_Is408()
    {
        await using var exchange = new CoapTestExchange(new CoapTestStore(CoapTestStore.Uploads));

        var answer = await exchange.SendWithBodyAsync(CoapCode.Put, "f", Body(16), Block(CoapOptionNumber.Block1, "1/0/16"));

        Assert.AreEqual(CoapCode.RequestEntityIncomplete, answer.Code);
    }

    [TestMethod]
    public async Task ContinueAsync_AnotherContentFormatThanBlock0s_Is408AndDiscardsTheUpload()
    {
        var store = new CoapTestStore(CoapTestStore.Uploads);
        await using var exchange = new CoapTestExchange(store);
        var json = CoapOption.FromUInt(CoapOptionNumber.ContentFormat, 50);

        await exchange.SendWithBodyAsync(CoapCode.Put, "f", Body(16), Block(CoapOptionNumber.Block1, "0/1/16"), json);
        var changed = await exchange.SendWithBodyAsync(CoapCode.Put, "f", Body(16), Block(CoapOptionNumber.Block1, "1/1/16"));

        Assert.AreEqual(CoapCode.RequestEntityIncomplete, changed.Code);
        Assert.IsEmpty(store.FileSystem.EnumerateDirectoryEntryNames(InMemoryContentFileSystem.RootPath));
    }

    [TestMethod]
    public async Task ContinueAsync_AfterExchangeLifetimeWithoutABlock_Is408()
    {
        var store = new CoapTestStore(CoapTestStore.Uploads);
        await using var exchange = new CoapTestExchange(store);

        await exchange.SendWithBodyAsync(CoapCode.Put, "f", Body(16), Block(CoapOptionNumber.Block1, "0/1/16"));
        exchange.Clock.Advance(CoapTransmissionParameters.ExchangeLifetime);
        var late = await exchange.SendWithBodyAsync(CoapCode.Put, "f", Body(16), Block(CoapOptionNumber.Block1, "1/0/16"));

        Assert.AreEqual(CoapCode.RequestEntityIncomplete, late.Code);
        Assert.IsEmpty(store.FileSystem.EnumerateDirectoryEntryNames(InMemoryContentFileSystem.RootPath));
    }

    [TestMethod]
    public async Task BeginAsync_AFifthUploadOnTheFlow_Is413UntilAnOpenOneExpires()
    {
        var store = new CoapTestStore(new ContentExposureOptions { AllowUploads = true, MaxUploadBytes = 0 });
        await using var exchange = new CoapTestExchange(store);
        for (var index = 0; index < CoapBlock1Uploads.MaxUploadsPerFlow; index++)
        {
            await exchange.SendWithBodyAsync(CoapCode.Put, $"f{index}", Body(16), Block(CoapOptionNumber.Block1, "0/1/16"));
        }

        var fifth = await exchange.SendWithBodyAsync(CoapCode.Put, "g", Body(16), Block(CoapOptionNumber.Block1, "0/1/16"));
        exchange.Clock.Advance(CoapTransmissionParameters.ExchangeLifetime);
        var afterExpiry = await exchange.SendWithBodyAsync(CoapCode.Put, "g", Body(16), Block(CoapOptionNumber.Block1, "0/1/16"));

        Assert.AreEqual(CoapCode.RequestEntityTooLarge, fifth.Code);
        Assert.AreEqual(0u, UIntOf(fifth, CoapOptionNumber.Size1));
        Assert.AreEqual(CoapCode.Continue, afterExpiry.Code);
        Assert.AreEqual(1, store.FileSystem.EnumerateDirectoryEntryNames(InMemoryContentFileSystem.RootPath).Count());
    }

    [TestMethod]
    public async Task BeginAsync_ASize1WithinTheLimit_IsAccepted()
    {
        var store = new CoapTestStore(new ContentExposureOptions { AllowUploads = true, MaxUploadBytes = 200 });
        await using var exchange = new CoapTestExchange(store);

        var answer = await exchange.SendWithBodyAsync(CoapCode.Put, "f", Body(16), Block(CoapOptionNumber.Block1, "0/0/16"), CoapOption.FromUInt(CoapOptionNumber.Size1, 200));

        Assert.AreEqual("2.01 0/0/16", $"{answer.Code} {BlockOf(answer, CoapOptionNumber.Block1)}");
        Assert.AreEqual(Body(16), store.Contents("f"));
    }

    [TestMethod]
    public async Task BeginAsync_PostToAFile_AppendsTheBlocks()
    {
        var store = new CoapTestStore(CoapTestStore.Uploads).WithFile("log", "start:");
        await using var exchange = new CoapTestExchange(store);

        await exchange.SendWithBodyAsync(CoapCode.Post, "log", Body(16), Block(CoapOptionNumber.Block1, "0/1/16"));
        var last = await exchange.SendWithBodyAsync(CoapCode.Post, "log", "end", Block(CoapOptionNumber.Block1, "1/0/16"));

        Assert.AreEqual(CoapCode.Changed, last.Code);
        Assert.AreEqual("start:" + Body(16) + "end", store.Contents("log"));
    }

    [TestMethod]
    public async Task BeginAsync_PostIntoADirectory_CreatesTheNamedFileAtTheLastBlock()
    {
        var store = new CoapTestStore(CoapTestStore.Uploads).WithDirectory("in");
        await using var exchange = new CoapTestExchange(store);

        await exchange.SendWithBodyAsync(CoapCode.Post, "in", Body(16), Block(CoapOptionNumber.Block1, "0/1/16"));
        var last = await exchange.SendWithBodyAsync(CoapCode.Post, "in", "end", Block(CoapOptionNumber.Block1, "1/0/16"));

        var name = string.Concat(Enumerable.Repeat("ab", 16));
        Assert.AreEqual("2.01 1/0/16", $"{last.Code} {BlockOf(last, CoapOptionNumber.Block1)}");
        CollectionAssert.AreEqual(
            new[] { "in", name },
            last.Options.Where(option => option.Number == CoapOptionNumber.LocationPath).Select(option => System.Text.Encoding.UTF8.GetString(option.Value.Span)).ToArray());
        Assert.AreEqual(Body(16) + "end", store.Contents($"in/{name}"));
    }

    [TestMethod]
    public async Task BeginAsync_AnUploadTheStoreWillNotOpen_Is404()
    {
        await using var exchange = new CoapTestExchange(new CoapTestStore(CoapTestStore.Uploads));

        var answer = await exchange.SendWithBodyAsync(CoapCode.Put, "missing/f", Body(16), Block(CoapOptionNumber.Block1, "0/1/16"));

        Assert.AreEqual(CoapCode.NotFound, answer.Code);
    }

    [TestMethod]
    public async Task ContinueAsync_ATargetThatBecameADirectory_Is404AtTheLastBlock()
    {
        var store = new CoapTestStore(CoapTestStore.Uploads);
        await using var exchange = new CoapTestExchange(store);

        await exchange.SendWithBodyAsync(CoapCode.Put, "f", Body(16), Block(CoapOptionNumber.Block1, "0/1/16"));
        store.WithDirectory("f");
        var last = await exchange.SendWithBodyAsync(CoapCode.Put, "f", "end", Block(CoapOptionNumber.Block1, "1/0/16"));

        Assert.AreEqual(CoapCode.NotFound, last.Code);
    }

    [TestMethod]
    public async Task ContinueAsync_AStoreThatFailsAWrite_Is500AndDiscardsTheUpload()
    {
        var store = new CoapTestStore(CoapTestStore.Uploads, maxTotalBytes: 24);
        await using var exchange = new CoapTestExchange(store);

        await exchange.SendWithBodyAsync(CoapCode.Put, "f", Body(16), Block(CoapOptionNumber.Block1, "0/1/16"));
        var failed = await exchange.SendWithBodyAsync(CoapCode.Put, "f", Body(16), Block(CoapOptionNumber.Block1, "1/1/16"));
        var after = await exchange.SendWithBodyAsync(CoapCode.Put, "f", Body(16), Block(CoapOptionNumber.Block1, "2/1/16"));

        Assert.AreEqual(CoapCode.InternalServerError, failed.Code);
        Assert.AreEqual(CoapCode.RequestEntityIncomplete, after.Code);
    }

    [TestMethod]
    public async Task DisposeAsync_TheFlowEnding_DeletesEveryOpenUploadsTemporaryFile()
    {
        var store = new CoapTestStore(CoapTestStore.Uploads);
        var exchange = new CoapTestExchange(store);
        await exchange.SendWithBodyAsync(CoapCode.Put, "f", Body(16), Block(CoapOptionNumber.Block1, "0/1/16"));
        var whileOpen = store.FileSystem.EnumerateDirectoryEntryNames(InMemoryContentFileSystem.RootPath).Count();

        await exchange.DisposeAsync();

        Assert.AreEqual(1, whileOpen);
        Assert.IsEmpty(store.FileSystem.EnumerateDirectoryEntryNames(InMemoryContentFileSystem.RootPath));
    }

    [TestMethod]
    public async Task ContinueAsync_UploadsToAnotherQuery_AreKeptApart()
    {
        await using var exchange = new CoapTestExchange(new CoapTestStore(CoapTestStore.Uploads));
        var query = CoapOption.FromString(CoapOptionNumber.UriQuery, "a=1");

        await exchange.SendWithBodyAsync(CoapCode.Put, "f", Body(16), Block(CoapOptionNumber.Block1, "0/1/16"), query);
        var other = await exchange.SendWithBodyAsync(CoapCode.Put, "f", Body(16), Block(CoapOptionNumber.Block1, "1/1/16"));
        var same = await exchange.SendWithBodyAsync(CoapCode.Put, "f", Body(16), Block(CoapOptionNumber.Block1, "1/1/16"), query);

        Assert.AreEqual(CoapCode.RequestEntityIncomplete, other.Code);
        Assert.AreEqual(CoapCode.Continue, same.Code);
    }

    [TestMethod]
    public async Task SendAsync_ABlock1WithMSetAndAnotherSizeThanSzx_Is400AndAMoreBlockOfTheSizeIsAccepted()
    {
        await using var exchange = new CoapTestExchange(new CoapTestStore(CoapTestStore.Uploads));

        var shortBlock = await exchange.SendWithBodyAsync(CoapCode.Put, "f", Body(8), Block(CoapOptionNumber.Block1, "0/1/16"));
        var longBlock = await exchange.SendWithBodyAsync(CoapCode.Put, "f", Body(17), Block(CoapOptionNumber.Block1, "0/1/16"));
        var exact = await exchange.SendWithBodyAsync(CoapCode.Put, "f", Body(16), Block(CoapOptionNumber.Block1, "0/1/16"));

        Assert.AreEqual(CoapCode.BadRequest, shortBlock.Code);
        Assert.AreEqual(CoapCode.BadRequest, longBlock.Code);
        Assert.AreEqual(CoapCode.Continue, exact.Code);
    }

    [TestMethod]
    public async Task SendAsync_ABlock1TwiceOrABlock2Twice_IsUnrecognisedAnd402()
    {
        await using var exchange = new CoapTestExchange(new CoapTestStore(CoapTestStore.Uploads).WithFile("g", "x"));

        var block1 = await exchange.SendWithBodyAsync(CoapCode.Put, "f", Body(16), Block(CoapOptionNumber.Block1, "0/1/16"), Block(CoapOptionNumber.Block1, "0/1/16"));
        var block2 = await exchange.SendAsync(CoapCode.Get, "g", Block(CoapOptionNumber.Block2, "0/0/16"), Block(CoapOptionNumber.Block2, "0/0/16"));

        Assert.AreEqual(CoapCode.BadOption, block1.Code);
        Assert.AreEqual(CoapCode.BadOption, block2.Code);
    }

    [TestMethod]
    public async Task SendAsync_BlockResponses_CarryEachBlockOptionAtMostOnceAndA231CarriesNoBlock2OrPayload()
    {
        await using var exchange = new CoapTestExchange(new CoapTestStore(CoapTestStore.Uploads).WithFile("g", Body(40)));

        var continued = await exchange.SendWithBodyAsync(CoapCode.Put, "f", Body(16), Block(CoapOptionNumber.Block1, "0/1/16"));
        var block = await exchange.SendAsync(CoapCode.Get, "g", Block(CoapOptionNumber.Block2, "0/0/16"));

        Assert.AreEqual(CoapCode.Continue, continued.Code);
        Assert.IsEmpty(continued.Payload.ToArray());
        CollectionAssert.AreEqual(new[] { CoapOptionNumber.Block1 }, continued.Options.Select(option => option.Number).ToArray());
        Assert.AreEqual(1, block.Options.Count(option => option.Number == CoapOptionNumber.Block2));
        Assert.AreEqual(0, block.Options.Count(option => option.Number == CoapOptionNumber.Block1));
    }

    [TestMethod]
    public async Task SendAsync_ConditionalOptions_AreEvaluatedOnBlock0OnlyAndIfNoneMatchOnBlock0Only()
    {
        var store = new CoapTestStore(CoapTestStore.Uploads).WithFile("taken", "x");
        await using var exchange = new CoapTestExchange(store);
        var ifNoneMatch = new CoapOption(CoapOptionNumber.IfNoneMatch, ReadOnlyMemory<byte>.Empty);
        var wrongIfMatch = new CoapOption(CoapOptionNumber.IfMatch, new byte[] { 9 });

        var refused = await exchange.SendWithBodyAsync(CoapCode.Put, "taken", Body(16), Block(CoapOptionNumber.Block1, "0/1/16"), ifNoneMatch);
        var opened = await exchange.SendWithBodyAsync(CoapCode.Put, "f", Body(16), Block(CoapOptionNumber.Block1, "0/1/16"), ifNoneMatch);
        var last = await exchange.SendWithBodyAsync(CoapCode.Put, "f", "end", Block(CoapOptionNumber.Block1, "1/0/16"), wrongIfMatch);

        Assert.AreEqual(CoapCode.PreconditionFailed, refused.Code);
        Assert.AreEqual(CoapCode.Continue, opened.Code);
        Assert.AreEqual(CoapCode.Created, last.Code);
    }

    private static async Task<CoapResponse> SendAfterTwoBlocksAsync(CoapTestExchange exchange, string repeated)
    {
        await exchange.SendWithBodyAsync(CoapCode.Put, "f", Body(16), Block(CoapOptionNumber.Block1, "0/1/16"));
        await exchange.SendWithBodyAsync(CoapCode.Put, "f", Body(16), Block(CoapOptionNumber.Block1, "1/1/16"));
        return await exchange.SendWithBodyAsync(CoapCode.Put, "f", repeated, Block(CoapOptionNumber.Block1, "1/1/16"));
    }
}
