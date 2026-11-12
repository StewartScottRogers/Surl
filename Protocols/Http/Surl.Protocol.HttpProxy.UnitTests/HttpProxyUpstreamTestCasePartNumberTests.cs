using Surl.HttpMessage;

namespace Surl.Protocol.HttpProxy;

[TestClass]
public sealed class HttpProxyUpstreamTestCasePartNumberTests
{
    // NTLM messages: "NTLMSSP\0" then the type, little-endian.
    private static readonly string NtlmType1 = Convert.ToBase64String([.. "NTLMSSP\0"u8, 1, 0, 0, 0]);
    private static readonly string NtlmType2 = Convert.ToBase64String([.. "NTLMSSP\0"u8, 2, 0, 0, 0]);
    private static readonly string NtlmType3 = Convert.ToBase64String([.. "NTLMSSP\0"u8, 3, 0, 0, 0, 9]);

    [TestMethod]
    [DataRow("GET", "/1220", 0)]
    [DataRow("GET", "/12200002", 2)]
    [DataRow("GET", "/we/want/12200017?foo=1", 17)]
    [DataRow("GET", "/12200003#frag", 3)]
    [DataRow("GET", "/10000", 0)]
    [DataRow("GET", "/10001", 1)]
    [DataRow("GET", "/0000012", 0)]
    [DataRow("GET", "/", 0)]
    [DataRow("GET", "http://test.remote.example.com/path/12200005", 5)]
    [DataRow("CONNECT", "test.remote.example.com.12200001:443", 1)]
    [DataRow("CONNECT", "test.12200009", 9)]
    [DataRow("CONNECT", "127.0.0.1:8990", 0)]
    public void FromTarget_TheTestNumberOverTenThousand_GivesItsRemainder(string method, string target, int expected)
    {
        var number = HttpProxyUpstreamTestCasePartNumber.FromTarget(method, target);

        Assert.AreEqual(expected, number);
    }

    [TestMethod]
    [DataRow("Authorization", "Digest username=\"x\"", 1000)]
    [DataRow("Proxy-Authorization", "Digest username=\"x\"", 1000)]
    [DataRow("Authorization", "Basic eDp5", 0)]
    [DataRow("Authorization", "Bearer abc", 0)]
    [DataRow("Authorization", "", 0)]
    [DataRow("Authorization", "NTLM", 0)]
    [DataRow("Authorization", "NTLM !!!", 0)]
    [DataRow("Authorization", "NTLM TlRMTQ==", 0)]
    public void Of_TheCredentialsScheme_AddsToTheNumber(string field, string value, int expected)
    {
        var count = 0;

        var number = HttpProxyUpstreamTestCasePartNumber.Of(Head("/1", (field, value)), ref count);

        Assert.AreEqual(expected, number);
    }

    [TestMethod]
    public void Of_NtlmMessages_AddTheirTypesAddend()
    {
        var count = 0;

        Assert.AreEqual(1001, HttpProxyUpstreamTestCasePartNumber.Of(Head("/1", ("Proxy-Authorization", "NTLM " + NtlmType1)), ref count));
        Assert.AreEqual(1002, HttpProxyUpstreamTestCasePartNumber.Of(Head("/1", ("Proxy-Authorization", "NTLM " + NtlmType3)), ref count));
        Assert.AreEqual(0, HttpProxyUpstreamTestCasePartNumber.Of(Head("/1", ("Proxy-Authorization", "NTLM " + NtlmType2)), ref count));
    }

    [TestMethod]
    public void Of_BasicOnceTheNumberIsAThousandOrMore_AddsOne()
    {
        var count = 0;

        var number = HttpProxyUpstreamTestCasePartNumber.Of(Head("/12201000", ("Proxy-Authorization", "Basic eDp5")), ref count);

        Assert.AreEqual(1001, number);
    }

    [TestMethod]
    public void Of_Negotiate_AddsTheCountOfNegotiateRequestsSoFar()
    {
        var count = 0;

        var first = HttpProxyUpstreamTestCasePartNumber.Of(Head("/1", ("Proxy-Authorization", "Negotiate abc")), ref count);
        var plain = HttpProxyUpstreamTestCasePartNumber.Of(Head("/1"), ref count);
        var second = HttpProxyUpstreamTestCasePartNumber.Of(Head("/1", ("Proxy-Authorization", "Negotiate abc")), ref count);

        Assert.AreEqual(1, first);
        Assert.AreEqual(0, plain);
        Assert.AreEqual(2, second);
    }

    private static HttpRequestHead Head(string target, params (string Name, string Value)[] fields) =>
        new("GET", target, new Version(1, 1), fields.Select(field => new HttpRequestField(field.Name, field.Value)));
}
