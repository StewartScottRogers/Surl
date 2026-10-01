using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// <see cref="NtlmAuthenticationMethod"/> alone and behind <see cref="AuthenticationPolicy"/>
/// (ADR-0039): the <c>NEGOTIATE_MESSAGE</c> and <c>AUTHENTICATE_MESSAGE</c> pinned upstream curl
/// 8.21.0 sent (Fixtures/README.md) replayed with the recorded server challenge, the
/// <c>CHALLENGE_MESSAGE</c> pinned byte for byte, and tampered, replayed and malformed answers.
/// </summary>
[TestClass]
public sealed class NtlmAuthenticationMethodTests
{
    // The CHALLENGE_MESSAGE the recordings were answered with: the one Surl sends for the
    // recorded NEGOTIATE_MESSAGE and the fixed challenge 0123456789abcdef.
    private const string RecordedChallenge =
        "TlRMTVNTUAACAAAACAAIADAAAAAFgoqgASNFZ4mrze8AAAAAAAAAABwAHAA4AAAAUwBVAFIATAACAAgAUwBVAFIATAABAAgAUwBVAFIATAAAAAAA";

    private static readonly AccountBook Accounts = new(
    [
        new Account("tester", "secret"),
        new Account(string.Empty, "tok"),
    ]);

    private static readonly HttpAuthenticationRequest GetX = PolicyFixture.Get();

    private static string RecordedAuthorization(string caseName, int requestNumber) =>
        RecordedFixture.ReadRequest(caseName, requestNumber).Fields.Single(field => field.Key == "Authorization").Value;

    private static byte[] RecordedMessage(string caseName, int requestNumber) =>
        Convert.FromBase64String(RecordedAuthorization(caseName, requestNumber)["NTLM ".Length..]);

    private static IHttpCredentialVerifier StartConnection(AccountBook? accounts = null) =>
        new NtlmAuthenticationMethod(accounts ?? Accounts, new FixedNtlmServerChallengeSource()).StartConnection();

    private static async Task<HttpCredentialCheck> VerifyAsync(IHttpCredentialVerifier verifier, string authorization) =>
        await verifier.VerifyAsync(authorization["NTLM ".Length..], GetX, CancellationToken.None);

    private static Task<HttpCredentialCheck> VerifyAsync(IHttpCredentialVerifier verifier, byte[] message) =>
        VerifyAsync(verifier, NtlmTestMessages.Authorization(message));

    private static async Task<IHttpCredentialVerifier> ChallengedConnectionAsync(AccountBook? accounts = null)
    {
        var verifier = StartConnection(accounts);
        var check = await VerifyAsync(verifier, RecordedAuthorization("ntlm", 1));
        Assert.AreEqual(HttpCredentialOutcome.Continue, check.Outcome);

        return verifier;
    }

    private static void AssertRefused(HttpCredentialCheck check, string? userAsSent = null)
    {
        Assert.AreEqual(HttpCredentialOutcome.Refused, check.Outcome);
        Assert.IsNull(check.AccountName);
        Assert.IsEmpty(check.WwwAuthenticateValues);
        Assert.AreEqual(userAsSent, check.UserAsSent);
    }

    [TestMethod]
    public void Method_Always_IsNtlm()
    {
        Assert.AreEqual(AuthenticationMethod.Ntlm, new NtlmAuthenticationMethod(Accounts).Method);
    }

    [TestMethod]
    public void CreateChallenges_Always_IsTheBareNtlmScheme()
    {
        CollectionAssert.AreEqual(new[] { "NTLM" }, new NtlmAuthenticationMethod(Accounts).CreateChallenges().ToArray());
    }

    [TestMethod]
    public void Constructor_NullAccounts_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new NtlmAuthenticationMethod(null!));
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
    public void RecordedNegotiate_FromTheWindowsReferenceBuild_OffersUnicodeAndExtendedSessionSecurity()
    {
        // 0xA2088207: Unicode, OEM, request target, NTLM, always sign, extended session
        // security, version, 128-bit and 56-bit.
        Assert.AreEqual(
            (NtlmNegotiateFlags)0xA2088207, NtlmNegotiateMessage.ReadFlags(RecordedMessage("ntlm", 1)));
    }

    [TestMethod]
    public async Task RecordedNegotiate_FixedChallenge_IsAnsweredWithTheRecordedChallengeMessage()
    {
        var check = await VerifyAsync(StartConnection(), RecordedAuthorization("ntlm", 1));

        Assert.AreEqual(HttpCredentialOutcome.Continue, check.Outcome);
        Assert.IsNull(check.AccountName);
        Assert.IsNull(check.UserAsSent);
        CollectionAssert.AreEqual(new[] { $"NTLM {RecordedChallenge}" }, check.WwwAuthenticateValues.ToArray());
    }

    [TestMethod]
    public void ChallengeMessage_RecordedNegotiate_IsPinnedByteForByte()
    {
        var expected = Convert.FromHexString(
            "4E544C4D53535000" + "02000000" + "0800080030000000" + "05828AA0" + "0123456789ABCDEF"
            + "0000000000000000" + "1C001C0038000000" + "5300550052004C00"
            + "020008005300550052004C00" + "010008005300550052004C00" + "00000000");

        var message = NtlmChallengeMessage.Create(
            NtlmNegotiateMessage.ReadFlags(RecordedMessage("ntlm", 1))!.Value, FixedNtlmServerChallengeSource.FixtureChallenge);

        CollectionAssert.AreEqual(expected, message);
        Assert.AreEqual(RecordedChallenge, Convert.ToBase64String(message));
    }

    [TestMethod]
    public void ChallengeMessage_OemOnlyClient_SetsOemAndWritesTheTargetNameInAscii()
    {
        // curl's own NTLM (the Linux and macOS builds) offers OEM strings, NTLM and NTLM2 keys only.
        var clientFlags = NtlmNegotiateFlags.Oem | NtlmNegotiateFlags.RequestTarget | NtlmNegotiateFlags.Ntlm
            | NtlmNegotiateFlags.ExtendedSessionSecurity | NtlmNegotiateFlags.AlwaysSign;
        var expected = Convert.FromHexString(
            "4E544C4D53535000" + "02000000" + "0400040030000000" + "06828A00" + "0123456789ABCDEF"
            + "0000000000000000" + "1C001C0034000000" + "5355524C"
            + "020008005300550052004C00" + "010008005300550052004C00" + "00000000");

        CollectionAssert.AreEqual(
            expected, NtlmChallengeMessage.Create(clientFlags, FixedNtlmServerChallengeSource.FixtureChallenge));
    }

    [TestMethod]
    public void ChooseFlags_ClientAskedNothing_IsTheFixedSetWithOem()
    {
        Assert.AreEqual(
            NtlmNegotiateFlags.Oem | NtlmNegotiateFlags.RequestTarget | NtlmNegotiateFlags.Ntlm | NtlmNegotiateFlags.AlwaysSign
                | NtlmNegotiateFlags.TargetTypeServer | NtlmNegotiateFlags.TargetInfo,
            NtlmChallengeMessage.ChooseFlags(NtlmNegotiateFlags.None));
    }

    [TestMethod]
    public async Task RecordedAuthenticate_AfterTheRecordedChallenge_IsAcceptedAsTheAccount()
    {
        var verifier = await ChallengedConnectionAsync();

        var check = await VerifyAsync(verifier, RecordedAuthorization("ntlm", 2));

        Assert.AreEqual(HttpCredentialOutcome.Accepted, check.Outcome);
        Assert.AreEqual("tester", check.AccountName);
        Assert.AreEqual("tester", check.UserAsSent);
        Assert.IsEmpty(check.WwwAuthenticateValues);
    }

    [TestMethod]
    public void RecordedAuthenticate_FromTheWindowsReferenceBuild_IsNtlmV2ForTesterWithNoDomain()
    {
        var answer = NtlmAuthenticateMessage.TryRead(RecordedMessage("ntlm", 2))!;

        Assert.AreEqual("tester", answer.UserName);
        Assert.AreEqual(string.Empty, answer.DomainName);
        Assert.IsGreaterThan(24, answer.NtChallengeResponse.Length);
    }

    [TestMethod]
    public async Task RecordedAuthenticate_WrongPassword_IsRefused()
    {
        var verifier = await ChallengedConnectionAsync();

        AssertRefused(await VerifyAsync(verifier, RecordedAuthorization("ntlm-wrong-password", 2)), "tester");
    }

    [TestMethod]
    public async Task RecordedAuthenticate_AccountWithThatWrongPassword_IsAccepted()
    {
        var verifier = await ChallengedConnectionAsync(new AccountBook([new Account("tester", "wrong")]));

        var check = await VerifyAsync(verifier, RecordedAuthorization("ntlm-wrong-password", 2));

        Assert.AreEqual(HttpCredentialOutcome.Accepted, check.Outcome);
        Assert.AreEqual("tester", check.AccountName);
    }

    [TestMethod]
    public async Task RecordedAuthenticate_ChangedNtProof_IsRefused()
    {
        var message = RecordedMessage("ntlm", 2);
        var answer = NtlmAuthenticateMessage.TryRead(message)!;
        var proofOffset = IndexOf(message, answer.NtChallengeResponse);
        message[proofOffset] ^= 0x01;
        var verifier = await ChallengedConnectionAsync();

        AssertRefused(await VerifyAsync(verifier, message), "tester");
    }

    [TestMethod]
    public async Task RecordedAuthenticate_ChangedBlob_IsRefused()
    {
        var message = RecordedMessage("ntlm", 2);
        var answer = NtlmAuthenticateMessage.TryRead(message)!;
        var blobOffset = IndexOf(message, answer.NtChallengeResponse) + 16 + 8;
        message[blobOffset] ^= 0x01;
        var verifier = await ChallengedConnectionAsync();

        AssertRefused(await VerifyAsync(verifier, message), "tester");
    }

    [TestMethod]
    public async Task RecordedAuthenticate_ChangedUser_IsRefused()
    {
        var message = RecordedMessage("ntlm", 2);
        var userOffset = IndexOf(message, Encoding.Unicode.GetBytes("tester"));
        message[userOffset] = (byte)'x';
        var verifier = await ChallengedConnectionAsync(
            new AccountBook([new Account("tester", "secret"), new Account("xester", "secret")]));

        AssertRefused(await VerifyAsync(verifier, message), "xester");
    }

    [TestMethod]
    public async Task Authenticate_ChangedDomain_IsRefused()
    {
        var challenge = FixedNtlmServerChallengeSource.FixtureChallenge;
        var verifier = await ChallengedConnectionAsync();

        var check = await VerifyAsync(
            verifier, NtlmTestMessages.Authenticate("tester", "OTHER", "secret", challenge, proofDomain: "SURL"));

        AssertRefused(check, "tester");
    }

    [TestMethod]
    public async Task Authenticate_DomainTheProofCovers_IsAccepted()
    {
        var challenge = FixedNtlmServerChallengeSource.FixtureChallenge;
        var verifier = await ChallengedConnectionAsync();

        var check = await VerifyAsync(verifier, NtlmTestMessages.Authenticate("tester", "SURL", "secret", challenge));

        Assert.AreEqual(HttpCredentialOutcome.Accepted, check.Outcome);
        Assert.AreEqual("tester", check.AccountName);
    }

    [TestMethod]
    public async Task Authenticate_OemStrings_IsReadOneBytePerCharacterAndAccepted()
    {
        var challenge = FixedNtlmServerChallengeSource.FixtureChallenge;
        var verifier = StartConnection();
        await VerifyAsync(verifier, NtlmTestMessages.Authorization(NtlmTestMessages.Negotiate(NtlmTestMessages.Oem)));

        var check = await VerifyAsync(
            verifier, NtlmTestMessages.Authenticate("tester", "WORKGROUP", "secret", challenge, unicode: false));

        Assert.AreEqual(HttpCredentialOutcome.Accepted, check.Outcome);
        Assert.AreEqual("tester", check.UserAsSent);
    }

    [TestMethod]
    public async Task RecordedAuthenticate_ReplayedOnANewConnection_IsRefused()
    {
        var first = await ChallengedConnectionAsync();
        Assert.AreEqual(HttpCredentialOutcome.Accepted, (await VerifyAsync(first, RecordedAuthorization("ntlm", 2))).Outcome);

        AssertRefused(await VerifyAsync(StartConnection(), RecordedAuthorization("ntlm", 2)), "tester");
    }

    [TestMethod]
    public async Task RecordedAuthenticate_ReplayedOnANewConnectionWithAnotherChallenge_IsRefused()
    {
        var verifier = new NtlmAuthenticationMethod(Accounts, new FixedNtlmServerChallengeSource(new byte[8])).StartConnection();
        await VerifyAsync(verifier, RecordedAuthorization("ntlm", 1));

        AssertRefused(await VerifyAsync(verifier, RecordedAuthorization("ntlm", 2)), "tester");
    }

    [TestMethod]
    public async Task RecordedAuthenticate_ReplayedOnTheSameConnection_IsRefusedBecauseTheChallengeIsUsedUp()
    {
        var verifier = await ChallengedConnectionAsync();
        await VerifyAsync(verifier, RecordedAuthorization("ntlm", 2));

        AssertRefused(await VerifyAsync(verifier, RecordedAuthorization("ntlm", 2)), "tester");
    }

    [TestMethod]
    public async Task RecordedAuthenticate_AfterAMalformedLeg_IsRefusedBecauseTheChallengeIsUsedUp()
    {
        var verifier = await ChallengedConnectionAsync();
        await verifier.VerifyAsync("not base64!", GetX, CancellationToken.None);

        AssertRefused(await VerifyAsync(verifier, RecordedAuthorization("ntlm", 2)), "tester");
    }

    [TestMethod]
    public async Task Authenticate_UnknownUser_IsRefused()
    {
        var challenge = FixedNtlmServerChallengeSource.FixtureChallenge;
        var verifier = await ChallengedConnectionAsync();

        AssertRefused(
            await VerifyAsync(verifier, NtlmTestMessages.Authenticate("nobody", string.Empty, "secret", challenge)), "nobody");
    }

    [TestMethod]
    public async Task Authenticate_EmptyUserWithTheBearerTokenAsPassword_IsRefused()
    {
        var challenge = FixedNtlmServerChallengeSource.FixtureChallenge;
        var verifier = await ChallengedConnectionAsync();

        AssertRefused(
            await VerifyAsync(verifier, NtlmTestMessages.Authenticate(string.Empty, string.Empty, "tok", challenge)), string.Empty);
    }

    [TestMethod]
    public async Task Authenticate_NtlmV1LengthResponse_IsRefused()
    {
        var verifier = await ChallengedConnectionAsync();

        AssertRefused(
            await VerifyAsync(verifier, NtlmTestMessages.Authenticate("tester", string.Empty, new byte[24], NtlmTestMessages.Unicode)),
            "tester");
    }

    [TestMethod]
    [DataRow("not base64!")]
    [DataRow("")]
    [DataRow("AAAA")]
    [DataRow("TlRMTVNTUAACAAAA")]
    [DataRow("TlRMTVNTUAABAAAA")]
    [DataRow("TlRMTVNTUAADAAAA")]
    public async Task VerifyAsync_MalformedOrUnexpectedMessage_IsRefused(string credentials)
    {
        var verifier = await ChallengedConnectionAsync();

        AssertRefused(await verifier.VerifyAsync(credentials, GetX, CancellationToken.None));
    }

    [TestMethod]
    [DataRow(20)]
    [DataRow(28)]
    [DataRow(36)]
    public async Task Authenticate_FieldPointingOutsideTheMessage_IsRefused(int fieldOffset)
    {
        var message = NtlmTestMessages.Authenticate(
            "tester", string.Empty, "secret", FixedNtlmServerChallengeSource.FixtureChallenge);
        message[fieldOffset + 4] = 0xFF;
        var verifier = await ChallengedConnectionAsync();

        AssertRefused(await VerifyAsync(verifier, message));
    }

    [TestMethod]
    public async Task PublicConstructor_TwoConnections_GetDifferentRandomChallenges()
    {
        var method = new NtlmAuthenticationMethod(Accounts);
        var negotiate = RecordedAuthorization("ntlm", 1);

        var first = (await VerifyAsync(method.StartConnection(), negotiate)).WwwAuthenticateValues.Single();
        var second = (await VerifyAsync(method.StartConnection(), negotiate)).WwwAuthenticateValues.Single();

        Assert.AreNotEqual(first, second);
        StringAssert.StartsWith(first, "NTLM TlRMTVNTUAACAAAA");
    }

    [TestMethod]
    public async Task Handshake_BehindThePolicy_ChallengesThenContinuesThenAcceptsWithTheLoginNote()
    {
        var policy = PolicyFixture.Create(
            Accounts,
            new ManualTimeProvider(),
            acceptedMethods: new HashSet<AuthenticationMethod> { AuthenticationMethod.Ntlm },
            httpMethods: new NtlmAuthenticationMethod(Accounts, new FixedNtlmServerChallengeSource()));
        var session = policy.StartHttpConnection(null);

        var bare = await session.JudgeAsync(PolicyFixture.Get(), CancellationToken.None);
        var challenged = await session.JudgeAsync(RecordedFixture.ReadRequest("ntlm", 1), CancellationToken.None);
        var accepted = await session.JudgeAsync(RecordedFixture.ReadRequest("ntlm", 2), CancellationToken.None);

        Assert.AreEqual(HttpAuthenticationOutcome.Challenge, bare.Outcome);
        CollectionAssert.AreEqual(new[] { "NTLM" }, bare.WwwAuthenticateValues.ToArray());
        Assert.AreEqual(HttpAuthenticationOutcome.Challenge, challenged.Outcome);
        CollectionAssert.AreEqual(new[] { $"NTLM {RecordedChallenge}" }, challenged.WwwAuthenticateValues.ToArray());
        Assert.IsNull(challenged.CheckedLogin);
        Assert.AreEqual(HttpAuthenticationOutcome.Proceed, accepted.Outcome);
        Assert.AreEqual("tester", accepted.AccountName);
        Assert.AreEqual(new CheckedLogin("NTLM", "tester", true), accepted.CheckedLogin);
    }

    private static IHttpAuthenticationSession StartPolicySession() =>
        PolicyFixture.Create(
            Accounts,
            new ManualTimeProvider(),
            acceptedMethods: new HashSet<AuthenticationMethod> { AuthenticationMethod.Ntlm },
            httpMethods: new NtlmAuthenticationMethod(Accounts, new FixedNtlmServerChallengeSource()))
        .StartHttpConnection(null);

    [TestMethod]
    public async Task RecordedSecondUrl_AfterTheHandshakeIsAccepted_ProceedsAsTheAccountUnchecked()
    {
        var session = StartPolicySession();
        await session.JudgeAsync(RecordedFixture.ReadRequest("ntlm-two-urls", 1), CancellationToken.None);
        var accepted = await session.JudgeAsync(RecordedFixture.ReadRequest("ntlm-two-urls", 2), CancellationToken.None);
        var secondUrl = RecordedFixture.ReadRequest("ntlm-two-urls", 3);

        var remembered = await session.JudgeAsync(secondUrl, CancellationToken.None);

        Assert.AreEqual(HttpAuthenticationOutcome.Proceed, accepted.Outcome);
        Assert.DoesNotContain("Authorization", secondUrl.Fields.Select(field => field.Key).ToList());
        Assert.AreEqual("/y", secondUrl.Target);
        Assert.AreEqual(HttpAuthenticationOutcome.Proceed, remembered.Outcome);
        Assert.AreEqual("tester", remembered.AccountName);
        Assert.IsEmpty(remembered.WwwAuthenticateValues);
        Assert.IsNull(remembered.CheckedLogin);
        Assert.AreEqual("tester", (await session.JudgeAsync(PolicyFixture.Put(), CancellationToken.None)).AccountName);
    }

    [TestMethod]
    public async Task RecordedSecondUrl_OnANewConnection_IsChallenged()
    {
        var verdict = await StartPolicySession().JudgeAsync(
            RecordedFixture.ReadRequest("ntlm-two-urls", 3), CancellationToken.None);

        Assert.AreEqual(HttpAuthenticationOutcome.Challenge, verdict.Outcome);
        CollectionAssert.AreEqual(new[] { "NTLM" }, verdict.WwwAuthenticateValues.ToArray());
    }

    [TestMethod]
    public async Task NewHandshake_AfterALogin_ForgetsItUntilAccepted()
    {
        var session = StartPolicySession();
        await session.JudgeAsync(RecordedFixture.ReadRequest("ntlm", 1), CancellationToken.None);
        await session.JudgeAsync(RecordedFixture.ReadRequest("ntlm", 2), CancellationToken.None);

        var restarted = await session.JudgeAsync(RecordedFixture.ReadRequest("ntlm", 1), CancellationToken.None);
        var midHandshake = await session.JudgeAsync(PolicyFixture.Get(), CancellationToken.None);

        Assert.AreEqual(HttpAuthenticationOutcome.Challenge, restarted.Outcome);
        Assert.AreEqual(HttpAuthenticationOutcome.Challenge, midHandshake.Outcome);
        Assert.IsNull(midHandshake.AccountName);
    }

    [TestMethod]
    public void RandomChallengeSource_CreateServerChallenge_IsEightBytes()
    {
        Assert.HasCount(8, RandomNtlmServerChallengeSource.Instance.CreateServerChallenge());
    }

    [TestMethod]
    public void FindNtlmAccount_UnknownOrEmptyName_IsADummyWithNoAccountName()
    {
        Assert.IsNull(Accounts.FindNtlmAccount("nobody").AccountName);
        Assert.IsNull(Accounts.FindNtlmAccount(string.Empty).AccountName);
        Assert.AreEqual("tester", Accounts.FindNtlmAccount("tester").AccountName);
        Assert.IsNull(Accounts.FindNtlmAccount("TESTER").AccountName);
    }

    [TestMethod]
    public void FindNtlmAccount_DummyAndAccount_KeepTheSameNumberOfHashes()
    {
        Assert.HasCount(NtlmPasswordHashes.Count, Accounts.FindNtlmAccount("nobody").NtHashes);
        Assert.HasCount(NtlmPasswordHashes.Count, Accounts.FindNtlmAccount("tester").NtHashes);
    }

    // BL-321: each pinned Windows build's answer for tester:pässword, from the command line and
    // from a UTF-8 -K config file (Fixtures/README.md), hashes the password its own way; BL-324
    // adds the Linux build's.
    [TestMethod]
    [DataRow("ntlm-non-ascii-password")]
    [DataRow("ntlm-non-ascii-password-utf8-config")]
    [DataRow("ntlm-non-ascii-password-static")]
    [DataRow("ntlm-non-ascii-password-static-utf8-config")]
    [DataRow("ntlm-non-ascii-password-linux")]
    public async Task RecordedAuthenticate_NonAsciiPassword_IsAcceptedAsTheAccount(string caseName)
    {
        var accounts = new AccountBook([new Account("tester", "pässword")]);
        var verifier = await ChallengedConnectionAsync(accounts);

        var check = await VerifyAsync(verifier, RecordedAuthorization(caseName, 2));

        Assert.AreEqual(HttpCredentialOutcome.Accepted, check.Outcome);
        Assert.AreEqual("tester", check.AccountName);
    }

    [TestMethod]
    [DataRow("ntlm-non-ascii-password")]
    [DataRow("ntlm-non-ascii-password-static")]
    public async Task RecordedAuthenticate_NonAsciiPasswordForAnAsciiAccount_IsRefused(string caseName)
    {
        var accounts = new AccountBook([new Account("tester", "password")]);
        var verifier = await ChallengedConnectionAsync(accounts);

        AssertRefused(await VerifyAsync(verifier, RecordedAuthorization(caseName, 2)), "tester");
    }

    // Upstream curl's own NTLM code, which the Linux and macOS builds use, widens each UTF-8
    // byte of the password (lib/curl_ntlm_core.c at curl-8_21_0); the Linux build's recording
    // proves it (BL-324).
    [TestMethod]
    public void RecordedAuthenticate_LinuxBuild_ProvesTheNtHashOfTheWidenedUtf8Password()
    {
        var message = RecordedMessage("ntlm-non-ascii-password-linux", 2);
        var length = BitConverter.ToUInt16(message, 20);
        var offset = BitConverter.ToInt32(message, 24);
        var answer = message.AsSpan(offset, length);
        var key = NtlmV2Calculation.ComputeResponseKeyNt(
            NtlmV1Calculation.ComputeNtHashOfWidenedUtf8("pässword"), "tester", string.Empty);

        var proof = NtlmV2Calculation.ComputeNtProof(
            key, FixedNtlmServerChallengeSource.FixtureChallenge, answer[NtlmV2Calculation.NtProofLength..]);

        CollectionAssert.AreEqual(answer[..NtlmV2Calculation.NtProofLength].ToArray(), proof);
    }

    [TestMethod]
    public async Task Authenticate_NtHashOfTheWidenedUtf8Password_IsAccepted()
    {
        var accounts = new AccountBook([new Account("tester", "pässword")]);
        var challenge = FixedNtlmServerChallengeSource.FixtureChallenge;
        var key = NtlmV2Calculation.ComputeResponseKeyNt(
            NtlmV1Calculation.ComputeNtHashOfWidenedUtf8("pässword"), "tester", string.Empty);
        var proof = NtlmV2Calculation.ComputeNtProof(key, challenge, NtlmTestMessages.ClientBlob);
        var message = NtlmTestMessages.Authenticate(
            "tester", string.Empty, [.. proof, .. NtlmTestMessages.ClientBlob], NtlmTestMessages.Unicode);
        var verifier = await ChallengedConnectionAsync(accounts);

        var check = await VerifyAsync(verifier, message);

        Assert.AreEqual(HttpCredentialOutcome.Accepted, check.Outcome);
        Assert.AreEqual("tester", check.AccountName);
    }

    private static int IndexOf(byte[] message, byte[] part) => message.AsSpan().IndexOf(part);
}
