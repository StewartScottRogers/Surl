using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.GoogleCloudStorage.GcsJsonServer;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// The JSON API's logins and chosen faults (ADR-0148, decisions 4 and 5): Bearer tokens only, each refusal in the
/// JSON error body with the challenge the page gives, and an <c>error</c> fault rule answering with the row of its
/// status, written by hand from the Cloud Storage JSON API reference pages.
/// </summary>
[TestClass]
public sealed class GcsJsonRequestAnswererLoginTests
{
    private const string Object = "/storage/v1/b/photos/o/a.txt";
    private const string BearerChallengeField = "WWW-Authenticate: Bearer realm=\"storage\"\r\n";
    private const string InvalidTokenChallengeField = "WWW-Authenticate: Bearer realm=\"storage\", error=\"invalid_token\"\r\n";

    [TestMethod]
    public async Task ServeAsync_NoCredentialWithAnonymousOff_Is401RequiredWithTheBearerChallenge()
    {
        var harness = HarnessWithHello();

        var response = await harness.SendAsync("GET", Object);

        Assert.AreEqual(JsonError("401 Unauthorized", "Authentication required.", "required", BearerChallengeField), response);
    }

    [TestMethod]
    public async Task ServeAsync_NoCredentialUnderAllowAnonymous_ServesTheRead()
    {
        var harness = HarnessWithHello(allowsAnonymous: true);

        var response = await harness.SendAsync("GET", Object);

        Assert.AreEqual(Json(HelloObject()), response);
    }

    [TestMethod]
    public async Task ServeAsync_AnonymousDelete_Is401Required()
    {
        var harness = HarnessWithHello(allowsAnonymous: true);

        var response = await harness.SendAsync("DELETE", Object);

        Assert.AreEqual(JsonError("401 Unauthorized", "Authentication required.", "required", BearerChallengeField), response);
        Assert.AreEqual(Surl.Content.ContentEntryKind.File, harness.EntryKind("photos/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_TokenThatIsNoAccount_Is401AuthErrorWithTheInvalidTokenChallenge()
    {
        var harness = HarnessWithHello();

        var response = await harness.SendAsync("GET", Object, "Authorization: Bearer wrong-token");

        Assert.AreEqual(JsonError("401 Unauthorized", "Invalid Credentials", "authError", InvalidTokenChallengeField), response);
    }

    [TestMethod]
    [DataRow("Basic YWxpY2U6cHc=", DisplayName = "Basic")]
    [DataRow("AWS4-HMAC-SHA256 Credential=x", DisplayName = "AWS SigV4")]
    [DataRow("GOOG4-HMAC-SHA256 Credential=x/20261105/auto/storage/goog4_request", DisplayName = "GOOG4-HMAC-SHA256")]
    [DataRow("Bearer ", DisplayName = "Bearer with no token")]
    public async Task ServeAsync_AnotherScheme_Is400Invalid(string authorization)
    {
        var harness = HarnessWithHello();

        var response = await harness.SendAsync("GET", Object, $"Authorization: {authorization}");

        Assert.AreEqual(JsonError("400 Bad Request", "The provided authentication header is invalid.", "invalid"), response);
        Assert.IsEmpty(harness.Policy.Requests);
    }

    [TestMethod]
    public async Task ServeAsync_AccessTokenInTheQuery_AuthenticatesNothing()
    {
        var harness = HarnessWithHello();

        var response = await harness.SendAsync("GET", $"{Object}?access_token={UnitTestGcsAuthenticationPolicy.Token}");

        Assert.AreEqual(JsonError("401 Unauthorized", "Authentication required.", "required", BearerChallengeField), response);
    }

    [TestMethod]
    public async Task ServeAsync_NoCredentialOnAnUnlistedRequest_IsRefusedBeforeItIsNotImplemented()
    {
        var harness = HarnessWithHello();

        var response = await harness.SendAsync("PATCH", Object);

        Assert.AreEqual(JsonError("401 Unauthorized", "Authentication required.", "required", BearerChallengeField), response);
    }

    [TestMethod]
    public async Task ServeAsync_AnErrorRuleOnAJsonRequest_IsTheRowOfItsStatusAndPublishesOneEvent()
    {
        var harness = HarnessWithHello();
        var faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Scheme = "gs", Command = "storage.objects.get", Path = Object, Reply = "503" });
        harness.Faults = faults;

        var response = await harness.SendWithBearerAsync("GET", Object);

        Assert.AreEqual(JsonError("503 Service Unavailable", "Please reduce your request rate.", "backendError", domain: "usageLimits"), response);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, Object), harness.Events.Events.Single());
        Assert.AreEqual(
            new FaultPoint(FaultStage.Command) { ExchangeId = 1, ListenUrl = "gs://127.0.0.1:4443/", Scheme = "gs", Path = Object, Command = "storage.objects.get" },
            faults.Points.Single());
        Assert.Contains($"storage.objects.get {Object}: answered 503 backendError by fault rule f1.", harness.Log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_AnErrorRuleWithAStatusNoRowGives_IsBackendErrorWithThatStatus()
    {
        var harness = HarnessWithHello();
        harness.Faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Scheme = "gs", Command = "storage.objects.delete", Reply = "418 teapot" });

        var response = await harness.SendWithBearerAsync("DELETE", Object);

        Assert.AreEqual(JsonError("418 ", "Backend Error.", "backendError"), response.Replace("418 \r\n", "418 \r\n"));
        Assert.AreEqual(Surl.Content.ContentEntryKind.File, harness.EntryKind("photos/a.txt"));
    }

    [TestMethod]
    [DataRow("teapot", DisplayName = "no status")]
    [DataRow("200", DisplayName = "a success status")]
    [DataRow("600", DisplayName = "a status past 599")]
    public async Task ServeAsync_AnErrorRuleThatDoesNotFit_IsServedAndNoted(string reply)
    {
        var harness = HarnessWithHello();
        harness.Faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Scheme = "gs", Reply = reply });

        var response = await harness.SendWithBearerAsync("GET", Object);

        Assert.AreEqual(Json(HelloObject()), response);
        Assert.IsEmpty(harness.Events.Events);
        Assert.Contains($"storage.objects.get {Object}: fault rule f1's reply does not fit the gs JSON error shape; answered as without it.", harness.Log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_ARefusedLoginOrAnUnlistedRequest_AsksNoFaultPoint()
    {
        var harness = HarnessWithHello();
        var faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Scheme = "gs", Reply = "503" });
        harness.Faults = faults;

        var refused = await harness.SendAsync("GET", Object);
        var unlisted = await harness.SendWithBearerAsync("POST", Object);

        StringAssert.StartsWith(refused, "HTTP/1.1 401 Unauthorized\r\n");
        Assert.AreEqual(NotImplemented, unlisted);
        Assert.IsEmpty(faults.Points);
    }
}
