using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// <see cref="NegotiateAuthenticationMethod"/> alone and behind <see cref="AuthenticationPolicy"/>
/// (ADR-0040): the NTLM messages pinned upstream curl 8.21.0 sent for BL-120 (Fixtures/README.md),
/// bare and wrapped in SPNEGO, answered for the recorded server challenge with the
/// <c>negTokenResp</c> bytes pinned; tokens offering no NTLM, Kerberos tokens and malformed DER
/// refused; what the reference build sent when offered <c>Negotiate</c> on the lane machine; and
/// the Negotiate handshakes the unpatched 8.21.0 build sent (ADR-0042).
/// </summary>
[TestClass]
public sealed class NegotiateAuthenticationMethodTests
{
    // The CHALLENGE_MESSAGE Surl sends for the recorded NEGOTIATE_MESSAGE and the fixed
    // challenge 0123456789abcdef (ADR-0039): 84 bytes. The NEGOTIATE_MESSAGE upstream curl's tag
    // 8.21.0 sent after Negotiate (Fixtures/negotiate-ntlm) gets the same bytes.
    private const string RecordedChallenge =
        "TlRMTVNTUAACAAAACAAIADAAAAAFgoqgASNFZ4mrze8AAAAAAAAAABwAHAA4AAAAUwBVAFIATAACAAgAUwBVAFIATAABAAgAUwBVAFIATAAAAAAA";

    // negTokenResp [1] { SEQUENCE { negState [0] accept-incomplete, supportedMech [1] NTLM,
    // responseToken [2] OCTET STRING (84 bytes) } }, written out by hand from RFC 4178.
    private const string FirstReplyPrefix = "A16D306B" + "A0030A0101" + "A10C060A2B06010401823702020A" + "A2560454";

    // The same without supportedMech, which RFC 4178 sends in the first reply only.
    private const string LaterReplyPrefix = "A15F305D" + "A0030A0101" + "A2560454";

    // negTokenResp { negState accept-incomplete, supportedMech NTLM }: the reply choosing NTLM
    // when the client preferred another mechanism.
    private const string ChooseNtlmReply = "A1153013" + "A0030A0101" + "A10C060A2B06010401823702020A";

    // negTokenResp { negState accept-completed }: A1 07 30 05 A0 03 0A 01 00.
    private const string AcceptCompleted = "Negotiate oQcwBaADCgEA";

    private static readonly AccountBook Accounts = new(
    [
        new Account("tester", "secret"),
        new Account(string.Empty, "tok"),
    ]);

    private static readonly HttpAuthenticationRequest GetX = PolicyFixture.Get();

    private static readonly string[] KerberosThenNtlm =
        [SpnegoTestTokens.KerberosOid, SpnegoTestTokens.MicrosoftKerberosOid, SpnegoTestTokens.NtlmOid];

    private static byte[] RecordedNtlm(int requestNumber) =>
        Convert.FromBase64String(RecordedFixture.ReadRequest("ntlm", requestNumber).Fields
            .Single(field => field.Key == "Authorization").Value["NTLM ".Length..]);

    private static byte[] RecordedNegotiate(string fixture, int requestNumber) =>
        Convert.FromBase64String(RecordedFixture.ReadRequest(fixture, requestNumber).Fields
            .Single(field => field.Key == "Authorization").Value["Negotiate ".Length..]);

    private static IHttpCredentialVerifier StartConnection() =>
        new NegotiateAuthenticationMethod(Accounts, new FixedNtlmServerChallengeSource()).StartConnection();

    private static async Task<HttpCredentialCheck> VerifyAsync(IHttpCredentialVerifier verifier, byte[] token) =>
        await verifier.VerifyAsync(Convert.ToBase64String(token), GetX, CancellationToken.None);

    private static string Reply(string prefixHex) =>
        $"Negotiate {Convert.ToBase64String([.. Convert.FromHexString(prefixHex), .. Convert.FromBase64String(RecordedChallenge)])}";

    private static void AssertContinue(HttpCredentialCheck check, string value)
    {
        Assert.AreEqual(HttpCredentialOutcome.Continue, check.Outcome);
        Assert.IsNull(check.AccountName);
        Assert.IsNull(check.UserAsSent);
        CollectionAssert.AreEqual(new[] { value }, check.WwwAuthenticateValues.ToArray());
    }

    private static void AssertRefused(HttpCredentialCheck check, string? userAsSent = null)
    {
        Assert.AreEqual(HttpCredentialOutcome.Refused, check.Outcome);
        Assert.IsNull(check.AccountName);
        Assert.IsEmpty(check.WwwAuthenticateValues);
        Assert.AreEqual(userAsSent, check.UserAsSent);
    }

    private static void AssertAccepted(HttpCredentialCheck check, params string[] values)
    {
        Assert.AreEqual(HttpCredentialOutcome.Accepted, check.Outcome);
        Assert.AreEqual("tester", check.AccountName);
        Assert.AreEqual("tester", check.UserAsSent);
        CollectionAssert.AreEqual(values, check.WwwAuthenticateValues.ToArray());
    }

    [TestMethod]
    public void Method_Always_IsNegotiate()
    {
        Assert.AreEqual(AuthenticationMethod.Negotiate, new NegotiateAuthenticationMethod(Accounts).Method);
    }

    [TestMethod]
    public void CreateChallenges_Always_IsTheBareNegotiateScheme()
    {
        CollectionAssert.AreEqual(
            new[] { "Negotiate" }, new NegotiateAuthenticationMethod(Accounts).CreateChallenges().ToArray());
    }

    [TestMethod]
    public void Constructor_NullAccounts_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new NegotiateAuthenticationMethod(null!));
    }

    [TestMethod]
    public async Task VerifyAsync_NullArguments_Throw()
    {
        var verifier = StartConnection();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await verifier.VerifyAsync(null!, GetX, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await verifier.VerifyAsync("x", null!, CancellationToken.None));
    }

    [TestMethod]
    public async Task VerifyAsync_Cancelled_Throws()
    {
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await StartConnection().VerifyAsync("x", GetX, new CancellationToken(true)));
    }

    [TestMethod]
    public void Recording_WindowsReferenceBuildOfferedNegotiate_SentNoTokenAndExitedZero()
    {
        // The build excludes NTLM from Negotiate (PackageList !ntlm), so with no Kerberos realm
        // SSPI's InitializeSecurityContext failed with SEC_E_NO_CREDENTIALS, and curl sent the
        // request without an Authorization field and gave up after the 401 (ADR-0040).
        var request = RecordedFixture.ReadRequest("negotiate-no-token", 1);

        Assert.IsFalse(request.Fields.Any(field => field.Key == "Authorization"));
        Assert.AreEqual("0", RecordedFixture.ReadText("negotiate-no-token", "exitcode.txt"));
        StringAssert.Contains(
            RecordedFixture.ReadText("negotiate-no-token", "stderr.txt"),
            "InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS");
    }

    [TestMethod]
    public async Task NegotiateRecording_UnpatchedBuild_IsChallengedBareThenAcceptedWithNoFinalToken()
    {
        // Upstream curl's tag 8.21.0 sent bare NTLM after Negotiate, with no SPNEGO around it
        // and so no mechListMIC to send or check (ADR-0040, "Measured").
        var verifier = StartConnection();

        AssertContinue(
            await VerifyAsync(verifier, RecordedNegotiate("negotiate-ntlm", 1)), $"Negotiate {RecordedChallenge}");
        AssertAccepted(await VerifyAsync(verifier, RecordedNegotiate("negotiate-ntlm", 2)));
        Assert.AreEqual("ok", RecordedFixture.ReadText("negotiate-ntlm", "stdout.bin"));
    }

    [TestMethod]
    public async Task NegotiateRecording_UnpatchedBuildWrongPassword_IsRefusedNamingTheUser()
    {
        var verifier = StartConnection();

        AssertContinue(
            await VerifyAsync(verifier, RecordedNegotiate("negotiate-ntlm-wrong-password", 1)),
            $"Negotiate {RecordedChallenge}");
        AssertRefused(await VerifyAsync(verifier, RecordedNegotiate("negotiate-ntlm-wrong-password", 2)), "tester");
        Assert.AreEqual("22", RecordedFixture.ReadText("negotiate-ntlm-wrong-password", "exitcode.txt"));
    }

    [TestMethod]
    public async Task BareNtlm_RecordedMessages_AreChallengedBareThenAcceptedWithNoFinalToken()
    {
        var verifier = StartConnection();

        AssertContinue(await VerifyAsync(verifier, RecordedNtlm(1)), $"Negotiate {RecordedChallenge}");
        AssertAccepted(await VerifyAsync(verifier, RecordedNtlm(2)));
    }

    [TestMethod]
    public async Task Spnego_NtlmFirstWithRecordedNegotiate_IsAnsweredWithThePinnedNegTokenResp()
    {
        var token = SpnegoTestTokens.NegTokenInit([SpnegoTestTokens.NtlmOid], RecordedNtlm(1));

        AssertContinue(await VerifyAsync(StartConnection(), token), Reply(FirstReplyPrefix));
    }

    [TestMethod]
    public async Task Spnego_NtlmFirst_RecordedAnswerIsAcceptedWithAcceptCompleted()
    {
        var verifier = StartConnection();
        await VerifyAsync(verifier, SpnegoTestTokens.NegTokenInit([SpnegoTestTokens.NtlmOid], RecordedNtlm(1)));

        AssertAccepted(await VerifyAsync(verifier, SpnegoTestTokens.NegTokenResp(RecordedNtlm(2))), AcceptCompleted);
    }

    [TestMethod]
    public async Task Spnego_EveryOptionalFieldPresent_IsReadPastThem()
    {
        var verifier = StartConnection();
        var init = SpnegoTestTokens.NegTokenInit(
            [SpnegoTestTokens.NtlmOid], RecordedNtlm(1), withReqFlags: true, withMechListMic: true);

        AssertContinue(await VerifyAsync(verifier, init), Reply(FirstReplyPrefix));
        AssertAccepted(
            await VerifyAsync(verifier, SpnegoTestTokens.NegTokenResp(RecordedNtlm(2), withEveryOtherField: true)),
            AcceptCompleted);
    }

    [TestMethod]
    public async Task Spnego_KerberosPreferred_ChoosesNtlmThenRunsItsHandshake()
    {
        var verifier = StartConnection();
        var init = SpnegoTestTokens.NegTokenInit(KerberosThenNtlm, [0x60, 0x00]);

        AssertContinue(await VerifyAsync(verifier, init), $"Negotiate {Convert.ToBase64String(Convert.FromHexString(ChooseNtlmReply))}");
        AssertContinue(await VerifyAsync(verifier, SpnegoTestTokens.NegTokenResp(RecordedNtlm(1))), Reply(LaterReplyPrefix));
        AssertAccepted(await VerifyAsync(verifier, SpnegoTestTokens.NegTokenResp(RecordedNtlm(2))), AcceptCompleted);
    }

    [TestMethod]
    public async Task Spnego_NtlmFirstWithoutMechToken_ChoosesNtlm()
    {
        var init = SpnegoTestTokens.NegTokenInit([SpnegoTestTokens.NtlmOid], null);

        AssertContinue(
            await VerifyAsync(StartConnection(), init),
            $"Negotiate {Convert.ToBase64String(Convert.FromHexString(ChooseNtlmReply))}");
    }

    [TestMethod]
    public async Task Spnego_WrongPassword_IsRefusedNamingTheUser()
    {
        var verifier = StartConnection();
        await VerifyAsync(verifier, SpnegoTestTokens.NegTokenInit([SpnegoTestTokens.NtlmOid], RecordedNtlm(1)));
        var answer = NtlmTestMessages.Authenticate("tester", string.Empty, "wrong", FixedNtlmServerChallengeSource.FixtureChallenge);

        AssertRefused(await VerifyAsync(verifier, SpnegoTestTokens.NegTokenResp(answer)), "tester");
    }

    [TestMethod]
    public async Task Spnego_NoNtlmOffered_IsRefused()
    {
        var init = SpnegoTestTokens.NegTokenInit(
            [SpnegoTestTokens.KerberosOid, SpnegoTestTokens.MicrosoftKerberosOid], [0x60, 0x00]);

        AssertRefused(await VerifyAsync(StartConnection(), init));
    }

    [TestMethod]
    public async Task Spnego_NtlmFirstButMechTokenNotNtlm_IsRefused()
    {
        var init = SpnegoTestTokens.NegTokenInit([SpnegoTestTokens.NtlmOid], [1, 2, 3]);

        AssertRefused(await VerifyAsync(StartConnection(), init));
    }

    [TestMethod]
    [DataRow("60", DisplayName = "truncated InitialContextToken")]
    [DataRow("6003060180", DisplayName = "an object identifier that is not DER")]
    [DataRow("600B06092A864886F712010202", DisplayName = "a bare Kerberos token")]
    [DataRow("600806062B0601050502", DisplayName = "SPNEGO with no NegTokenInit")]
    [DataRow("600A06062B0601050502A000", DisplayName = "an empty NegTokenInit wrapper")]
    [DataRow("600E06062B0601050502A0043002A000", DisplayName = "an empty mechTypes field")]
    [DataRow("600C06062B0601050502A0023000" + "00", DisplayName = "trailing bytes after")]
    [DataRow("FF00", DisplayName = "neither NTLM nor SPNEGO")]
    public async Task VerifyAsync_MalformedToken_IsRefusedNeverThrown(string hex)
    {
        AssertRefused(await VerifyAsync(StartConnection(), Convert.FromHexString(hex)));
    }

    [TestMethod]
    public async Task VerifyAsync_NotBase64_IsRefused()
    {
        AssertRefused(await StartConnection().VerifyAsync("*not base64*", GetX, CancellationToken.None));
    }

    [TestMethod]
    [DataRow("A1", DisplayName = "truncated negTokenResp")]
    [DataRow("A1023000" + "00", DisplayName = "trailing bytes after")]
    [DataRow("A1033001" + "00", DisplayName = "a field that is not DER")]
    [DataRow("A104300200" + "00", DisplayName = "trailing bytes inside")]
    [DataRow("A1023000", DisplayName = "no responseToken")]
    [DataRow("A1073005A2030401", DisplayName = "a responseToken cut short")]
    [DataRow("A1073005A20304" + "0100", DisplayName = "a responseToken that is not NTLM")]
    [DataRow("A10A3008A2060400" + "0400", DisplayName = "two values in the responseToken field")]
    public async Task NegTokenResp_MalformedAfterSpnegoStarted_IsRefused(string hex)
    {
        var verifier = StartConnection();
        await VerifyAsync(verifier, SpnegoTestTokens.NegTokenInit([SpnegoTestTokens.NtlmOid], RecordedNtlm(1)));

        AssertRefused(await VerifyAsync(verifier, Convert.FromHexString(hex)));
        AssertRefused(await VerifyAsync(verifier, SpnegoTestTokens.NegTokenResp(RecordedNtlm(2))));
    }

    [TestMethod]
    public async Task NegTokenResp_OnAConnectionWithNoSpnego_IsRefusedAndUsesTheChallengeUp()
    {
        var verifier = StartConnection();
        await VerifyAsync(verifier, RecordedNtlm(1));

        AssertRefused(await VerifyAsync(verifier, SpnegoTestTokens.NegTokenResp(RecordedNtlm(2))));
        AssertRefused(await VerifyAsync(verifier, RecordedNtlm(2)), "tester");
    }

    [TestMethod]
    public async Task NegTokenInit_RefusedAfterAChallenge_UsesTheChallengeUp()
    {
        var verifier = StartConnection();
        await VerifyAsync(verifier, RecordedNtlm(1));

        AssertRefused(await VerifyAsync(verifier, SpnegoTestTokens.NegTokenInit([SpnegoTestTokens.KerberosOid], null)));
        AssertRefused(await VerifyAsync(verifier, RecordedNtlm(2)), "tester");
    }

    [TestMethod]
    public async Task NegTokenResp_AfterAcceptance_IsRefused()
    {
        var verifier = StartConnection();
        await VerifyAsync(verifier, SpnegoTestTokens.NegTokenInit([SpnegoTestTokens.NtlmOid], RecordedNtlm(1)));
        await VerifyAsync(verifier, SpnegoTestTokens.NegTokenResp(RecordedNtlm(2)));

        AssertRefused(await VerifyAsync(verifier, SpnegoTestTokens.NegTokenResp(RecordedNtlm(2))));
    }

    [TestMethod]
    public async Task Spnego_ReplayedOnANewConnection_IsRefused()
    {
        var first = StartConnection();
        await VerifyAsync(first, SpnegoTestTokens.NegTokenInit([SpnegoTestTokens.NtlmOid], RecordedNtlm(1)));
        await VerifyAsync(first, SpnegoTestTokens.NegTokenResp(RecordedNtlm(2)));
        var second = StartConnection();
        await VerifyAsync(second, SpnegoTestTokens.NegTokenInit(KerberosThenNtlm, null));

        AssertRefused(await VerifyAsync(second, SpnegoTestTokens.NegTokenResp(RecordedNtlm(2))), "tester");
    }

    [TestMethod]
    public async Task Handshake_BehindThePolicy_ChallengesThenContinuesThenAcceptsWithTheFinalToken()
    {
        var policy = PolicyFixture.Create(
            Accounts,
            new ManualTimeProvider(),
            acceptedMethods: new HashSet<AuthenticationMethod> { AuthenticationMethod.Negotiate },
            httpMethods: new NegotiateAuthenticationMethod(Accounts, new FixedNtlmServerChallengeSource()));
        var session = policy.StartHttpConnection(null);
        var init = SpnegoTestTokens.NegTokenInit(KerberosThenNtlm, [0x60, 0x00]);

        var bare = await session.JudgeAsync(PolicyFixture.Get(), CancellationToken.None);
        var chosen = await session.JudgeAsync(PolicyFixture.Get(SpnegoTestTokens.Authorization(init)), CancellationToken.None);
        var challenged = await session.JudgeAsync(
            PolicyFixture.Get(SpnegoTestTokens.Authorization(SpnegoTestTokens.NegTokenResp(RecordedNtlm(1)))),
            CancellationToken.None);
        var accepted = await session.JudgeAsync(
            PolicyFixture.Get(SpnegoTestTokens.Authorization(SpnegoTestTokens.NegTokenResp(RecordedNtlm(2)))),
            CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "Negotiate" }, bare.WwwAuthenticateValues.ToArray());
        Assert.AreEqual(HttpAuthenticationOutcome.Challenge, chosen.Outcome);
        Assert.AreEqual(HttpAuthenticationOutcome.Challenge, challenged.Outcome);
        CollectionAssert.AreEqual(new[] { Reply(LaterReplyPrefix) }, challenged.WwwAuthenticateValues.ToArray());
        Assert.AreEqual(HttpAuthenticationOutcome.Proceed, accepted.Outcome);
        Assert.AreEqual("tester", accepted.AccountName);
        CollectionAssert.AreEqual(new[] { AcceptCompleted }, accepted.WwwAuthenticateValues.ToArray());
        Assert.AreEqual(new CheckedLogin("Negotiate", "tester", true), accepted.CheckedLogin);
    }

    [TestMethod]
    public async Task NoNtlmOffered_BehindThePolicy_IsAnsweredWithEveryChallengeAfterTheDelay()
    {
        var clock = new ManualTimeProvider();
        var policy = PolicyFixture.Create(
            Accounts,
            clock,
            acceptedMethods: new HashSet<AuthenticationMethod> { AuthenticationMethod.Negotiate },
            httpMethods: new NegotiateAuthenticationMethod(Accounts));
        var kerberosOnly = SpnegoTestTokens.NegTokenInit([SpnegoTestTokens.KerberosOid], [0x60, 0x00]);

        var judging = policy.StartHttpConnection(null)
            .JudgeAsync(PolicyFixture.Get(SpnegoTestTokens.Authorization(kerberosOnly)), CancellationToken.None)
            .AsTask();
        Assert.IsFalse(judging.IsCompleted);
        clock.Advance(AuthenticationPolicy.RefusalDelay);
        var refused = await judging;

        Assert.AreEqual(HttpAuthenticationOutcome.Challenge, refused.Outcome);
        CollectionAssert.AreEqual(new[] { "Negotiate" }, refused.WwwAuthenticateValues.ToArray());
        Assert.AreEqual(new CheckedLogin("Negotiate", null, false), refused.CheckedLogin);
    }

    private static IHttpAuthenticationSession StartPolicySession() =>
        PolicyFixture.Create(
            Accounts,
            new ManualTimeProvider(),
            acceptedMethods: new HashSet<AuthenticationMethod> { AuthenticationMethod.Negotiate },
            httpMethods: new NegotiateAuthenticationMethod(Accounts, new FixedNtlmServerChallengeSource()))
        .StartHttpConnection(null);

    [TestMethod]
    public async Task RecordedSecondUrl_AfterTheHandshakeIsAccepted_ProceedsAsTheAccountUnchecked()
    {
        // The unpatched 8.21.0 build sent GET /y on the logged-in connection with no
        // Authorization and printed okok (Fixtures/negotiate-ntlm-two-urls, ADR-0044).
        var session = StartPolicySession();
        var challenged = await session.JudgeAsync(
            RecordedFixture.ReadRequest("negotiate-ntlm-two-urls", 1), CancellationToken.None);
        var accepted = await session.JudgeAsync(
            RecordedFixture.ReadRequest("negotiate-ntlm-two-urls", 2), CancellationToken.None);
        var secondUrl = RecordedFixture.ReadRequest("negotiate-ntlm-two-urls", 3);

        var remembered = await session.JudgeAsync(secondUrl, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { $"Negotiate {RecordedChallenge}" }, challenged.WwwAuthenticateValues.ToArray());
        Assert.AreEqual(HttpAuthenticationOutcome.Proceed, accepted.Outcome);
        Assert.AreEqual("tester", accepted.AccountName);
        Assert.DoesNotContain("Authorization", secondUrl.Fields.Select(field => field.Key).ToList());
        Assert.AreEqual("/y", secondUrl.Target);
        Assert.AreEqual(HttpAuthenticationOutcome.Proceed, remembered.Outcome);
        Assert.AreEqual("tester", remembered.AccountName);
        Assert.IsEmpty(remembered.WwwAuthenticateValues);
        Assert.IsNull(remembered.CheckedLogin);
        Assert.AreEqual("okok", RecordedFixture.ReadText("negotiate-ntlm-two-urls", "stdout.bin"));
    }

    [TestMethod]
    public async Task RecordedSecondUrl_OnANewConnection_IsChallenged()
    {
        var verdict = await StartPolicySession().JudgeAsync(
            RecordedFixture.ReadRequest("negotiate-ntlm-two-urls", 3), CancellationToken.None);

        Assert.AreEqual(HttpAuthenticationOutcome.Challenge, verdict.Outcome);
        CollectionAssert.AreEqual(new[] { "Negotiate" }, verdict.WwwAuthenticateValues.ToArray());
    }

    [TestMethod]
    public async Task NewHandshake_AfterALogin_ForgetsItUntilAccepted()
    {
        var session = StartPolicySession();
        await session.JudgeAsync(RecordedFixture.ReadRequest("negotiate-ntlm", 1), CancellationToken.None);
        await session.JudgeAsync(RecordedFixture.ReadRequest("negotiate-ntlm", 2), CancellationToken.None);

        var restarted = await session.JudgeAsync(RecordedFixture.ReadRequest("negotiate-ntlm", 1), CancellationToken.None);
        var midHandshake = await session.JudgeAsync(PolicyFixture.Get(), CancellationToken.None);

        Assert.AreEqual(HttpAuthenticationOutcome.Challenge, restarted.Outcome);
        Assert.AreEqual(HttpAuthenticationOutcome.Challenge, midHandshake.Outcome);
        Assert.IsNull(midHandshake.AccountName);
    }

    [TestMethod]
    public async Task PublicConstructor_TwoConnections_GetDifferentRandomChallenges()
    {
        var method = new NegotiateAuthenticationMethod(Accounts);

        var first = (await VerifyAsync(method.StartConnection(), RecordedNtlm(1))).WwwAuthenticateValues.Single();
        var second = (await VerifyAsync(method.StartConnection(), RecordedNtlm(1))).WwwAuthenticateValues.Single();

        Assert.AreNotEqual(first, second);
        StringAssert.StartsWith(first, "Negotiate TlRMTVNTUAACAAAA");
    }
}
