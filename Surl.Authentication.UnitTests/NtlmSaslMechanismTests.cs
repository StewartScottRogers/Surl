using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// <see cref="AuthenticationPolicy"/>'s SASL <c>NTLM</c> exchange against ADR-0049 sections 5 and
/// 7: the type 1 and type 3 messages upstream curl 8.21.0 sent when ADR-0049 measured it
/// (<c>-u user:secret</c>, <c>--sasl-ir</c>, SMTP), answered with the server challenge
/// <c>0123456789abcdef</c> and ADR-0039's type 2 message.
/// </summary>
[TestClass]
public sealed class NtlmSaslMechanismTests
{
    // ADR-0049, "What upstream curl 8.21.0 does (measured)".
    private static readonly byte[] CurlType1 =
        Convert.FromBase64String("TlRMTVNTUAABAAAAB4IIogAAAAAAAAAAAAAAAAAAAAAKAPRlAAAADw==");

    private static readonly byte[] SurlType2 = Convert.FromBase64String(
        "TlRMTVNTUAACAAAACAAIADAAAAAFgoqgASNFZ4mrze8AAAAAAAAAABwAHAA4AAAAUwBVAFIATAACAAgAUwBVAFIATAABAAgAUwBVAFIATAAAAAAA");

    private static readonly byte[] CurlType3 = Convert.FromBase64String(
        "TlRMTVNTUAADAAAAGAAYAH4AAADUANQAlgAAAAAAAABYAAAACAAIAFgAAAAeAB4AYAAAAAAAAABqAQAABYKIogoA9GUAAAAPpgITG5q/UykWS+PZmsHf4HUAcwBlAHIAUwBUAEUAVwBBAFIAVAAtAFIATwBHAEUAUgBTAC0AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAIWk1twMV/l4rJ3YcUmGEWwEBAAAAAAAAswFk93xQ3QG9LFx5Aw4sWQAAAAACAAgAUwBVAFIATAABAAgAUwBVAFIATAAIAFAAUAAAAAAAAAABAAAAACAAAA82fx1uMgwq+62Zq5sV4iBe8HQknDfjMjud6b7ltW9Twv580Os8fiRrKHCVeMX/KLHTmbLzKHclJA6owo/zF5gKABAAAAAAAAAAAAAAAAAAAAAAAAkAHABzAG0AdABwAC8AMQAyADcALgAwAC4AMAAuADEAAAAAAAAAAAA=");

    private static readonly HashSet<AuthenticationMethod> EveryMethod = [.. Enum.GetValues<AuthenticationMethod>()];

    private readonly ManualTimeProvider clock = new();

    private AuthenticationPolicy Policy(AccountBook? accounts = null, bool allowAnonymous = false) =>
        PolicyFixture.CreateWithFixedNonces(accounts ?? SaslExchangeRunner.UserAndToken, clock, allowAnonymous, EveryMethod);

    private static ISaslExchange Start(AuthenticationPolicy policy, byte[]? initialResponse = null) =>
        SaslExchangeRunner.Start(policy, "NTLM", initialResponse, null);

    private static async Task<MailLoginStep> Undelayed(ValueTask<MailLoginStep> pending)
    {
        Assert.IsTrue(pending.IsCompleted);

        return await pending;
    }

    // A refusal waits the refusal delay on the injected clock, and only then is answered.
    private async Task<MailLoginStep> AfterTheRefusalDelay(ValueTask<MailLoginStep> pending)
    {
        var step = pending.AsTask();
        clock.Advance(AuthenticationPolicy.RefusalDelay - TimeSpan.FromMilliseconds(1));
        Assert.IsFalse(step.IsCompleted);
        clock.Advance(TimeSpan.FromMilliseconds(1));

        return await step;
    }

    private static void AssertChallenge(byte[] expected, MailLoginStep step)
    {
        Assert.AreEqual(MailLoginOutcome.Challenge, step.Outcome);
        CollectionAssert.AreEqual(expected, step.Challenge.ToArray());
        Assert.IsNull(step.AccountName);
        Assert.IsNull(step.CheckedLogin);
    }

    private static void AssertRefused(string note, MailLoginStep step)
    {
        Assert.AreEqual(MailLoginOutcome.RefusedCredentials, step.Outcome);
        Assert.IsNull(step.AccountName);
        Assert.AreEqual(note, step.CheckedLogin?.Note);
    }

    [TestMethod]
    public async Task CurlsMessages_WithInitialResponse_AreAnsweredWithTheType2AndAccepted()
    {
        var exchange = Start(Policy(), CurlType1);

        AssertChallenge(SurlType2, await Undelayed(exchange.BeginAsync(CancellationToken.None)));
        var step = await Undelayed(exchange.ContinueAsync(CurlType3, CancellationToken.None));

        Assert.AreEqual(MailLoginOutcome.Accepted, step.Outcome);
        Assert.AreEqual("user", step.AccountName);
        Assert.AreEqual("Login accepted: NTLM user", step.CheckedLogin?.Note);
    }

    [TestMethod]
    public async Task CurlsMessages_WithoutInitialResponse_BeginWithAnEmptyChallenge()
    {
        var exchange = Start(Policy());

        AssertChallenge([], await Undelayed(exchange.BeginAsync(CancellationToken.None)));
        AssertChallenge(SurlType2, await Undelayed(exchange.ContinueAsync(CurlType1, CancellationToken.None)));
        var step = await Undelayed(exchange.ContinueAsync(CurlType3, CancellationToken.None));

        Assert.AreEqual(MailLoginOutcome.Accepted, step.Outcome);
        Assert.AreEqual("user", step.AccountName);
    }

    [TestMethod]
    [DataRow("wrong password")]
    [DataRow("no such user")]
    public async Task CurlsType3_NotMatchingAnAccount_IsRefusedAfterTheDelay(string accounts)
    {
        var book = accounts == "wrong password" ? new AccountBook([new Account("user", "other")]) : PolicyFixture.NoAccounts;
        var exchange = Start(Policy(book), CurlType1);
        await exchange.BeginAsync(CancellationToken.None);

        AssertRefused("Login refused: NTLM user", await AfterTheRefusalDelay(exchange.ContinueAsync(CurlType3, CancellationToken.None)));
    }

    [TestMethod]
    public async Task Type3_AsInitialResponse_IsRefusedAfterTheDelay_SinceNoChallengeWasIssued()
    {
        var step = await AfterTheRefusalDelay(Start(Policy(), CurlType3).BeginAsync(CancellationToken.None));

        AssertRefused("Login refused: NTLM user", step);
    }

    [TestMethod]
    [DataRow(new byte[0], DisplayName = "empty")]
    [DataRow(new byte[] { 0x6E, 0x6F }, DisplayName = "not NTLM")]
    [DataRow(new byte[] { 0x4E, 0x54, 0x4C, 0x4D, 0x53, 0x53, 0x50, 0x00, 0x03, 0x00, 0x00, 0x00 }, DisplayName = "truncated type 3")]
    public async Task MalformedMessage_IsRefusedAfterTheDelayWithNoUser(byte[] message)
    {
        var exchange = Start(Policy(), CurlType1);
        await exchange.BeginAsync(CancellationToken.None);

        AssertRefused("Login refused: NTLM", await AfterTheRefusalDelay(exchange.ContinueAsync(message, CancellationToken.None)));
    }

    [TestMethod]
    public async Task SecondType1_IsRefusedAfterTheDelay_OneChallengePerExchange()
    {
        var exchange = Start(Policy(), CurlType1);
        await exchange.BeginAsync(CancellationToken.None);

        AssertRefused("Login refused: NTLM", await AfterTheRefusalDelay(exchange.ContinueAsync(CurlType1, CancellationToken.None)));
    }

    [TestMethod]
    public async Task AllowAnonymous_RunsTheSteps_ThenAcceptsTheType3Unchecked()
    {
        var exchange = Start(Policy(PolicyFixture.NoAccounts, allowAnonymous: true));

        AssertChallenge([], await exchange.BeginAsync(CancellationToken.None));
        AssertChallenge(SurlType2, await exchange.ContinueAsync(CurlType1, CancellationToken.None));
        var step = await Undelayed(exchange.ContinueAsync(CurlType3, CancellationToken.None));

        Assert.AreEqual(new MailLoginStep(MailLoginOutcome.AcceptedUnchecked, ReadOnlyMemory<byte>.Empty, null, null), step);
    }

    [TestMethod]
    public async Task NtlmAccepted_IsOfferedWithoutTls_SinceItSendsNoPlaintextSecret()
    {
        var offer = Policy().GetMailLoginOffer(null);

        CollectionAssert.Contains(offer.SaslMechanisms.ToArray(), "NTLM");
        Assert.AreEqual(MailLoginOutcome.Challenge, (await Start(Policy()).BeginAsync(CancellationToken.None)).Outcome);
    }
}
