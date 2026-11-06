using System.Net;
using System.Security.Cryptography;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// A request naming its <see cref="HttpAuthenticationRequest.AzureStorage"/>, as the Azure Storage
/// server's do (ADR-0109 decision 3): judged by a bearer token, Shared Key, Shared Key Lite or a
/// shared access signature, whatever <c>--auth</c> says, each refusal
/// <see cref="HttpAuthenticationOutcome.Forbidden"/> with its <see cref="HttpAuthenticationRefusal"/>.
/// Each signature is made here over the string to sign written as the pages give it - "Authorize
/// with Shared Key", "Create a service SAS" and "Create an account SAS" - never from the server's
/// own builder, and never from the Curl port.
/// </summary>
[TestClass]
public sealed class AzureStorageVerifierTests
{
    private const string AccountName = "myaccount";

    private const string Date = "Thu, 05 Nov 2026 12:00:00 GMT";

    private static readonly byte[] Key = Encoding.ASCII.GetBytes("0123456789abcdef0123456789abcdef");

    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    private static readonly AccountBook Accounts = new(
    [
        new Account(AccountName, Convert.ToBase64String(Key)),
        new Account("plainaccount", "not base64!"),
        new Account(string.Empty, "tok"),
    ]);

    private readonly ManualTimeProvider clock = new();

    private static string Sign(string stringToSign) =>
        Convert.ToBase64String(HMACSHA256.HashData(Key, Encoding.UTF8.GetBytes(stringToSign)));

    private static AzureStorageScope Scope(string resourcePath, string permissions = "r", string? accountName = AccountName, IPAddress? peer = null) =>
        new(accountName, resourcePath, permissions, peer ?? IPAddress.Loopback);

    private static HttpAuthenticationRequest Request(string method, string target, AzureStorageScope scope, params string[] fields) =>
        new(
            method,
            target,
            method is not ("GET" or "HEAD"),
            [new("Host", $"{AccountName}.blob.core.windows.net"), .. fields.Select(field => new KeyValuePair<string, string>(field[..field.IndexOf(':')], field[(field.IndexOf(':') + 1)..].TrimStart(' ')))],
            AzureStorage: scope);

    // Only Basic accepted, so --auth plays no part in Azure's check.
    private AuthenticationPolicy CreatePolicy(bool allowAnonymous = false, TimeSpan clockOffset = default)
    {
        clock.Advance(Now + clockOffset - clock.GetUtcNow());

        return PolicyFixture.Create(
            Accounts,
            clock,
            allowAnonymous,
            acceptedMethods: new HashSet<AuthenticationMethod> { AuthenticationMethod.Basic },
            httpMethods: [new BasicAuthenticationMethod(Accounts)]);
    }

    // Judges the request, letting the refusal delay pass when the judgement waits on it.
    private async Task<HttpAuthenticationVerdict> JudgeAsync(HttpAuthenticationRequest request, AuthenticationPolicy? policy = null, TlsSession? tls = null)
    {
        var judgement = (policy ?? CreatePolicy()).StartHttpConnection(tls).JudgeAsync(request, CancellationToken.None).AsTask();
        if (!judgement.IsCompleted)
        {
            clock.Advance(AuthenticationPolicy.RefusalDelay);
        }

        return await judgement;
    }

    private static void AssertForbidden(HttpAuthenticationVerdict verdict, HttpAuthenticationRefusal refusal)
    {
        Assert.AreEqual(HttpAuthenticationOutcome.Forbidden, verdict.Outcome);
        Assert.AreEqual(refusal, verdict.Refusal);
        Assert.IsNull(verdict.AccountName);
        Assert.IsEmpty(verdict.WwwAuthenticateValues);
    }

    private static void AssertAccepted(HttpAuthenticationVerdict verdict, string expectedNote, string accountName = AccountName)
    {
        Assert.AreEqual(HttpAuthenticationOutcome.Proceed, verdict.Outcome);
        Assert.AreEqual(accountName, verdict.AccountName);
        Assert.AreEqual(expectedNote, verdict.CheckedLogin?.Note);
    }

    // ---- Shared Key -------------------------------------------------------------------------

    // The page's Get Container Metadata example: GET ...?restype=container&comp=metadata&timeout=20.
    private static HttpAuthenticationRequest BlobSharedKeyRequest(string signature, string accountName = AccountName, string date = Date, string dateField = "x-ms-date") =>
        Request(
            "GET",
            "/mycontainer?restype=container&comp=metadata&timeout=20",
            Scope("/mycontainer"),
            $"{dateField}: {date}",
            "x-ms-version: 2009-09-19",
            $"Authorization: SharedKey {accountName}:{signature}");

    private const string BlobStringToSign =
        "GET\n\n\n\n\n\n\n\n\n\n\n\nx-ms-date:" + Date + "\nx-ms-version:2009-09-19\n/myaccount/mycontainer\ncomp:metadata\nrestype:container\ntimeout:20";

    [TestMethod]
    public async Task SharedKey_BlobRequestSignedOverThePagesString_IsAcceptedAsTheAccount()
    {
        var verdict = await JudgeAsync(BlobSharedKeyRequest(Sign(BlobStringToSign)));

        AssertAccepted(verdict, "Login accepted: SharedKey myaccount");
    }

    [TestMethod]
    public async Task SharedKey_DataLakeAppendWithEveryStandardField_IsAccepted()
    {
        const string stringToSign =
            "PATCH\ngzip\nen\n5\nXUFAKrxLKna5cZ2REBfFkg==\ntext/plain\n\nThu, 05 Nov 2026 11:00:00 GMT\n\"0x1\"\n*\nThu, 05 Nov 2026 11:30:00 GMT\nbytes=0-4\n"
            + "x-ms-client-request-id:a,b\nx-ms-date:" + Date + "\nx-ms-meta-note:one two\nx-ms-version:2025-01-05\n"
            + "/myaccount/myfs/dir/file%20one.txt\naction:append\nposition:0";
        var request = Request(
            "PATCH",
            "/myfs/dir/file%20one.txt?position=0&action=append",
            Scope("/myfs/dir/file one.txt", "aw"),
            "Content-Encoding: gzip",
            "Content-Language: en",
            "Content-Length: 5",
            "Content-MD5: XUFAKrxLKna5cZ2REBfFkg==",
            "Content-Type: text/plain",
            "If-Modified-Since: Thu, 05 Nov 2026 11:00:00 GMT",
            "If-Match: \"0x1\"",
            "If-None-Match: *",
            "If-Unmodified-Since: Thu, 05 Nov 2026 11:30:00 GMT",
            "Range: bytes=0-4",
            "X-MS-Meta-Note:   one    two  ",
            "x-ms-client-request-id: a",
            "x-ms-client-request-id: b",
            $"x-ms-date: {Date}",
            "x-ms-version: 2025-01-05",
            $"Authorization: SharedKey {AccountName}:{Sign(stringToSign)}");

        var verdict = await JudgeAsync(request);

        AssertAccepted(verdict, "Login accepted: SharedKey myaccount");
    }

    [TestMethod]
    public async Task SharedKey_ZeroContentLength_IsSignedEmpty()
    {
        const string stringToSign =
            "PUT\n\n\n\n\n\n\n\n\n\n\n\nx-ms-date:" + Date + "\nx-ms-version:2025-01-05\n/myaccount/myfs/new.txt\nresource:file";
        var request = Request(
            "PUT",
            "/myfs/new.txt?resource=file",
            Scope("/myfs/new.txt", "w"),
            "Content-Length: 0",
            $"x-ms-date: {Date}",
            "x-ms-version: 2025-01-05",
            $"Authorization: SharedKey {AccountName}:{Sign(stringToSign)}");

        var verdict = await JudgeAsync(request);

        AssertAccepted(verdict, "Login accepted: SharedKey myaccount");
    }

    [TestMethod]
    public async Task SharedKey_QueryParameterRepeated_SignsItsValuesSortedWithCommas()
    {
        const string stringToSign =
            "GET\n\n\n\n\n\n\n\n\n\n\n\nx-ms-date:" + Date + "\nx-ms-version:2025-01-05\n/myaccount/mycontainer\ncomp:list\ninclude:metadata,snapshots,uncommittedblobs\nprefix:a b\nrestype:container\nx:";
        var request = Request(
            "GET",
            "/mycontainer?restype=container&comp=list&include=snapshots&include=metadata&&include=uncommittedblobs&prefix=a+b&x",
            Scope("/mycontainer", "l"),
            $"x-ms-date: {Date}",
            "x-ms-version: 2025-01-05",
            $"Authorization: SharedKey {AccountName}:{Sign(stringToSign)}");

        var verdict = await JudgeAsync(request);

        AssertAccepted(verdict, "Login accepted: SharedKey myaccount");
    }

    [TestMethod]
    public async Task SharedKeyLite_SignedOverThePagesShorterString_IsAccepted()
    {
        const string stringToSign = "GET\n\ntext/plain\n\nx-ms-date:" + Date + "\nx-ms-version:2025-01-05\n/myaccount/mycontainer?comp=list";
        var request = Request(
            "GET",
            "/mycontainer?restype=container&comp=list",
            Scope("/mycontainer", "l"),
            "Content-Type: text/plain",
            $"x-ms-date: {Date}",
            "x-ms-version: 2025-01-05",
            $"Authorization: SharedKeyLite {AccountName}:{Sign(stringToSign)}");

        var verdict = await JudgeAsync(request);

        AssertAccepted(verdict, "Login accepted: SharedKeyLite myaccount");
    }

    [TestMethod]
    public async Task SharedKeyLite_WithoutComp_SignsThePathAlone()
    {
        const string stringToSign = "GET\n\n\n\nx-ms-date:" + Date + "\nx-ms-version:2025-01-05\n/myaccount/mycontainer/a.txt";
        var request = Request(
            "GET",
            "/mycontainer/a.txt",
            Scope("/mycontainer/a.txt"),
            $"x-ms-date: {Date}",
            "x-ms-version: 2025-01-05",
            $"Authorization: SharedKeyLite {AccountName}:{Sign(stringToSign)}");

        var verdict = await JudgeAsync(request);

        AssertAccepted(verdict, "Login accepted: SharedKeyLite myaccount");
    }

    [TestMethod]
    public async Task SharedKey_DateFieldWithoutXMsDate_IsTheSignedDate()
    {
        const string stringToSign = "GET\n\n\n\n\n\n" + Date + "\n\n\n\n\n\nx-ms-version:2009-09-19\n/myaccount/mycontainer\ncomp:metadata\nrestype:container\ntimeout:20";

        var verdict = await JudgeAsync(BlobSharedKeyRequest(Sign(stringToSign), dateField: "Date"));

        AssertAccepted(verdict, "Login accepted: SharedKey myaccount");
    }

    [TestMethod]
    public async Task SharedKey_WrongSignature_IsForbiddenAsBadSignatureWithTheNote()
    {
        var verdict = await JudgeAsync(BlobSharedKeyRequest(Sign(BlobStringToSign + "x")));

        AssertForbidden(verdict, HttpAuthenticationRefusal.BadSignature);
        Assert.AreEqual("Login refused: SharedKey myaccount", verdict.CheckedLogin?.Note);
    }

    [TestMethod]
    [DataRow("nobody", DisplayName = "no such account")]
    [DataRow("plainaccount", DisplayName = "an account whose password is not base64")]
    public async Task SharedKey_UnknownAccount_IsForbiddenAsUnknownAccount(string accountName)
    {
        var verdict = await JudgeAsync(BlobSharedKeyRequest(Sign(BlobStringToSign.Replace("/myaccount/", $"/{accountName}/", StringComparison.Ordinal)), accountName));

        AssertForbidden(verdict, HttpAuthenticationRefusal.UnknownAccount);
    }

    [TestMethod]
    [DataRow(-16)]
    [DataRow(16)]
    public async Task SharedKey_DateMoreThan15MinutesOff_IsForbiddenAsClockSkew(int minutes)
    {
        var verdict = await JudgeAsync(BlobSharedKeyRequest(Sign(BlobStringToSign)), CreatePolicy(clockOffset: TimeSpan.FromMinutes(minutes)));

        AssertForbidden(verdict, HttpAuthenticationRefusal.ClockSkew);
    }

    [TestMethod]
    public async Task SharedKey_DateWithin15Minutes_IsAccepted()
    {
        var verdict = await JudgeAsync(BlobSharedKeyRequest(Sign(BlobStringToSign)), CreatePolicy(clockOffset: TimeSpan.FromMinutes(15)));

        Assert.AreEqual(HttpAuthenticationOutcome.Proceed, verdict.Outcome);
    }

    [TestMethod]
    [DataRow("X-Other", Date, DisplayName = "no date field")]
    [DataRow("x-ms-date", "yesterday", DisplayName = "a date that does not read")]
    public async Task SharedKey_NoReadableDate_IsForbiddenAsMissingRequestDate(string dateField, string date)
    {
        var verdict = await JudgeAsync(BlobSharedKeyRequest(Sign(BlobStringToSign), date: date, dateField: dateField));

        AssertForbidden(verdict, HttpAuthenticationRefusal.MissingRequestDate);
    }

    [TestMethod]
    [DataRow("SharedKey myaccount", DisplayName = "no colon")]
    [DataRow("SharedKey :c2ln", DisplayName = "no account")]
    [DataRow("SharedKey myaccount:", DisplayName = "no signature")]
    [DataRow("SharedKey myaccount:c2ln extra", DisplayName = "a space inside")]
    [DataRow("SharedKey", DisplayName = "nothing after the scheme")]
    public async Task SharedKey_HeaderThatDoesNotParse_IsForbiddenAsMalformedWithNoNote(string authorization)
    {
        var request = Request("GET", "/mycontainer/a.txt", Scope("/mycontainer/a.txt"), $"x-ms-date: {Date}", $"Authorization: {authorization}");

        var verdict = await JudgeAsync(request);

        AssertForbidden(verdict, HttpAuthenticationRefusal.MalformedCredentials);
        Assert.IsNull(verdict.CheckedLogin);
    }

    [TestMethod]
    [DataRow("Basic YWxpY2U6c2VjcmV0", DisplayName = "Basic")]
    [DataRow("AWS4-HMAC-SHA256 Credential=x", DisplayName = "AWS Signature Version 4")]
    [DataRow("Bearer", DisplayName = "a bearer with no token")]
    [DataRow("sharedkey myaccount:c2ln", DisplayName = "Shared Key in lower case")]
    public async Task OtherScheme_IsForbiddenAsUnsupportedScheme(string authorization)
    {
        var verdict = await JudgeAsync(Request("GET", "/c/a.txt", Scope("/c/a.txt"), $"Authorization: {authorization}"));

        AssertForbidden(verdict, HttpAuthenticationRefusal.UnsupportedScheme);
    }

    [TestMethod]
    [DataRow("az", HttpAuthenticationOutcome.Proceed, DisplayName = "a scheme the account may log in on")]
    [DataRow("s3", HttpAuthenticationOutcome.Forbidden, DisplayName = "a scheme the account may not log in on")]
    public async Task SharedKey_AccountRestrictedToSchemes_SignsOnlyOnThoseSchemes(string scheme, HttpAuthenticationOutcome expected)
    {
        var accounts = new AccountBook(
        [
            new Account(AccountName, Convert.ToBase64String(Key)) { Schemes = new HashSet<string> { "az" } },
            new Account("other", Convert.ToBase64String(Key)),
        ]).ForScheme(scheme);
        clock.Advance(Now - clock.GetUtcNow());
        var policy = PolicyFixture.Create(accounts, clock);

        var verdict = await JudgeAsync(BlobSharedKeyRequest(Sign(BlobStringToSign)), policy);

        Assert.AreEqual(expected, verdict.Outcome);
    }

    [TestMethod]
    public async Task SharedKey_UnderAllowAnonymous_IsStillChecked()
    {
        var verdict = await JudgeAsync(BlobSharedKeyRequest(Sign("wrong")), CreatePolicy(allowAnonymous: true));

        AssertForbidden(verdict, HttpAuthenticationRefusal.BadSignature);
    }

    // ---- refuse-login (ADR-0124, decision 4) -------------------------------------------------

    private AuthenticationPolicy CreateRefusingPolicy(string account, RecordingSurlEventPublisher events)
    {
        clock.Advance(Now - clock.GetUtcNow());

        return PolicyFixture.CreateObserved(
            Accounts,
            clock,
            events,
            new ScriptedFaultPlan(new FaultRule("f1", FaultKind.RefuseLogin) { Account = account }),
            new HashSet<AuthenticationMethod> { AuthenticationMethod.Basic },
            [new BasicAuthenticationMethod(Accounts)]);
    }

    [TestMethod]
    public async Task SharedKey_RefuseLoginFault_IsForbiddenAsBadSignatureWithTheRefusedNote()
    {
        var events = new RecordingSurlEventPublisher();

        var verdict = await JudgeAsync(BlobSharedKeyRequest(Sign(BlobStringToSign)), CreateRefusingPolicy(AccountName, events));

        AssertForbidden(verdict, HttpAuthenticationRefusal.BadSignature);
        Assert.AreEqual("Login refused: SharedKey myaccount", verdict.CheckedLogin?.Note);
        Assert.AreEqual(new LoginJudgedEvent(null, "http", AccountName, "SharedKey", false, true), events.Events[^1]);
    }

    [TestMethod]
    public async Task Bearer_RefuseLoginFault_IsForbiddenAsUnknownAccount()
    {
        var events = new RecordingSurlEventPublisher();

        var verdict = await JudgeAsync(
            Request("GET", "/c/a.txt", Scope("/c/a.txt"), "Authorization: Bearer tok"), CreateRefusingPolicy(string.Empty, events));

        AssertForbidden(verdict, HttpAuthenticationRefusal.UnknownAccount);
        Assert.AreEqual("Login refused: Bearer bearer token", verdict.CheckedLogin?.Note);
        Assert.HasCount(2, events.Events);
    }

    // ---- Bearer and no credentials ----------------------------------------------------------

    [TestMethod]
    public async Task Bearer_TokenOfTheEmptyNameAccount_IsAcceptedAsTheEmptyName()
    {
        var verdict = await JudgeAsync(Request("GET", "/c/a.txt", Scope("/c/a.txt"), "Authorization: bearer tok"));

        AssertAccepted(verdict, "Login accepted: Bearer bearer token", string.Empty);
    }

    [TestMethod]
    public async Task Bearer_WrongToken_IsForbiddenAsUnknownAccount()
    {
        var verdict = await JudgeAsync(Request("GET", "/c/a.txt", Scope("/c/a.txt"), "Authorization: Bearer wrong"));

        AssertForbidden(verdict, HttpAuthenticationRefusal.UnknownAccount);
        Assert.AreEqual("Login refused: Bearer bearer token", verdict.CheckedLogin?.Note);
    }

    [TestMethod]
    public async Task NoCredentials_AnonymousOff_IsForbiddenAsMissingCredentialsUndelayed()
    {
        var judgement = CreatePolicy().StartHttpConnection(null).JudgeAsync(Request("GET", "/c/a.txt", Scope("/c/a.txt")), CancellationToken.None);

        Assert.IsTrue(judgement.IsCompleted);
        AssertForbidden(await judgement, HttpAuthenticationRefusal.MissingCredentials);
    }

    [TestMethod]
    public async Task NoCredentials_UnderAllowAnonymous_ProceedsWithNoAccount()
    {
        var verdict = await JudgeAsync(Request("PUT", "/c/a.txt", Scope("/c/a.txt", "w")), CreatePolicy(allowAnonymous: true));

        Assert.AreEqual(HttpAuthenticationOutcome.Proceed, verdict.Outcome);
        Assert.IsNull(verdict.AccountName);
        Assert.IsNull(verdict.CheckedLogin);
    }

    [TestMethod]
    public async Task Bearer_WrongTokenUnderAllowAnonymous_IsForbiddenAsUnknownAccount()
    {
        var verdict = await JudgeAsync(Request("GET", "/c/a.txt", Scope("/c/a.txt"), "Authorization: Bearer wrong"), CreatePolicy(allowAnonymous: true));

        AssertForbidden(verdict, HttpAuthenticationRefusal.UnknownAccount);
    }

    [TestMethod]
    public async Task Bearer_GoodTokenUnderAllowAnonymous_IsAcceptedAsTheEmptyName()
    {
        var verdict = await JudgeAsync(Request("GET", "/c/a.txt", Scope("/c/a.txt"), "Authorization: Bearer tok"), CreatePolicy(allowAnonymous: true));

        AssertAccepted(verdict, "Login accepted: Bearer bearer token", string.Empty);
    }

    // ---- The path-style account segment (ADR-0109 decision 2) ------------------------------

    [TestMethod]
    [DataRow(AccountName, true, DisplayName = "a named account")]
    [DataRow("plainaccount", true, DisplayName = "a named account whose password is not base64")]
    [DataRow("MyAccount", false, DisplayName = "a named account in another case")]
    [DataRow("nosuchaccount", false, DisplayName = "no account")]
    [DataRow("", false, DisplayName = "the empty name, the bearer token's account")]
    public void NamesAccount_Name_IsTrueOnlyForANamedAccount(string name, bool expected)
    {
        var policy = CreatePolicy();

        Assert.AreEqual(expected, policy.NamesAccount(name));
    }

    [TestMethod]
    public void NamesAccount_Null_Throws()
    {
        var policy = CreatePolicy();

        Assert.ThrowsExactly<ArgumentNullException>(() => policy.NamesAccount(null!));
    }

    // ---- Service shared access signatures ---------------------------------------------------

    private static string ServiceStringToSign(string permissions, string start, string expiry, string resource, string ip, string protocol, string signedResource, string snapshot = "") =>
        $"{permissions}\n{start}\n{expiry}\n{resource}\n\n{ip}\n{protocol}\n2025-01-05\n{signedResource}\n{snapshot}\n\n\n\n\n\n";

    private static string ServiceQuery(string permissions, string start, string expiry, string ip, string protocol, string signedResource, string stringToSign, string extra = "")
    {
        var fields = new List<string> { "sv=2025-01-05", $"sr={signedResource}", $"sp={permissions}", $"se={Uri.EscapeDataString(expiry)}" };
        fields.AddRange(start.Length > 0 ? [$"st={Uri.EscapeDataString(start)}"] : []);
        fields.AddRange(ip.Length > 0 ? [$"sip={ip}"] : []);
        fields.AddRange(protocol.Length > 0 ? [$"spr={Uri.EscapeDataString(protocol)}"] : []);

        return string.Join('&', fields) + extra + $"&sig={Uri.EscapeDataString(Sign(stringToSign))}";
    }

    private static HttpAuthenticationRequest BlobSas(
        string permissions = "r", string start = "", string expiry = "2026-11-06T00:00:00Z", string ip = "", string protocol = "", string grants = "r", IPAddress? peer = null, string extra = "")
    {
        var stringToSign = ServiceStringToSign(permissions, start, expiry, "/blob/myaccount/mycontainer/photos/a b.txt", ip, protocol, "b");

        return Request(
            "GET",
            $"/mycontainer/photos/a%20b.txt?{ServiceQuery(permissions, start, expiry, ip, protocol, "b", stringToSign, extra)}",
            Scope("/mycontainer/photos/a b.txt", grants, peer: peer));
    }

    [TestMethod]
    public async Task ServiceSas_BlobSignedOverThePagesString_IsAcceptedAsTheAccount()
    {
        var verdict = await JudgeAsync(BlobSas(start: "2026-11-05T11:00:00Z", ip: "127.0.0.0-127.255.255.255", protocol: "https,http"));

        AssertAccepted(verdict, "Login accepted: SAS myaccount");
    }

    [TestMethod]
    public async Task ServiceSas_ContainerSignatureForAListing_IsAccepted()
    {
        var stringToSign = ServiceStringToSign("rl", string.Empty, "2026-11-06", "/blob/myaccount/mycontainer", string.Empty, string.Empty, "c");
        var request = Request(
            "GET",
            $"/mycontainer?restype=container&comp=list&{ServiceQuery("rl", string.Empty, "2026-11-06", string.Empty, string.Empty, "c", stringToSign)}",
            Scope("/mycontainer", "l"));

        var verdict = await JudgeAsync(request);

        AssertAccepted(verdict, "Login accepted: SAS myaccount");
    }

    [TestMethod]
    public async Task ServiceSas_DirectorySignatureForAPathBelowIt_IsAccepted()
    {
        var stringToSign = ServiceStringToSign("w", string.Empty, "2026-11-06T00:00Z", "/blob/myaccount/myfs/dir", string.Empty, string.Empty, "d");
        var request = Request(
            "PUT",
            $"/myfs/dir/sub/file.txt?resource=file&sdd=1&{ServiceQuery("w", string.Empty, "2026-11-06T00:00Z", string.Empty, string.Empty, "d", stringToSign)}",
            Scope("/myfs/dir/sub/file.txt", "w"));

        var verdict = await JudgeAsync(request);

        AssertAccepted(verdict, "Login accepted: SAS myaccount");
    }

    [TestMethod]
    public async Task ServiceSas_SnapshotInTheQuery_IsSigned()
    {
        var stringToSign = ServiceStringToSign("r", string.Empty, "2026-11-06T00:00:00.5Z", "/blob/myaccount/c/a.txt", string.Empty, string.Empty, "b", "2026-11-01T00:00:00Z");
        var request = Request(
            "GET",
            $"/c/a.txt?snapshot=2026-11-01T00%3A00%3A00Z&{ServiceQuery("r", string.Empty, "2026-11-06T00:00:00.5Z", string.Empty, string.Empty, "b", stringToSign)}",
            Scope("/c/a.txt"));

        var verdict = await JudgeAsync(request);

        AssertAccepted(verdict, "Login accepted: SAS myaccount");
    }

    [TestMethod]
    public async Task ServiceSas_SignedOverAnotherBlob_IsForbiddenAsBadSignature()
    {
        var stringToSign = ServiceStringToSign("r", string.Empty, "2026-11-06", "/blob/myaccount/c/other.txt", string.Empty, string.Empty, "b");
        var request = Request("GET", $"/c/a.txt?{ServiceQuery("r", string.Empty, "2026-11-06", string.Empty, string.Empty, "b", stringToSign)}", Scope("/c/a.txt"));

        var verdict = await JudgeAsync(request);

        AssertForbidden(verdict, HttpAuthenticationRefusal.BadSignature);
        Assert.AreEqual("Login refused: SAS myaccount", verdict.CheckedLogin?.Note);
    }

    [TestMethod]
    public async Task ServiceSas_SignedEarlierThanTheWindow_IsForbiddenAsExpired()
    {
        var verdict = await JudgeAsync(BlobSas(expiry: "2026-11-05T11:44:59Z"));

        AssertForbidden(verdict, HttpAuthenticationRefusal.SignatureExpired);
    }

    [TestMethod]
    public async Task ServiceSas_StartWithinTheWindow_IsAccepted()
    {
        var verdict = await JudgeAsync(BlobSas(start: "2026-11-05T12:15:00Z"));

        Assert.AreEqual(HttpAuthenticationOutcome.Proceed, verdict.Outcome);
    }

    [TestMethod]
    public async Task ServiceSas_StartBeyondTheWindow_IsForbiddenAsNotYetValid()
    {
        var verdict = await JudgeAsync(BlobSas(start: "2026-11-05T12:16:00Z"));

        AssertForbidden(verdict, HttpAuthenticationRefusal.SignatureNotYetValid);
    }

    [TestMethod]
    [DataRow("10.0.0.1", "127.0.0.1", DisplayName = "another address")]
    [DataRow("10.0.0.1-10.0.0.9", "10.0.0.10", DisplayName = "past the range")]
    [DataRow("10.0.0.5-10.0.0.9", "10.0.0.1", DisplayName = "before the range")]
    [DataRow("::1", "127.0.0.1", DisplayName = "another family")]
    [DataRow("not-an-address", "127.0.0.1", DisplayName = "a range that does not read")]
    [DataRow("10.0.0.1-nothing", "10.0.0.1", DisplayName = "a high end that does not read")]
    public async Task ServiceSas_AddressOutsideTheSignedRange_IsForbiddenAsAddressNotAllowed(string signedAddresses, string peer)
    {
        var verdict = await JudgeAsync(BlobSas(ip: signedAddresses, peer: IPAddress.Parse(peer)));

        AssertForbidden(verdict, HttpAuthenticationRefusal.AddressNotAllowed);
    }

    [TestMethod]
    [DataRow("10.0.0.7", "10.0.0.7")]
    [DataRow("10.0.0.1-10.0.0.9", "::ffff:10.0.0.9")]
    [DataRow("2001:db8::1-2001:db8::ff", "2001:db8::10")]
    public async Task ServiceSas_AddressInsideTheSignedRange_IsAccepted(string signedAddresses, string peer)
    {
        var verdict = await JudgeAsync(BlobSas(ip: signedAddresses, peer: IPAddress.Parse(peer)));

        Assert.AreEqual(HttpAuthenticationOutcome.Proceed, verdict.Outcome);
    }

    [TestMethod]
    [DataRow("https", DisplayName = "HTTPS only")]
    [DataRow("ftp", DisplayName = "a protocol that is none")]
    public async Task ServiceSas_ProtocolNotAdmittingPlainText_IsForbiddenAsProtocolNotAllowed(string protocol)
    {
        var verdict = await JudgeAsync(BlobSas(protocol: protocol));

        AssertForbidden(verdict, HttpAuthenticationRefusal.ProtocolNotAllowed);
    }

    [TestMethod]
    [DataRow("https")]
    [DataRow("http,https")]
    public async Task ServiceSas_HttpsOverTls_IsAccepted(string protocol)
    {
        var verdict = await JudgeAsync(BlobSas(protocol: protocol), tls: PolicyFixture.Tls);

        Assert.AreEqual(HttpAuthenticationOutcome.Proceed, verdict.Outcome);
    }

    [TestMethod]
    public async Task ServiceSas_PermissionNotGranted_IsForbiddenAsPermissionNotGranted()
    {
        var verdict = await JudgeAsync(BlobSas(permissions: "rl", grants: "w"));

        AssertForbidden(verdict, HttpAuthenticationRefusal.PermissionNotGranted);
    }

    [TestMethod]
    [DataRow("&skoid=6e2a9c3b-0000-0000-0000-000000000000", DisplayName = "a user delegation SAS")]
    [DataRow("&si=policy1", DisplayName = "a stored access policy")]
    public async Task Sas_OfAKindNotServed_IsForbiddenAsUnservedSignatureKind(string extra)
    {
        var verdict = await JudgeAsync(BlobSas(extra: extra));

        AssertForbidden(verdict, HttpAuthenticationRefusal.UnservedSignatureKind);
    }

    [TestMethod]
    [DataRow("sv=2019-12-12&sr=b&sp=r&se=2026-11-06", HttpAuthenticationRefusal.UnservedSignatureKind, DisplayName = "a version before 2020-12-06")]
    [DataRow("sv=2025-01-05&sr=bs&sp=r&se=2026-11-06", HttpAuthenticationRefusal.UnservedSignatureKind, DisplayName = "a snapshot resource")]
    [DataRow("sr=b&sp=r&se=2026-11-06", HttpAuthenticationRefusal.UnreadableSignatureFields, DisplayName = "no version")]
    [DataRow("sv=latest&sr=b&sp=r&se=2026-11-06", HttpAuthenticationRefusal.UnreadableSignatureFields, DisplayName = "a version that is no date")]
    [DataRow("sv=2025-01-05&sr=b&se=2026-11-06", HttpAuthenticationRefusal.UnreadableSignatureFields, DisplayName = "no permissions")]
    [DataRow("sv=2025-01-05&sr=b&sp=r", HttpAuthenticationRefusal.UnreadableSignatureFields, DisplayName = "no expiry")]
    [DataRow("sv=2025-01-05&sr=b&sp=r&se=2026-11-06&st=noon", HttpAuthenticationRefusal.UnreadableSignatureFields, DisplayName = "a start that is no time")]
    [DataRow("sv=2025-01-05&sp=r&se=2026-11-06", HttpAuthenticationRefusal.UnservedSignatureKind, DisplayName = "no signed resource")]
    public async Task Sas_FieldsNotServed_IsForbiddenWithTheirReason(string fields, HttpAuthenticationRefusal refusal)
    {
        var verdict = await JudgeAsync(Request("GET", $"/c/a.txt?{fields}&sig=c2ln", Scope("/c/a.txt")));

        AssertForbidden(verdict, refusal);
    }

    [TestMethod]
    [DataRow("b", "/c", DisplayName = "a blob signature for a container")]
    [DataRow("c", "/", DisplayName = "a container signature for the account")]
    [DataRow("d", "/c/dir", DisplayName = "a directory deeper than the path")]
    [DataRow("d", "/c", DisplayName = "a directory signature for a container")]
    public async Task ServiceSas_SignedResourceTheRequestDoesNotAddress_IsForbiddenAsUnservedSignatureKind(string signedResource, string resourcePath)
    {
        var verdict = await JudgeAsync(Request("GET", $"{resourcePath}?sv=2025-01-05&sr={signedResource}&sdd=2&sp=r&se=2026-11-06&sig=c2ln", Scope(resourcePath)));

        AssertForbidden(verdict, HttpAuthenticationRefusal.UnservedSignatureKind);
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("one")]
    public async Task ServiceSas_DirectoryDepthNotServed_IsForbiddenAsUnservedSignatureKind(string depth)
    {
        var verdict = await JudgeAsync(Request("GET", $"/c/dir/a.txt?sv=2025-01-05&sr=d&sdd={depth}&sp=r&se=2026-11-06&sig=c2ln", Scope("/c/dir/a.txt")));

        AssertForbidden(verdict, HttpAuthenticationRefusal.UnservedSignatureKind);
    }

    [TestMethod]
    [DataRow("nobody", DisplayName = "an account that is none")]
    [DataRow(null, DisplayName = "no account named")]
    public async Task ServiceSas_AccountThatIsNone_IsForbiddenAsUnknownAccount(string? accountName)
    {
        var stringToSign = ServiceStringToSign("r", string.Empty, "2026-11-06", $"/blob/{accountName}/c/a.txt", string.Empty, string.Empty, "b");
        var request = Request("GET", $"/c/a.txt?{ServiceQuery("r", string.Empty, "2026-11-06", string.Empty, string.Empty, "b", stringToSign)}", Scope("/c/a.txt", accountName: accountName));

        var verdict = await JudgeAsync(request);

        AssertForbidden(verdict, HttpAuthenticationRefusal.UnknownAccount);
    }

    [TestMethod]
    public async Task Sas_WithAnAuthorizationAsWell_IsForbiddenAsMalformed()
    {
        var request = BlobSas() with { Fields = [.. BlobSas().Fields, new("Authorization", "Bearer tok")] };

        var verdict = await JudgeAsync(request);

        AssertForbidden(verdict, HttpAuthenticationRefusal.MalformedCredentials);
    }

    // ---- Account shared access signatures ---------------------------------------------------

    private static HttpAuthenticationRequest AccountSas(string resourcePath, string grants, string services = "b", string resourceTypes = "sco", string permissions = "rwdl")
    {
        var stringToSign = $"myaccount\n{permissions}\n{services}\n{resourceTypes}\n\n2026-11-06\n\nhttps,http\n2025-01-05\n\n";
        var query = $"sv=2025-01-05&ss={services}&srt={resourceTypes}&sp={permissions}&se=2026-11-06&spr=https,http&sig={Uri.EscapeDataString(Sign(stringToSign))}";

        return Request("GET", $"{resourcePath}?{query}", Scope(resourcePath, grants));
    }

    [TestMethod]
    [DataRow("/", "l")]
    [DataRow("/c", "l")]
    [DataRow("/c/a.txt", "r")]
    public async Task AccountSas_SignedOverThePagesString_IsAcceptedAsTheAccount(string resourcePath, string grants)
    {
        var verdict = await JudgeAsync(AccountSas(resourcePath, grants));

        AssertAccepted(verdict, "Login accepted: SAS myaccount");
    }

    [TestMethod]
    [DataRow("q", "sco", "rwdl", DisplayName = "no Blob service")]
    [DataRow("b", "sc", "rwdl", DisplayName = "no object resource type")]
    [DataRow("b", "sco", "wdl", DisplayName = "no read permission")]
    public async Task AccountSas_NotGrantingTheRead_IsForbiddenAsPermissionNotGranted(string services, string resourceTypes, string permissions)
    {
        var verdict = await JudgeAsync(AccountSas("/c/a.txt", "r", services, resourceTypes, permissions));

        AssertForbidden(verdict, HttpAuthenticationRefusal.PermissionNotGranted);
    }

    [TestMethod]
    public async Task AccountSas_NoAccountNamed_IsForbiddenAsUnknownAccount()
    {
        var request = AccountSas("/c/a.txt", "r");

        var verdict = await JudgeAsync(request with { AzureStorage = request.AzureStorage! with { AccountName = null } });

        AssertForbidden(verdict, HttpAuthenticationRefusal.UnknownAccount);
    }
}
