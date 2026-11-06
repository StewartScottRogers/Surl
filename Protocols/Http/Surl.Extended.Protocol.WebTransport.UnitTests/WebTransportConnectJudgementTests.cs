namespace Surl.Extended.Protocol.WebTransport;

/// <summary>ADR-0117 decision 1's origin and path rules (WTH3 3.2), judged on heads written by hand.</summary>
[TestClass]
public sealed class WebTransportConnectJudgementTests
{
    [TestMethod]
    [DataRow("/echo", "/echo", true)]
    [DataRow("/echo/a", "/echo", true)]
    [DataRow("/echo?x=1", "/echo", true)]
    [DataRow("/echoes", "/echo", false)]
    [DataRow("/other", "/echo", false)]
    [DataRow("/echo/a", "/echo/", true)]
    [DataRow("/anything", "", true)]
    [DataRow("/anything", "/", true)]
    public void IsAcceptedPath_PathAgainstTheListenPath_IsAcceptedAtOrBelowItAtASlash(string target, string acceptedPath, bool expected)
    {
        var accepted = WebTransportConnectJudgement.IsAcceptedPath(target, acceptedPath);

        Assert.AreEqual(expected, accepted);
    }

    [TestMethod]
    [DataRow("https://127.0.0.1:18050", "127.0.0.1:18050", true)]
    [DataRow("HTTPS://Example.COM", "example.com", true)]
    [DataRow("https://example.com:443", "example.com", true)]
    [DataRow("https://example.com", "example.com:443", true)]
    [DataRow("http://example.com", "example.com", false)]
    [DataRow("https://other.example", "example.com", false)]
    [DataRow("null", "example.com", false)]
    public void IsOwnOrigin_OneOrigin_IsAcceptedOnlyWhenItIsHttpsAndTheAuthority(string origin, string authority, bool expected)
    {
        var head = Head(authority, ("origin", origin));

        var own = WebTransportConnectJudgement.IsOwnOrigin(head);

        Assert.AreEqual(expected, own);
    }

    [TestMethod]
    public void IsOwnOrigin_NoOrigin_IsAccepted()
    {
        var own = WebTransportConnectJudgement.IsOwnOrigin(Head("example.com"));

        Assert.IsTrue(own, "WTH3 3.2: Origin is optional for clients that are not browsers.");
    }

    [TestMethod]
    public void IsOwnOrigin_TwoOrigins_IsRefused()
    {
        var own = WebTransportConnectJudgement.IsOwnOrigin(Head("example.com", ("origin", "https://example.com"), ("origin", "https://example.com")));

        Assert.IsFalse(own);
    }

    [TestMethod]
    public void IsOwnOrigin_NoAuthority_IsComparedWithAnEmptyOne()
    {
        var own = WebTransportConnectJudgement.IsOwnOrigin(Head(null, ("origin", "https://")));

        Assert.IsTrue(own);
    }

    [TestMethod]
    [DataRow("https://other.example", "/echo", 403)]
    [DataRow("https://example.com", "/other", 405)]
    [DataRow("https://example.com", "/echo", 200)]
    public void Judge_OriginThenPath_AnswersTheFirstFailure(string origin, string target, int expected)
    {
        var head = Head("example.com", ("origin", origin)) with { Target = target };

        var status = WebTransportConnectJudgement.Judge(head, "/echo");

        Assert.AreEqual(expected, status);
    }

    private static Http3RequestHead Head(string? authority, params (string Name, string Value)[] fields) =>
        new("CONNECT", "/echo", authority, [.. fields.Select(field => new KeyValuePair<string, string>(field.Name, field.Value))], null, false) { Protocol = "webtransport-h3" };
}
