using Surl.Content;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// The Data Lake endpoint's Path - Update <c>setProperties</c> and Path - Get Properties, whose
/// requests and fields are written from the Data Lake Storage Gen2 REST API pages "Path - Update"
/// (<c>x-ms-properties</c>: <c>n1=base64,n2=base64</c>) and "Path - Get Properties" (ADR-0109,
/// decision 1).
/// </summary>
[TestClass]
public sealed class AzurePathSetPropertiesTests
{
    private const string EmptyETag = "\"d41d8cd98f00b204e9800998ecf8427e\"";

    // hdi_isfolder=true and owner=Stewart, each value the base64 of its UTF-8 bytes.
    private const string TwoProperties = "hdi_isfolder=dHJ1ZQ==,owner=U3Rld2FydA==";

    [TestMethod]
    public async Task ServeAsync_SetProperties_StoresThemInTheIndexAndGetPropertiesReturnsThem()
    {
        var harness = Writable();
        harness.WriteFile("photos/a.txt", string.Empty);

        var response = await harness.SendWithBearerAsync("PATCH", "/photos/a.txt?action=setProperties", $"x-ms-properties: {TwoProperties}", "Content-Length: 0");
        var read = await harness.SendWithBearerAsync("HEAD", "/photos/a.txt");

        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\n{AzureServerHarness.CommonFields(response)}ETag: {EmptyETag}\r\nLast-Modified: {AzureServerHarness.NowHttpDate}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
            response);
        var stored = await harness.Index.ReadAsync("photos/a.txt", CancellationToken.None);
        CollectionAssert.AreEqual(
            new KeyValuePair<string, string>[] { new("hdi_isfolder", "true"), new("owner", "Stewart") },
            stored.Properties!.UserMetadata.ToArray());
        StringAssert.Contains(read, $"x-ms-resource-type: file\r\nx-ms-properties: {TwoProperties}\r\nx-ms-meta-hdi_isfolder: true\r\nx-ms-meta-owner: Stewart\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_SetPropertiesWithoutTheField_RemovesEveryPropertyAndKeepsTheHeaderFields()
    {
        var harness = Writable();
        await harness.SendWithBearerAsync("PUT", "/photos/a.txt?resource=file", "x-ms-content-type: text/plain");
        await harness.SendWithBearerAsync("PATCH", "/photos/a.txt?action=setProperties", $"x-ms-properties: {TwoProperties}", "Content-Length: 0");

        var response = await harness.SendWithBearerAsync("PATCH", "/photos/a.txt?action=setProperties", "Content-Length: 0");
        var read = await harness.SendWithBearerAsync("HEAD", "/photos/a.txt");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        StringAssert.Contains(read, "Content-Type: text/plain\r\n");
        Assert.IsFalse(read.Contains("x-ms-properties", StringComparison.Ordinal), read);
    }

    [TestMethod]
    public async Task ServeAsync_SetPropertiesWithHeaderFields_ReplacesTheStoredOnes()
    {
        var harness = Writable();
        harness.WriteFile("photos/a.txt", string.Empty);

        await harness.SendWithBearerAsync(
            "PATCH",
            "/photos/a.txt?action=setProperties",
            "x-ms-content-type: text/plain",
            "x-ms-content-encoding: gzip",
            "x-ms-content-language: en",
            "x-ms-content-disposition: inline",
            "x-ms-cache-control: no-cache",
            "Content-Length: 0");
        var read = await harness.SendWithBearerAsync("HEAD", "/photos/a.txt");

        StringAssert.Contains(read, "Content-Type: text/plain\r\n");
        StringAssert.Contains(read, "Content-Encoding: gzip\r\nContent-Language: en\r\nContent-Disposition: inline\r\nCache-Control: no-cache\r\n");
    }

    [TestMethod]
    [DataRow("noequals", DisplayName = "a pair without =")]
    [DataRow("=dHJ1ZQ==", DisplayName = "a pair without a name")]
    [DataRow("a=***", DisplayName = "a value that is not base64")]
    [DataRow("a=/w==", DisplayName = "a value that is not UTF-8")]
    [DataRow("1a=dHJ1ZQ==", DisplayName = "a name that is no identifier")]
    [DataRow("a=dHJ1ZQ==,a=dHJ1ZQ==", DisplayName = "a name given twice")]
    public async Task ServeAsync_SetPropertiesThatDoNotRead_Is400InvalidHeaderValue(string properties)
    {
        var harness = Writable();
        harness.WriteFile("photos/a.txt", string.Empty);

        var response = await harness.SendWithBearerAsync("PATCH", "/photos/a.txt?action=setProperties", $"x-ms-properties: {properties}", "Content-Length: 0");

        Assert.AreEqual(
            AzureServerHarness.ExpectedJsonError(response, "400 Bad Request", "InvalidHeaderValue", "The value provided for one of the HTTP headers was not in the correct format."),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_SetPropertiesOfAMissingPath_Is404PathNotFound()
    {
        var harness = Writable();

        var response = await harness.SendWithBearerAsync("PATCH", "/photos/a.txt?action=setProperties", "Content-Length: 0");

        StringAssert.Contains(response, "\"code\":\"PathNotFound\"");
    }

    [TestMethod]
    public async Task ServeAsync_SetPropertiesOfANameTheStoreRefuses_Is404PathNotFound()
    {
        var harness = Writable();

        var response = await harness.SendWithBearerAsync("PATCH", "/photos/a%00b?action=setProperties", "Content-Length: 0");

        StringAssert.Contains(response, "\"code\":\"PathNotFound\"");
    }

    [TestMethod]
    public async Task ServeAsync_SetPropertiesOfADirectory_Is409ResourceTypeMismatch()
    {
        var harness = Writable();
        harness.CreateFolder("photos/2026");

        var response = await harness.SendWithBearerAsync("PATCH", "/photos/2026?action=setProperties", "Content-Length: 0");

        StringAssert.Contains(response, "\"code\":\"ResourceTypeMismatch\"");
    }

    [TestMethod]
    public async Task ServeAsync_SetPropertiesWithUploadsOff_Is403AuthorizationPermissionMismatch()
    {
        var harness = new AzureServerHarness().ServingDataLake();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", string.Empty);

        var response = await harness.SendWithBearerAsync("PATCH", "/photos/a.txt?action=setProperties", "Content-Length: 0");

        StringAssert.Contains(response, "\"code\":\"AuthorizationPermissionMismatch\"");
    }

    [TestMethod]
    public async Task ServeAsync_SetPropertiesPastTheMetadataLimit_Is400MetadataTooLarge()
    {
        var harness = Writable();
        harness.WriteFile("photos/a.txt", string.Empty);
        var large = Convert.ToBase64String(new byte[9000].Select(_ => (byte)'a').ToArray());

        var response = await harness.SendWithBearerAsync("PATCH", "/photos/a.txt?action=setProperties", $"x-ms-properties: big={large}", "Content-Length: 0");

        StringAssert.Contains(response, "\"code\":\"MetadataTooLarge\"");
    }

    [TestMethod]
    public async Task ServeAsync_SetPropertiesOfAFileInAShare_KeepsNothingAndIs200()
    {
        var shareFileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(AzureServerHarness.Now));
        AzureServerHarness.WriteFile(shareFileSystem, "a.txt", "a");
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable, shares: [new ContentShare("shared", AzureServerHarness.Root, shareFileSystem)]).ServingDataLake();

        var response = await harness.SendWithBearerAsync("PATCH", "/shared/a.txt?action=setProperties", $"x-ms-properties: {TwoProperties}", "Content-Length: 0");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        Assert.AreEqual(0, harness.Index.Count);
    }

    [TestMethod]
    public async Task ServeAsync_SetPropertiesWhenTheIndexCannotBeWritten_NotesIt()
    {
        var harness = new AzureServerHarness(maxTotalBytes: 0, persistsIndex: true, exposure: AzureServerHarness.Writable).ServingDataLake();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", string.Empty);

        var response = await harness.SendWithBearerAsync("PATCH", "/photos/a.txt?action=setProperties", $"x-ms-properties: {TwoProperties}", "Content-Length: 0");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        Assert.IsTrue(harness.Log.Notes.Any(note => note.StartsWith("The object metadata index could not be written: ", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow("/photos/2026", DisplayName = "a directory")]
    [DataRow("/photos/2026?action=getStatus", DisplayName = "a directory's status")]
    public async Task ServeAsync_GetPropertiesOfADirectory_AnswersItsResourceType(string target)
    {
        var harness = Writable();
        harness.CreateFolder("photos/2026");

        var response = await harness.SendWithBearerAsync("HEAD", target);

        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\n{AzureServerHarness.CommonFields(response)}Content-Length: 0\r\nETag: {AzurePathCreate.DirectoryETag(AzureServerHarness.Now)}\r\n"
                + $"Last-Modified: {AzureServerHarness.NowHttpDate}\r\nx-ms-resource-type: directory\r\nConnection: close\r\n\r\n",
            response);
    }

    [TestMethod]
    public async Task ServeAsync_GetPropertiesOfAFile_AnswersItsResourceTypeWithoutProperties()
    {
        var harness = Writable();
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendWithBearerAsync("HEAD", "/photos/a.txt?action=getStatus");

        StringAssert.Contains(response, "\r\nx-ms-resource-type: file\r\nConnection: close\r\n\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_GetAccessControl_Is400InvalidQueryParameterValue()
    {
        var harness = Writable();
        harness.WriteFile("photos/a.txt", "a");

        var response = await harness.SendWithBearerAsync("GET", "/photos/a.txt?action=getAccessControl");

        Assert.AreEqual(
            AzureServerHarness.ExpectedJsonError(response, "400 Bad Request", "InvalidQueryParameterValue", "An invalid value was specified for one of the query parameters in the request URI."),
            response);
    }

    private static AzureServerHarness Writable()
    {
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable).ServingDataLake();
        harness.CreateFolder("photos");

        return harness;
    }
}
