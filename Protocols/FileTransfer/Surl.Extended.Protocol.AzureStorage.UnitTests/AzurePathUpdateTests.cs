using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// The Data Lake endpoint's Path - Update <c>append</c> and <c>flush</c>, whose bytes are written
/// from the Data Lake Storage Gen2 REST API pages "Path - Create", "Path - Update" and "Path -
/// Read", and their <c>DataLakeStorageError</c> (ADR-0109, decision 5.5).
/// </summary>
[TestClass]
public sealed class AzurePathUpdateTests
{
    // MD5("hello world"), the ETag the flushed file answers.
    private const string HelloWorldETag = "\"5eb63bbbe01eeed093cb22bb8f5acdc3\"";

    [TestMethod]
    public async Task ServeAsync_FileCreatedAppendedInTwoPartsAndFlushed_IsReadBack()
    {
        var harness = Writable();

        var created = await harness.SendWithBearerAsync("PUT", "/photos/a.txt?resource=file");
        var firstAppend = await harness.SendWithBearerAndBodyAsync("PATCH", "/photos/a.txt?action=append&position=0", "hello ");
        var secondAppend = await harness.SendWithBearerAndBodyAsync("PATCH", "/photos/a.txt?action=append&position=6", "world");
        var flushed = await harness.SendWithBearerAsync("PATCH", "/photos/a.txt?action=flush&position=11");
        var read = await harness.SendWithBearerAsync("GET", "/photos/a.txt");

        Assert.AreEqual(
            $"HTTP/1.1 201 Created\r\n{AzureServerHarness.CommonFields(created)}ETag: \"d41d8cd98f00b204e9800998ecf8427e\"\r\nLast-Modified: {AzureServerHarness.NowHttpDate}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
            created);
        Assert.AreEqual($"HTTP/1.1 202 Accepted\r\n{AzureServerHarness.CommonFields(firstAppend)}Content-Length: 0\r\nConnection: close\r\n\r\n", firstAppend);
        Assert.AreEqual($"HTTP/1.1 202 Accepted\r\n{AzureServerHarness.CommonFields(secondAppend)}Content-Length: 0\r\nConnection: close\r\n\r\n", secondAppend);
        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\n{AzureServerHarness.CommonFields(flushed)}ETag: {HelloWorldETag}\r\nLast-Modified: {AzureServerHarness.NowHttpDate}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
            flushed);
        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\n{AzureServerHarness.CommonFields(read)}Content-Length: 11\r\nContent-Type: text/plain\r\nContent-MD5: XrY7u+Ae7tCTyyK7j1rNww==\r\n"
                + $"ETag: {HelloWorldETag}\r\nLast-Modified: {AzureServerHarness.NowHttpDate}\r\nAccept-Ranges: bytes\r\nx-ms-resource-type: file\r\nConnection: close\r\n\r\nhello world",
            read);
    }

    [TestMethod]
    public async Task ServeAsync_AppendsBeforeTheFlush_AreInvisible()
    {
        var harness = Writable();
        harness.WriteFile("photos/a.txt", "old");

        await harness.SendWithBearerAndBodyAsync("PATCH", "/photos/a.txt?action=append&position=3", " new");

        Assert.AreEqual("old", harness.ReadFile("photos/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_AppendsOutOfOrder_AreFlushedInPositionOrder()
    {
        var harness = Writable();
        harness.WriteFile("photos/a.txt", "");

        await harness.SendWithBearerAndBodyAsync("PATCH", "/photos/a.txt?action=append&position=6", "world");
        await harness.SendWithBearerAndBodyAsync("PATCH", "/photos/a.txt?action=append&position=0", "hello ");
        var flushed = await harness.SendWithBearerAsync("PATCH", "/photos/a.txt?action=flush&position=11");

        StringAssert.StartsWith(flushed, "HTTP/1.1 200 OK\r\n");
        Assert.AreEqual("hello world", harness.ReadFile("photos/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_FlushShortOfTheStagedData_DropsTheRest()
    {
        var harness = Writable();
        harness.WriteFile("photos/a.txt", "");

        await harness.SendWithBearerAndBodyAsync("PATCH", "/photos/a.txt?action=append&position=0", "hello world");
        var flushed = await harness.SendWithBearerAsync("PATCH", "/photos/a.txt?action=flush&position=5&close=true");

        StringAssert.StartsWith(flushed, "HTTP/1.1 200 OK\r\n");
        Assert.AreEqual("hello", harness.ReadFile("photos/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_AppendWithFlush_AppendsAndCommits()
    {
        var harness = Writable();
        harness.WriteFile("photos/a.txt", "hello ");

        var response = await harness.SendWithBearerAndBodyAsync("PATCH", "/photos/a.txt?action=append&position=6&flush=true", "world");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        StringAssert.Contains(response, $"ETag: {HelloWorldETag}\r\n");
        Assert.AreEqual("hello world", harness.ReadFile("photos/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_FlushKeepsTheStoredContentType()
    {
        var harness = Writable();
        await harness.SendWithBearerAsync("PUT", "/photos/a.txt?resource=file", "x-ms-content-type: text/plain");

        await harness.SendWithBearerAndBodyAsync("PATCH", "/photos/a.txt?action=append&position=0&flush=true", "hello world");
        var read = await harness.SendWithBearerAsync("HEAD", "/photos/a.txt");

        StringAssert.Contains(read, "Content-Type: text/plain\r\n");
        StringAssert.Contains(read, $"ETag: {HelloWorldETag}\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_FlushOfAFileInAShare_IsCommitted()
    {
        var shareFileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(AzureServerHarness.Now));
        AzureServerHarness.WriteFile(shareFileSystem, "a.txt", "");
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable, shares: [new ContentShare("shared", AzureServerHarness.Root, shareFileSystem)]).ServingDataLake();

        var response = await harness.SendWithBearerAndBodyAsync("PATCH", "/shared/a.txt?action=append&position=0&flush=true", "hello world");

        StringAssert.Contains(response, $"ETag: {HelloWorldETag}\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_FlushAtTheWrongPosition_Is400InvalidFlushPosition()
    {
        var harness = Writable();
        harness.WriteFile("photos/a.txt", "");
        await harness.SendWithBearerAndBodyAsync("PATCH", "/photos/a.txt?action=append&position=0", "hello");

        var response = await harness.SendWithBearerAsync("PATCH", "/photos/a.txt?action=flush&position=11");

        Assert.AreEqual(
            AzureServerHarness.ExpectedJsonError(
                response,
                "400 Bad Request",
                "InvalidFlushPosition",
                "The uploaded data is not contiguous or the position query parameter value is not equal to the length of the file after appending the uploaded data."),
            response);
        Assert.AreEqual(string.Empty, harness.ReadFile("photos/a.txt"));
    }

    [TestMethod]
    [DataRow(5, DisplayName = "the file's length")]
    [DataRow(3, DisplayName = "not the file's length")]
    public async Task ServeAsync_FlushWithNothingStaged_SucceedsOnlyAtTheFilesLength(int position)
    {
        var harness = Writable();
        harness.WriteFile("photos/a.txt", "hello");

        var response = await harness.SendWithBearerAsync("PATCH", $"/photos/a.txt?action=flush&position={position}");

        StringAssert.StartsWith(response, position == 5 ? "HTTP/1.1 200 OK\r\n" : "HTTP/1.1 400 Bad Request\r\n");
        Assert.AreEqual("hello", harness.ReadFile("photos/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_AppendWithoutUploadsAllowed_Is403AuthorizationPermissionMismatch()
    {
        var harness = new AzureServerHarness(exposure: new ContentExposureOptions { ListDirectories = true }).ServingDataLake();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "");

        var response = await harness.SendWithBearerAndBodyAsync("PATCH", "/photos/a.txt?action=append&position=0", "hello");

        Assert.AreEqual(
            AzureServerHarness.ExpectedJsonError(response, "403 Forbidden", "AuthorizationPermissionMismatch", "This request is not authorized to perform this operation using this permission."),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_AppendWithoutAPosition_Is400MissingRequiredQueryParameter()
    {
        var harness = Writable();

        var response = await harness.SendWithBearerAndBodyAsync("PATCH", "/photos/a.txt?action=append", "hello");

        Assert.AreEqual(
            AzureServerHarness.ExpectedJsonError(response, "400 Bad Request", "MissingRequiredQueryParameter", "A query parameter that's mandatory for this request is not specified."),
            response);
    }

    [TestMethod]
    [DataRow("/photos/a.txt?action=append&position=-1", DisplayName = "a negative position")]
    [DataRow("/photos/a.txt?action=append&position=x", DisplayName = "no number")]
    [DataRow("/photos/a.txt?action=flush&position=0&retainUncommittedData=true", DisplayName = "retaining uncommitted data")]
    public async Task ServeAsync_UpdateWithAQueryValueNotServed_Is400InvalidQueryParameterValue(string target)
    {
        var harness = Writable();
        harness.WriteFile("photos/a.txt", "");

        var response = await harness.SendWithBearerAsync("PATCH", target, "Content-Length: 0");

        StringAssert.StartsWith(response, "HTTP/1.1 400 Bad Request\r\n");
        StringAssert.Contains(response, "{\"error\":{\"code\":\"InvalidQueryParameterValue\",");
    }

    [TestMethod]
    public async Task ServeAsync_FlushWithRetainUncommittedDataFalse_IsCommitted()
    {
        var harness = Writable();
        harness.WriteFile("photos/a.txt", "");

        var response = await harness.SendWithBearerAsync("PATCH", "/photos/a.txt?action=flush&position=0&retainUncommittedData=false");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_AppendBeforeTheCommittedEnd_Is400InvalidQueryParameterValue()
    {
        var harness = Writable();
        harness.WriteFile("photos/a.txt", "hello");

        var response = await harness.SendWithBearerAndBodyAsync("PATCH", "/photos/a.txt?action=append&position=2", "XX");

        StringAssert.StartsWith(response, "HTTP/1.1 400 Bad Request\r\n");
        StringAssert.Contains(response, "{\"error\":{\"code\":\"InvalidQueryParameterValue\",");
        Assert.AreEqual("hello", harness.ReadFile("photos/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_AppendWithoutAContentLength_Is411MissingContentLengthHeader()
    {
        var harness = Writable();
        harness.WriteFile("photos/a.txt", "");

        var response = await harness.SendWithBearerAsync("PATCH", "/photos/a.txt?action=append&position=0");

        Assert.AreEqual(
            AzureServerHarness.ExpectedJsonError(response, "411 Length Required", "MissingContentLengthHeader", "The Content-Length header was not specified."),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_AppendOfNoBytes_StagesNothing()
    {
        var harness = Writable();
        harness.WriteFile("photos/a.txt", "");

        var response = await harness.SendWithBearerAsync("PATCH", "/photos/a.txt?action=append&position=0", "Content-Length: 0");

        StringAssert.StartsWith(response, "HTTP/1.1 202 Accepted\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_AppendWithAnUnreadableBody_Is400InvalidInput()
    {
        var harness = Writable();
        harness.WriteFile("photos/a.txt", "");

        var response = await harness.SendRawAsync($"PATCH /photos/a.txt?action=append&position=0 HTTP/1.1\r\nHost: h\r\n{BearerFields()}Transfer-Encoding: gzip\r\n\r\n");

        StringAssert.StartsWith(response, "HTTP/1.1 400 Bad Request\r\n");
        StringAssert.Contains(response, "\"code\":\"InvalidInput\"");
    }

    [TestMethod]
    public async Task ServeAsync_AppendWhoseBodyEndsEarly_Is400InvalidInput()
    {
        var harness = Writable();
        harness.WriteFile("photos/a.txt", "");

        var response = await harness.SendRawAsync($"PATCH /photos/a.txt?action=append&position=0 HTTP/1.1\r\nHost: h\r\n{BearerFields()}Content-Length: 20\r\n\r\nhello");

        StringAssert.Contains(response, "\"code\":\"InvalidInput\"");
    }

    [TestMethod]
    public async Task ServeAsync_AppendAnnouncedPastMaxFilesize_Is413RequestBodyTooLarge()
    {
        var harness = Writable(AzureServerHarness.Writable with { MaxUploadBytes = 10 });
        harness.WriteFile("photos/a.txt", "");

        var response = await harness.SendWithBearerAndBodyAsync("PATCH", "/photos/a.txt?action=append&position=0", "hello world");

        StringAssert.StartsWith(response, "HTTP/1.1 413 Content Too Large\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_AppendWithNoUploadLimit_IsStaged()
    {
        var harness = Writable(AzureServerHarness.Writable with { MaxUploadBytes = 0 });
        harness.WriteFile("photos/a.txt", "");

        var response = await harness.SendWithBearerAndBodyAsync("PATCH", "/photos/a.txt?action=append&position=0", "hello world");

        StringAssert.StartsWith(response, "HTTP/1.1 202 Accepted\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_ChunkedAppendPastMaxFilesize_Is413AndDiscardsTheStaging()
    {
        var harness = Writable(AzureServerHarness.Writable with { MaxUploadBytes = 10 });
        harness.WriteFile("photos/a.txt", "");

        var tooLarge = await harness.SendRawAsync(
            $"PATCH /photos/a.txt?action=append&position=0 HTTP/1.1\r\nHost: h\r\n{BearerFields()}Transfer-Encoding: chunked\r\nConnection: close\r\n\r\nb\r\nhello world\r\n0\r\n\r\n");
        var next = await harness.SendWithBearerAndBodyAsync("PATCH", "/photos/a.txt?action=append&position=0", "hello");

        StringAssert.StartsWith(tooLarge, "HTTP/1.1 413 Content Too Large\r\n");
        StringAssert.StartsWith(next, "HTTP/1.1 202 Accepted\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_FlushWithABody_Is400ContentLengthMustBeZero()
    {
        var harness = Writable();
        harness.WriteFile("photos/a.txt", "");

        var response = await harness.SendWithBearerAndBodyAsync("PATCH", "/photos/a.txt?action=flush&position=0", "x");

        Assert.AreEqual(
            AzureServerHarness.ExpectedJsonError(response, "400 Bad Request", "ContentLengthMustBeZero", "The Content-Length request header must be zero."),
            response);
    }

    [TestMethod]
    [DataRow("/photos/missing.txt", DisplayName = "no file")]
    [DataRow("/photos/a%00b", DisplayName = "a name the store refuses")]
    public async Task ServeAsync_UpdateOfAMissingPath_Is404PathNotFound(string path)
    {
        var harness = Writable();

        var response = await harness.SendWithBearerAsync("PATCH", $"{path}?action=flush&position=0");

        Assert.AreEqual(AzureServerHarness.ExpectedJsonError(response, "404 Not Found", "PathNotFound", "The specified path does not exist."), response);
    }

    [TestMethod]
    public async Task ServeAsync_UpdateOfADirectory_Is409ResourceTypeMismatch()
    {
        var harness = Writable();
        harness.CreateFolder("photos/2026");

        var response = await harness.SendWithBearerAsync("PATCH", "/photos/2026?action=flush&position=0");

        Assert.AreEqual(
            AzureServerHarness.ExpectedJsonError(response, "409 Conflict", "ResourceTypeMismatch", "The resource type specified in the request does not match the type of the resource."),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_FileRecreatedAfterAnAppend_DropsTheStagedBytes()
    {
        var harness = Writable();
        harness.WriteFile("photos/a.txt", "");
        await harness.SendWithBearerAndBodyAsync("PATCH", "/photos/a.txt?action=append&position=0", "hello");

        await harness.SendWithBearerAsync("PUT", "/photos/a.txt?resource=file");
        var flushed = await harness.SendWithBearerAsync("PATCH", "/photos/a.txt?action=flush&position=5");

        StringAssert.Contains(flushed, "\"code\":\"InvalidFlushPosition\"");
    }

    [TestMethod]
    public async Task ServeAsync_AppendPastTheStagedPathLimit_Is503ServerBusy()
    {
        var harness = Writable();
        for (var file = 0; file <= AzureStagingArea.MaxStagings; file++)
        {
            harness.WriteFile($"photos/{file}.txt", "");
        }

        for (var file = 0; file < AzureStagingArea.MaxStagings; file++)
        {
            await harness.SendWithBearerAndBodyAsync("PATCH", $"/photos/{file}.txt?action=append&position=0", "x");
        }

        var response = await harness.SendWithBearerAndBodyAsync("PATCH", $"/photos/{AzureStagingArea.MaxStagings}.txt?action=append&position=0", "x");

        Assert.AreEqual(
            AzureServerHarness.ExpectedJsonError(response, "503 Service Unavailable", "ServerBusy", "The server is currently unable to receive requests. Please retry your request."),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_StagingUntouchedForAWeek_IsDiscardedWhenTheNextBegins()
    {
        var harness = Writable();
        harness.WriteFile("photos/a.txt", "");
        harness.WriteFile("photos/b.txt", "");
        await harness.SendWithBearerAndBodyAsync("PATCH", "/photos/a.txt?action=append&position=0", "hello");

        harness.TimeProvider.UtcNow = AzureServerHarness.Now + AzureStagingArea.StagedLifetime + TimeSpan.FromSeconds(1);
        await harness.SendWithBearerAndBodyAsync("PATCH", "/photos/b.txt?action=append&position=0", "x");
        var flushed = await harness.SendWithBearerAsync("PATCH", "/photos/a.txt?action=flush&position=5");

        StringAssert.Contains(flushed, "\"code\":\"InvalidFlushPosition\"");
    }

    [TestMethod]
    public async Task ServeAsync_FolderAppearingAtTheNameBeforeTheFlush_Is403AuthorizationPermissionMismatch()
    {
        var harness = Writable();
        harness.WriteFile("photos/a.txt", "");
        var inner = new InMemoryConnection([
            Encoding.Latin1.GetBytes($"PATCH /photos/a.txt?action=append&position=0&flush=true HTTP/1.1\r\nHost: h\r\n{BearerFields()}Content-Length: 5\r\nConnection: close\r\n\r\n"),
            Encoding.Latin1.GetBytes("hello")]);
        var connection = new UnitTestActingConnection(inner, () =>
        {
            harness.FileSystem.DeleteFile(Path.Join(AzureServerHarness.Root, "photos", "a.txt"));
            harness.CreateFolder("photos/a.txt");
        });

        await harness.Server.ServeAsync(connection, harness.Context());

        StringAssert.StartsWith(Encoding.UTF8.GetString(inner.WrittenBytes), "HTTP/1.1 403 Forbidden\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_IndexThatCannotBeWritten_IsNotedAndTheFlushStands()
    {
        var harness = new AzureServerHarness(maxTotalBytes: 11, persistsIndex: true, exposure: AzureServerHarness.Writable).ServingDataLake();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "");

        var response = await harness.SendWithBearerAndBodyAsync("PATCH", "/photos/a.txt?action=append&position=0&flush=true", "hello world");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        Assert.IsTrue(harness.Log.Notes.Any(note => note.StartsWith("The object metadata index could not be written: ", StringComparison.Ordinal)));
    }

    private static string BearerFields() => string.Concat(AzureServerHarness.Bearer.Select(field => field + "\r\n"));

    private static AzureServerHarness Writable(ContentExposureOptions? exposure = null)
    {
        var harness = new AzureServerHarness(exposure: exposure ?? AzureServerHarness.Writable).ServingDataLake();
        harness.CreateFolder("photos");

        return harness;
    }
}
