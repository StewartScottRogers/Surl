using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

// ADR-0043 amendment (BL-1547, BL-1548): over HTTP/2 and HTTP/3 the host is the :authority,
// and the login check is handed it as a `host` field when none was received.
public sealed partial class HttpRequestAnswererTests
{
    [TestMethod]
    public async Task AnswerAsync_AuthorityWithNoHostField_ShowsTheSessionAHostFieldAfterTheFieldsReceived()
    {
        var policy = new UnitTestAuthenticationPolicy(_ => new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], null));
        var request = Request("GET", "/file.txt") with { Authority = "127.0.0.1:18122", Fields = [Field("x-amz-date", "20260929T210307Z")] };

        await Answerer(policy).AnswerAsync(request, TestContext.CancellationToken);

        CollectionAssert.AreEqual(
            new[] { Field("x-amz-date", "20260929T210307Z"), Field("host", "127.0.0.1:18122") },
            policy.JudgedRequests.Single().Fields.ToArray());
        CollectionAssert.AreEqual(new[] { Field("x-amz-date", "20260929T210307Z") }, request.Fields.ToArray());
    }

    [TestMethod]
    [DataRow("Host")]
    [DataRow("host")]
    public async Task AnswerAsync_RequestThatCarriesAHostField_ShowsTheSessionItsFieldsUnchanged(string name)
    {
        var policy = new UnitTestAuthenticationPolicy(_ => new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], null));
        var request = Request("GET", "/file.txt") with { Authority = "other.test", Fields = [Field(name, "example.test")] };

        await Answerer(policy).AnswerAsync(request, TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { Field(name, "example.test") }, policy.JudgedRequests.Single().Fields.ToArray());
    }

    [TestMethod]
    public async Task AnswerAsync_NoAuthorityAndNoHostField_ShowsTheSessionTheFieldsUnchanged()
    {
        var policy = new UnitTestAuthenticationPolicy(_ => new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], null));
        var request = Request("GET", "/file.txt") with { Authority = null, Fields = [Field("accept", "*/*")] };

        await Answerer(policy).AnswerAsync(request, TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { Field("accept", "*/*") }, policy.JudgedRequests.Single().Fields.ToArray());
    }
}
