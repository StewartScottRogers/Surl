using System.Text;
using Surl.Content;
using static Surl.Extended.Protocol.Coap.CoapTestExchange;

namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// <see cref="CoapResourceAnswerer"/>: methods, options and response codes over the content store
/// (RFC 7252 sections 5.8 to 5.10, RFC 6690; ADR-0097 decisions 1, 3 and 6).
/// </summary>
[TestClass]
public sealed class CoapResourceAnswererTests
{
    private static readonly CoapAccessRules Anonymous = new(AllowsAnonymous: true, HasAccounts: false);

    private static readonly ContentExposureOptions UploadsAndListings = new() { AllowUploads = true, ListDirectories = true };

    [TestMethod]
    public async Task AnswerAsync_Get_Is205WithETagMaxAgeContentFormatAndTheBytes()
    {
        var store = new CoapTestStore().WithFile("d.json", "{}");

        var response = await AnswerAsync(store, CoapCode.Get, Path("d.json"));

        Assert.AreEqual(CoapCode.Content, response.Code);
        CollectionAssert.AreEqual(CoapTestStore.EntityTagOf(2), Option(response, CoapOptionNumber.ETag));
        Assert.AreEqual(60u, new CoapOption(CoapOptionNumber.MaxAge, Option(response, CoapOptionNumber.MaxAge)).ReadUInt());
        Assert.AreEqual(50u, new CoapOption(CoapOptionNumber.ContentFormat, Option(response, CoapOptionNumber.ContentFormat)).ReadUInt());
        Assert.AreEqual("{}", Encoding.UTF8.GetString(response.Payload.Span));
    }

    [TestMethod]
    public async Task AnswerAsync_GetWithAMatchingETag_Is203WithoutPayload()
    {
        var store = new CoapTestStore().WithFile("d.txt", "abc");

        var response = await AnswerAsync(store, CoapCode.Get, [.. Path("d.txt"), new CoapOption(CoapOptionNumber.ETag, new byte[] { 1 }), new CoapOption(CoapOptionNumber.ETag, CoapTestStore.EntityTagOf(3))]);

        Assert.AreEqual(CoapCode.Valid, response.Code);
        Assert.IsTrue(response.Payload.IsEmpty);
        CollectionAssert.AreEqual(CoapTestStore.EntityTagOf(3), Option(response, CoapOptionNumber.ETag));
    }

    [TestMethod]
    [DataRow(0u, "0.00")]
    [DataRow(50u, "4.06")]
    public async Task AnswerAsync_GetWithAccept_IsServedOnlyInTheResourcesFormat(uint accept, string code)
    {
        var store = new CoapTestStore().WithFile("d.txt", "abc");

        var response = await AnswerAsync(store, CoapCode.Get, [.. Path("d.txt"), CoapOption.FromUInt(CoapOptionNumber.Accept, accept)]);

        Assert.AreEqual(code == "0.00" ? CoapCode.Content : CoapCode.NotAcceptable, response.Code);
    }

    [TestMethod]
    public async Task AnswerAsync_GetOfAMissingFileADirectoryOrARefusedPath_Is404()
    {
        var store = new CoapTestStore().WithDirectory("dir");

        Assert.AreEqual(CoapCode.NotFound, (await AnswerAsync(store, CoapCode.Get, Path("nope"))).Code);
        Assert.AreEqual(CoapCode.NotFound, (await AnswerAsync(store, CoapCode.Get, Path("dir"))).Code);
        Assert.AreEqual(CoapCode.NotFound, (await AnswerAsync(store, CoapCode.Get, Path("CON"))).Code);
    }

    [TestMethod]
    [DataRow(".")]
    [DataRow("..")]
    public async Task AnswerAsync_DotSegment_Is400(string segment) =>
        Assert.AreEqual(CoapCode.BadRequest, (await AnswerAsync(new CoapTestStore(), CoapCode.Get, Path(segment))).Code);

    [TestMethod]
    [DataRow(CoapOptionNumber.ProxyUri)]
    [DataRow(CoapOptionNumber.ProxyScheme)]
    public async Task AnswerAsync_Proxying_Is505(int number) =>
        Assert.AreEqual(CoapCode.ProxyingNotSupported, (await AnswerAsync(new CoapTestStore(), CoapCode.Get, [CoapOption.FromString(number, "coap://elsewhere/")])).Code);

    [TestMethod]
    [DataRow(false, false, true, "2.05", "4.01")]
    [DataRow(false, true, true, "4.01", "4.01")]
    [DataRow(true, true, true, "2.05", "2.04")]
    [DataRow(true, false, false, "2.05", "4.05")]
    public async Task AnswerAsync_AccessRulesAndTheUploadSwitch_DecideReadsAndWrites(bool allowsAnonymous, bool hasAccounts, bool uploads, string read, string write)
    {
        var store = new CoapTestStore(new ContentExposureOptions { AllowUploads = uploads }).WithFile("f.txt", "x");
        var rules = new CoapAccessRules(allowsAnonymous, hasAccounts);

        var got = await AnswerAsync(store, CoapCode.Get, Path("f.txt"), rules);
        var put = await AnswerAsync(store, CoapCode.Put, Path("f.txt"), rules, "y");

        Assert.AreEqual(read, got.Code.ToString());
        Assert.AreEqual(write, put.Code.ToString());
    }

    [TestMethod]
    public async Task AnswerAsync_AuthenticatedPeer_MayReadAndWriteWithoutAnonymity()
    {
        var store = new CoapTestStore(CoapTestStore.Uploads).WithFile("f.txt", "x");
        var answerer = new CoapResourceAnswerer(store.Store, new CoapAccessRules(false, true), new UnitTestCoapRandomSource());
        var put = Request(CoapCode.Put, Path("f.txt"), "y");

        await using var uploads = new CoapBlock1Uploads(store.Store, new ManualTimeProvider(), CoapBlockOption.LargestSizeExponent);

        var response = await answerer.AnswerAsync(put, CoapRequestOptions.Read(put), "coaps", peerIsAuthenticated: true, uploads, CancellationToken.None);

        Assert.AreEqual(CoapCode.Changed, response.Code);
    }

    [TestMethod]
    [DataRow((byte)0x05)]
    [DataRow((byte)0x07)]
    [DataRow((byte)0x1F)]
    public async Task AnswerAsync_AnyOtherMethod_Is405(byte method) =>
        Assert.AreEqual(CoapCode.MethodNotAllowed, (await AnswerAsync(new CoapTestStore(), new CoapCode(method), Path("f"))).Code);

    [TestMethod]
    public async Task AnswerAsync_PutIntoAMissingDirectoryOrOntoADirectory_Is404()
    {
        var store = new CoapTestStore(CoapTestStore.Uploads).WithDirectory("dir");

        Assert.AreEqual(CoapCode.NotFound, (await AnswerAsync(store, CoapCode.Put, Path("none", "f.txt"), body: "x")).Code);
        Assert.AreEqual(CoapCode.NotFound, (await AnswerAsync(store, CoapCode.Put, Path("dir"), body: "x")).Code);
        Assert.AreEqual(CoapCode.NotFound, (await AnswerAsync(store, CoapCode.Put, Path("CON"), body: "x")).Code);
    }

    [TestMethod]
    public async Task AnswerAsync_PutOverTheLimit_Is413WithSize1()
    {
        var store = new CoapTestStore(new ContentExposureOptions { AllowUploads = true, MaxUploadBytes = 2 });

        var response = await AnswerAsync(store, CoapCode.Put, Path("f.txt"), body: "abc");

        Assert.AreEqual(CoapCode.RequestEntityTooLarge, response.Code);
        Assert.AreEqual(2u, new CoapOption(CoapOptionNumber.Size1, Option(response, CoapOptionNumber.Size1)).ReadUInt());
    }

    [TestMethod]
    public async Task AnswerAsync_StoreFailure_Is500WithoutPayload()
    {
        var store = new CoapTestStore(CoapTestStore.Uploads, maxTotalBytes: 1);

        var response = await AnswerAsync(store, CoapCode.Put, Path("f.txt"), body: "abc");

        Assert.AreEqual(CoapCode.InternalServerError, response.Code);
        Assert.IsTrue(response.Payload.IsEmpty);
    }

    [TestMethod]
    public async Task AnswerAsync_UnreadableFile_Is500AndAnyOtherFailureGoesOn()
    {
        static Task<CoapResponse> GetThrough(Exception failure)
        {
            var store = new ContentStore(InMemoryContentFileSystem.RootPath, new UnitTestThrowingContentFileSystem(InMemoryContentFileSystem.RootPath, failure), new ContentExposureOptions());
            var answerer = new CoapResourceAnswerer(store, Anonymous, new UnitTestCoapRandomSource());
            var get = Request(CoapCode.Get, Path("f.txt"));
            return answerer.AnswerAsync(get, CoapRequestOptions.Read(get), "coap", false, new CoapBlock1Uploads(store, new ManualTimeProvider(), 6), CancellationToken.None);
        }

        Assert.AreEqual(CoapCode.InternalServerError, (await GetThrough(new UnauthorizedAccessException("denied"))).Code);
        Assert.AreEqual(CoapCode.InternalServerError, (await GetThrough(new IOException("unreadable"))).Code);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => GetThrough(new InvalidOperationException("a defect")));
    }

    [TestMethod]
    public async Task AnswerAsync_PostToAFile_AppendsAnd204s()
    {
        var store = new CoapTestStore(CoapTestStore.Uploads).WithFile("log.txt", "a");

        var response = await AnswerAsync(store, CoapCode.Post, Path("log.txt"), body: "b");

        Assert.AreEqual(CoapCode.Changed, response.Code);
        Assert.AreEqual("ab", store.Contents("log.txt"));
    }

    [TestMethod]
    public async Task AnswerAsync_PostToADirectory_CreatesARandomlyNamedFileAnd201sWithItsLocationPath()
    {
        var store = new CoapTestStore(CoapTestStore.Uploads).WithDirectory("box");
        var name = string.Concat(Enumerable.Repeat("ab", 16));

        var response = await AnswerAsync(store, CoapCode.Post, Path("box", string.Empty), body: "posted");

        Assert.AreEqual(CoapCode.Created, response.Code);
        CollectionAssert.AreEqual(new[] { "box", name }, response.Options.Select(option => Encoding.UTF8.GetString(option.Value.Span)).ToArray());
        Assert.IsTrue(response.Options.All(option => option.Number == CoapOptionNumber.LocationPath));
        Assert.AreEqual("posted", store.Contents("box/" + name));
    }

    [TestMethod]
    public async Task AnswerAsync_PostToTheRootOverTheLimitOrWithIfMatch_IsRefused()
    {
        var store = new CoapTestStore(new ContentExposureOptions { AllowUploads = true, MaxUploadBytes = 1 });

        Assert.AreEqual(CoapCode.RequestEntityTooLarge, (await AnswerAsync(store, CoapCode.Post, [], body: "ab")).Code);
        Assert.AreEqual(CoapCode.PreconditionFailed, (await AnswerAsync(store, CoapCode.Post, [new CoapOption(CoapOptionNumber.IfMatch, ReadOnlyMemory<byte>.Empty)], body: "a")).Code);
    }

    [TestMethod]
    public async Task AnswerAsync_DeleteOfAMissingOrRefusedPath_Is202AndOfADirectory_Is405()
    {
        var store = new CoapTestStore(CoapTestStore.Uploads).WithDirectory("dir");

        Assert.AreEqual(CoapCode.Deleted, (await AnswerAsync(store, CoapCode.Delete, Path("nope"))).Code);
        Assert.AreEqual(CoapCode.Deleted, (await AnswerAsync(store, CoapCode.Delete, Path("CON"))).Code);
        Assert.AreEqual(CoapCode.MethodNotAllowed, (await AnswerAsync(store, CoapCode.Delete, Path("dir"))).Code);
    }

    [TestMethod]
    public async Task AnswerAsync_DeleteInAReadOnlyShare_Is405()
    {
        var share = new InMemoryContentFileSystem(new ManualTimeProvider());
        using (var stream = share.CreateFileForAsyncWrite(System.IO.Path.Join(InMemoryContentFileSystem.RootPath, "f.txt")))
        {
            stream.WriteByte(1);
        }

        var store = new ContentStore(
            InMemoryContentFileSystem.RootPath,
            new InMemoryContentFileSystem(new ManualTimeProvider()),
            CoapTestStore.Uploads,
            [new ContentShare("ro", InMemoryContentFileSystem.RootPath, share) { IsReadOnly = true }],
            []);
        var answerer = new CoapResourceAnswerer(store, Anonymous, new UnitTestCoapRandomSource());
        var delete = Request(CoapCode.Delete, Path("ro", "f.txt"));

        await using var uploads = new CoapBlock1Uploads(store, new ManualTimeProvider(), 6);

        var response = await answerer.AnswerAsync(delete, CoapRequestOptions.Read(delete), "coap", false, uploads, CancellationToken.None);

        Assert.AreEqual(CoapCode.MethodNotAllowed, response.Code);
    }

    [TestMethod]
    [DataRow(CoapOptionNumber.IfMatch, new byte[0], true, "2.04")]
    [DataRow(CoapOptionNumber.IfMatch, new byte[0], false, "4.12")]
    [DataRow(CoapOptionNumber.IfMatch, new byte[] { 9 }, true, "4.12")]
    [DataRow(CoapOptionNumber.IfNoneMatch, new byte[0], false, "2.01")]
    [DataRow(CoapOptionNumber.IfNoneMatch, new byte[0], true, "4.12")]
    public async Task AnswerAsync_ConditionalPut_IsPerformedOnlyWhenTheConditionHolds(int number, byte[] value, bool exists, string code)
    {
        var store = new CoapTestStore(CoapTestStore.Uploads);
        if (exists)
        {
            store.WithFile("f.txt", "x");
        }

        var response = await AnswerAsync(store, CoapCode.Put, [.. Path("f.txt"), new CoapOption(number, value)], body: "y");

        Assert.AreEqual(code, response.Code.ToString());
    }

    [TestMethod]
    public async Task AnswerAsync_GetWithIfMatchOfTheCurrentETag_IsServedAndOfAnotherIs412()
    {
        var store = new CoapTestStore().WithFile("f.txt", "x");

        var matching = await AnswerAsync(store, CoapCode.Get, [.. Path("f.txt"), new CoapOption(CoapOptionNumber.IfMatch, CoapTestStore.EntityTagOf(1))]);
        var other = await AnswerAsync(store, CoapCode.Get, [.. Path("f.txt"), new CoapOption(CoapOptionNumber.IfMatch, new byte[] { 1 })]);

        Assert.AreEqual(CoapCode.Content, matching.Code);
        Assert.AreEqual(CoapCode.PreconditionFailed, other.Code);
    }

    [TestMethod]
    public async Task AnswerAsync_WellKnownCore_IsServedOnlyUnderTheListingSwitchAndOnlyToGet()
    {
        var listed = new CoapTestStore(UploadsAndListings).WithFile("a.txt", "1");
        var unlisted = new CoapTestStore().WithFile("a.txt", "1");

        var served = await AnswerAsync(listed, CoapCode.Get, WellKnownCore);
        var hidden = await AnswerAsync(unlisted, CoapCode.Get, WellKnownCore);
        var written = await AnswerAsync(listed, CoapCode.Put, WellKnownCore, body: "x");

        Assert.AreEqual(CoapCode.Content, served.Code);
        Assert.AreEqual("</a.txt>;ct=0;sz=1;obs", Encoding.UTF8.GetString(served.Payload.Span));
        Assert.AreEqual(40u, new CoapOption(CoapOptionNumber.ContentFormat, Option(served, CoapOptionNumber.ContentFormat)).ReadUInt());
        Assert.AreEqual(CoapCode.NotFound, hidden.Code);
        Assert.AreEqual(CoapCode.MethodNotAllowed, written.Code);
    }

    [TestMethod]
    public async Task AnswerAsync_WellKnownCoreWithItsETag_Is203()
    {
        var store = new CoapTestStore(CoapTestStore.Listings);
        var entityTag = System.Security.Cryptography.SHA256.HashData(ReadOnlySpan<byte>.Empty)[..8];

        var response = await AnswerAsync(store, CoapCode.Get, [.. WellKnownCore, new CoapOption(CoapOptionNumber.ETag, entityTag)]);

        Assert.AreEqual(CoapCode.Valid, response.Code);
    }

    [TestMethod]
    public async Task AnswerAsync_ResponsesWithAnyOption_CarryEachNonRepeatableOptionAtMostOnce()
    {
        var store = new CoapTestStore(CoapTestStore.Uploads).WithFile("big.txt", new string('x', 3000)).WithFile("d.txt", "abc").WithDirectory("box");
        CoapOption[] validating = [.. Path("d.txt"), new CoapOption(CoapOptionNumber.ETag, CoapTestStore.EntityTagOf(3))];

        CoapResponse[] responses =
        [
            await AnswerAsync(store, CoapCode.Get, Path("big.txt")),
            await AnswerAsync(store, CoapCode.Get, [.. Path("big.txt"), CoapOption.FromUInt(CoapOptionNumber.Size2, 0)]),
            await AnswerAsync(store, CoapCode.Get, validating),
            await AnswerAsync(store, CoapCode.Post, Path("box", string.Empty), body: "posted"),
            await AnswerAsync(store, CoapCode.Put, Path("f.txt"), body: new string('y', 5000)),
        ];

        foreach (var response in responses)
        {
            var repeated = response.Options.GroupBy(option => option.Number).Where(group => group.Count() > 1).Select(group => group.Key);
            CollectionAssert.IsSubsetOf(repeated.ToArray(), new[] { CoapOptionNumber.LocationPath });
        }
    }

    [TestMethod]
    public async Task AnswerAsync_ResponsesWithoutAPayloadByCode_CarryNone_AndAPayloadOnGetOrDeleteIsIgnored()
    {
        var store = new CoapTestStore(CoapTestStore.Uploads).WithFile("d.txt", "abc").WithFile("gone.txt", "bye");

        var valid = await AnswerAsync(store, CoapCode.Get, [.. Path("d.txt"), new CoapOption(CoapOptionNumber.ETag, CoapTestStore.EntityTagOf(3))]);
        var created = await AnswerAsync(store, CoapCode.Put, Path("new.txt"), body: "n");
        var changed = await AnswerAsync(store, CoapCode.Put, Path("d.txt"), body: "n");
        var got = await AnswerAsync(store, CoapCode.Get, Path("gone.txt"), body: "ignored");
        var deleted = await AnswerAsync(store, CoapCode.Delete, Path("gone.txt"), body: "ignored");

        foreach (var response in new[] { valid, created, changed, deleted })
        {
            Assert.IsTrue(response.Payload.IsEmpty);
        }

        Assert.AreEqual(CoapCode.Content, got.Code);
        Assert.AreEqual("bye", Encoding.UTF8.GetString(got.Payload.Span));
        Assert.AreEqual(CoapCode.Deleted, deleted.Code);
        Assert.IsFalse(store.HasFile("gone.txt"));
    }

    [TestMethod]
    public async Task AnswerAsync_ProxyUriBesideUriPath_Is505AndNothingIsServedFromThePath()
    {
        var store = new CoapTestStore().WithFile("d.txt", "abc");

        var response = await AnswerAsync(store, CoapCode.Get, [.. Path("d.txt"), CoapOption.FromString(CoapOptionNumber.ProxyUri, "coap://localhost/d.txt")]);

        Assert.AreEqual(CoapCode.ProxyingNotSupported, response.Code);
        Assert.IsTrue(response.Payload.IsEmpty);
    }

    [TestMethod]
    public async Task AnswerAsync_ConditionsOnARequestThatEndsInNeither2xxNor412_AreIgnored()
    {
        var store = new CoapTestStore(CoapTestStore.Uploads).WithDirectory("dir");
        var ifNoneMatch = new CoapOption(CoapOptionNumber.IfNoneMatch, ReadOnlyMemory<byte>.Empty);
        var wrongIfMatch = new CoapOption(CoapOptionNumber.IfMatch, new byte[] { 9 });

        var missingGet = await AnswerAsync(store, CoapCode.Get, [.. Path("nope"), wrongIfMatch]);
        var missingDirectory = await AnswerAsync(store, CoapCode.Put, [.. Path("none", "f.txt"), ifNoneMatch], body: "x");
        var directoryDelete = await AnswerAsync(store, CoapCode.Delete, [.. Path("dir"), wrongIfMatch]);
        var unservedMethod = await AnswerAsync(store, new CoapCode(0x05), [.. Path("f"), wrongIfMatch]);
        var dotSegment = await AnswerAsync(store, CoapCode.Get, [.. Path(".."), wrongIfMatch]);

        Assert.AreEqual(CoapCode.NotFound, missingGet.Code);
        Assert.AreEqual(CoapCode.NotFound, missingDirectory.Code);
        Assert.AreEqual(CoapCode.MethodNotAllowed, directoryDelete.Code);
        Assert.AreEqual(CoapCode.MethodNotAllowed, unservedMethod.Code);
        Assert.AreEqual(CoapCode.BadRequest, dotSegment.Code);
    }

    [TestMethod]
    public void RequestPathOf_SegmentsWithReservedOrNonAsciiCharacters_ArePercentEncodedWithUpperCaseHex()
    {
        var request = Request(CoapCode.Get, Path("café ü", "a/b", "50%", "~-._"));

        var path = CoapResourceAnswerer.RequestPathOf(CoapRequestOptions.Read(request));

        Assert.AreEqual("/caf%C3%A9%20%C3%BC/a%2Fb/50%25/~-._", path);
    }

    [TestMethod]
    public async Task AnswerAsync_EachBlockOfALargeRepresentation_CarriesTheSameETag()
    {
        var store = new CoapTestStore().WithFile("big.txt", new string('x', 40));
        await using var exchange = new CoapTestExchange(store);

        var first = await exchange.SendAsync(CoapCode.Get, "big.txt", Block(CoapOptionNumber.Block2, "0/0/16"));
        var second = await exchange.SendAsync(CoapCode.Get, "big.txt", Block(CoapOptionNumber.Block2, "1/0/16"));
        var third = await exchange.SendAsync(CoapCode.Get, "big.txt", Block(CoapOptionNumber.Block2, "2/0/16"));

        foreach (var block in new[] { first, second, third })
        {
            CollectionAssert.AreEqual(CoapTestStore.EntityTagOf(40), Option(block, CoapOptionNumber.ETag));
        }
    }

    private static CoapOption[] WellKnownCore => Path(".well-known", "core");

    private static CoapOption[] Path(params string[] segments) =>
        [.. segments.Select(segment => CoapOption.FromString(CoapOptionNumber.UriPath, segment))];

    private static byte[] Option(CoapResponse response, int number) =>
        response.Options.Single(option => option.Number == number).Value.ToArray();

    private static CoapMessage Request(CoapCode method, CoapOption[] options, string? body = null) =>
        new(CoapMessageType.Confirmable, method, 1, new byte[] { 0xC0 }, options, body is null ? default : Encoding.UTF8.GetBytes(body));

    private static async Task<CoapResponse> AnswerAsync(CoapTestStore store, CoapCode method, CoapOption[] options, CoapAccessRules? rules = null, string? body = null)
    {
        var answerer = new CoapResourceAnswerer(store.Store, rules ?? Anonymous, new UnitTestCoapRandomSource());
        await using var uploads = new CoapBlock1Uploads(store.Store, new ManualTimeProvider(), CoapBlockOption.LargestSizeExponent);
        var request = Request(method, options, body);
        return await answerer.AnswerAsync(request, CoapRequestOptions.Read(request), "coap", peerIsAuthenticated: false, uploads, CancellationToken.None);
    }
}
