namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// The path-style form, <c>http://&lt;ip&gt;:&lt;port&gt;/&lt;account&gt;/&lt;container&gt;/&lt;blob&gt;</c>
/// ("Naming and Referencing Containers, Blobs, and Metadata"; ADR-0109, decision 2): with an
/// IP-literal or <c>localhost</c> <c>Host</c>, a first segment naming a named account is removed
/// before the container is read.
/// </summary>
[TestClass]
public sealed class AzurePathStyleTests
{
    private const string Version = "x-ms-version: 2025-01-05";

    private const string Bearer = $"Authorization: Bearer {UnitTestBearerAuthenticationPolicy.Token}";

    // Container "photos" holds "photos says hello"; container "account1" holds "account1 says hello".
    private static AzureServerHarness Harness(params string[] namedAccounts)
    {
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable);
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "photos says hello");
        harness.CreateFolder("account1");
        harness.CreateFolder("account1/photos");
        harness.WriteFile("account1/photos/a.txt", "account1 says hello");
        harness.Policy.NamedAccounts.UnionWith(namedAccounts);

        return harness;
    }

    private static Task<string> SendAsync(AzureServerHarness harness, string method, string target, string host) =>
        harness.SendRawAsync($"{method} {target} HTTP/1.1\r\nHost: {host}\r\n{Version}\r\n{Bearer}\r\nConnection: close\r\n\r\n");

    [TestMethod]
    public async Task Read_IpHostAndANamedAccountSegment_ServesTheContainerAfterIt()
    {
        var harness = Harness("account1");

        var response = await SendAsync(harness, "GET", "/account1/photos/a.txt", "127.0.0.1:10000");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        StringAssert.EndsWith(response, "\r\n\r\nphotos says hello");
    }

    [TestMethod]
    public async Task Read_IpHostAndASegmentNamingNoAccount_ServesThatContainer()
    {
        var harness = Harness("someoneelse");

        var response = await SendAsync(harness, "GET", "/account1/photos/a.txt", "127.0.0.1:10000");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        StringAssert.EndsWith(response, "\r\n\r\naccount1 says hello");
    }

    [TestMethod]
    public async Task Read_AccountHostAndANamedAccountSegment_ServesThatContainer()
    {
        var harness = Harness("account1");

        var response = await SendAsync(harness, "GET", "/account1/photos/a.txt", "account1.blob.core.windows.net");

        StringAssert.EndsWith(response, "\r\n\r\naccount1 says hello");
        Assert.AreEqual("account1", harness.Policy.Requests[0].AzureStorage!.AccountName);
        Assert.AreEqual("/account1/photos/a.txt", harness.Policy.Requests[0].AzureStorage!.ResourcePath);
    }

    [TestMethod]
    public async Task Read_PathStyle_TellsTheSessionTheSegmentsAccountTheResourceAfterItAndTheTargetAsSent()
    {
        var harness = Harness("account1");

        await SendAsync(harness, "GET", "/account1/photos/a.txt?timeout=20", "localhost:10000");

        var request = harness.Policy.Requests[0];
        Assert.AreEqual("account1", request.AzureStorage!.AccountName);
        Assert.AreEqual("/photos/a.txt", request.AzureStorage.ResourcePath);
        Assert.AreEqual("/account1/photos/a.txt?timeout=20", request.Target);
    }

    [TestMethod]
    public async Task Read_PercentEncodedAccountSegment_IsDecodedBeforeItIsLookedUp()
    {
        var harness = Harness("account1");

        var response = await SendAsync(harness, "GET", "/account%31/photos/a.txt", "[::1]:10000");

        StringAssert.EndsWith(response, "\r\n\r\nphotos says hello");
    }

    [TestMethod]
    public async Task Read_AccountSegmentAlone_IsTheAccountsListContainersWithItsQuery()
    {
        var harness = Harness("account1");

        var response = await SendAsync(harness, "GET", "/account1?comp=list", "127.0.0.1");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        StringAssert.Contains(response, "<Name>photos</Name>");
        Assert.AreEqual("/", harness.Policy.Requests[0].AzureStorage!.ResourcePath);
    }

    [TestMethod]
    public async Task Read_IpHostAndTheAccountRoot_HasNoSegmentToRemove()
    {
        var harness = Harness("account1");

        await SendAsync(harness, "GET", "/?comp=list", "127.0.0.1");

        Assert.AreEqual("127", harness.Policy.Requests[0].AzureStorage!.AccountName);
    }

    [TestMethod]
    public async Task Read_DataLakeEndpoint_RemovesTheSegmentToo()
    {
        var harness = Harness("account1").ServingDataLake();

        var response = await SendAsync(harness, "GET", "/account1/photos/a.txt", "127.0.0.1:10000");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        StringAssert.EndsWith(response, "\r\n\r\nphotos says hello");
    }

    [TestMethod]
    [DataRow("localhost", true)]
    [DataRow("LocalHost:10000", true)]
    [DataRow("127.0.0.1", true)]
    [DataRow("10.0.0.7:10000", true)]
    [DataRow("[::1]", true)]
    [DataRow("[2001:db8::1]:10000", true)]
    [DataRow("[127.0.0.1]", false, DisplayName = "an IPv4 address in brackets")]
    [DataRow("[::1", false, DisplayName = "an unclosed bracket")]
    [DataRow("[account1]", false, DisplayName = "a name in brackets")]
    [DataRow("12345", false, DisplayName = "a one-label name of digits")]
    [DataRow("1.2.3.4.5", false, DisplayName = "five dotted parts")]
    [DataRow("1.2.3.x", false, DisplayName = "four dotted parts, one not a number")]
    [DataRow("account1.blob.core.windows.net", false)]
    [DataRow("localhost.example", false)]
    public void IsAddressedByIpOrLocalhost_Host_IsTrueOnlyForAnIpLiteralOrLocalhost(string host, bool expected)
    {
        var isAddressed = AzurePathStyle.IsAddressedByIpOrLocalhost(host);

        Assert.AreEqual(expected, isAddressed);
    }

    [TestMethod]
    public void IsAddressedByIpOrLocalhost_NoHost_IsFalse()
    {
        var isAddressed = AzurePathStyle.IsAddressedByIpOrLocalhost(null);

        Assert.IsFalse(isAddressed);
    }
}
