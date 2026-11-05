using System.Net;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>
/// Protocol headers and SASL (AMQP 1.0 Part 2 section 2.2 and Part 5; RFC 4616; RFC 4505; ADR-0107
/// decisions 1 and 2). The headers and the <c>PLAIN</c> exchange are written by hand from the
/// specification; the rest is built with the library's frame codec, which BL-510's tests pin.
/// </summary>
[TestClass]
public sealed class Amqp1SecurityNegotiationTests
{
    private const string SaslHeader = "414D515003010000";
    private const string AmqpHeader = "414D515000010000";
    private const string TlsHeader = "414D515002010000";

    // sasl-mechanisms offering PLAIN (Part 5 section 5.3.3.1): one symbol, not an array.
    private const string MechanismsPlain = "0000001502010000" + "005340C00801A305504C41494E";

    // sasl-init PLAIN with the RFC 4616 message "\0alice\0secret".
    private const string InitPlainAliceSecret = "0000002402010000" + "005341C01702A305504C41494EA00D00616C69636500736563726574";

    private const string OutcomeOk = "0000001002010000" + "005344C0030150" + "00";
    private const string OutcomeAuth = "0000001002010000" + "005344C0030150" + "01";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task NegotiateAsync_PlainWithGoodCredentials_AnswersOutcomeOkThenTheAmqpHeader()
    {
        var connection = Connection(SaslHeader + InitPlainAliceSecret + AmqpHeader);
        var log = new RecordingExchangeLog();

        var result = await Negotiation(new UnitTestAmqp1SaslPolicy("PLAIN"), connection, log: log).NegotiateAsync();

        Assert.IsTrue(result.IsReadyForAmqp);
        Assert.AreEqual("alice", result.AccountName);
        Assert.AreEqual(SaslHeader + MechanismsPlain + OutcomeOk + AmqpHeader, Convert.ToHexString(connection.WrittenBytes));
        Assert.IsFalse(connection.WritesCompleted);
        CollectionAssert.Contains(log.Notes.ToList(), "Login accepted: PLAIN alice");
    }

    [TestMethod]
    public async Task NegotiateAsync_HeadersAndFramesInSingleBytes_AreReadInOrder()
    {
        var bytes = Convert.FromHexString(SaslHeader + InitPlainAliceSecret + AmqpHeader);
        var connection = new InMemoryConnection(bytes.Select(value => (ReadOnlyMemory<byte>)new[] { value }));

        var result = await Negotiation(new UnitTestAmqp1SaslPolicy("PLAIN"), connection).NegotiateAsync();

        Assert.IsTrue(result.IsReadyForAmqp);
        Assert.AreEqual(SaslHeader + MechanismsPlain + OutcomeOk + AmqpHeader, Convert.ToHexString(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task NegotiateAsync_AmqpFramePipelinedAfterTheAmqpHeader_IsLeftForTheNextLayer()
    {
        var open = Amqp1Frame.Carrying(0, new Amqp1Open("client")).Encode();
        var connection = Connection(SaslHeader + InitPlainAliceSecret + AmqpHeader + Convert.ToHexString(open));

        var result = await Negotiation(new UnitTestAmqp1SaslPolicy("PLAIN"), connection).NegotiateAsync();
        var next = await new Amqp1FrameReader(connection).ReadFrameAsync(Amqp1FrameLimits.MinMaxFrameSize, TestContext.CancellationToken);

        Assert.IsTrue(result.IsReadyForAmqp);
        Assert.IsInstanceOfType<Amqp1Open>(Amqp1FrameBody.Read(next.Frame!).Performative);
    }

    [TestMethod]
    public async Task NegotiateAsync_PlainWithABadPassword_AnswersOutcomeAuthAndCloses()
    {
        var connection = Connection(SaslHeader + Hex(Init("PLAIN", "\0alice\0wrong")) + AmqpHeader);
        var log = new RecordingExchangeLog();

        var result = await Negotiation(new UnitTestAmqp1SaslPolicy("PLAIN"), connection, log: log).NegotiateAsync();

        Assert.IsFalse(result.IsReadyForAmqp);
        Assert.IsNull(result.AccountName);
        Assert.AreEqual(SaslHeader + MechanismsPlain + OutcomeAuth, Convert.ToHexString(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        CollectionAssert.AreEqual(new[] { "Login refused: PLAIN alice", "PLAIN: wrong password" }, log.Notes.ToArray());
    }

    [TestMethod]
    public async Task NegotiateAsync_AnonymousWhenNotAllowed_IsNotOfferedAndAnsweredOutcomeAuth()
    {
        var policy = new UnitTestAmqp1SaslPolicy("PLAIN");
        var connection = Connection(SaslHeader + Hex(Init("ANONYMOUS", "trace")) + AmqpHeader);

        var result = await Negotiation(policy, connection).NegotiateAsync();

        Assert.IsFalse(result.IsReadyForAmqp);
        Assert.AreEqual(SaslHeader + MechanismsPlain + OutcomeAuth, Convert.ToHexString(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.IsEmpty(policy.Starts);
    }

    [TestMethod]
    public async Task NegotiateAsync_AnonymousWhenAllowed_IsOfferedLastAndAcceptedWithNoAccount()
    {
        var policy = new UnitTestAmqp1SaslPolicy("PLAIN");
        var connection = Connection(SaslHeader + Hex(Init("ANONYMOUS", "trace")) + AmqpHeader);

        var result = await Negotiation(policy, connection, new() { AllowsAnonymous = true }).NegotiateAsync();

        Assert.IsTrue(result.IsReadyForAmqp);
        Assert.IsNull(result.AccountName);
        var mechanisms = Hex(Amqp1Frame.Carrying(0, new Amqp1SaslMechanisms([new("PLAIN"), new("ANONYMOUS")])));
        Assert.AreEqual(SaslHeader + mechanisms + OutcomeOk + AmqpHeader, Convert.ToHexString(connection.WrittenBytes));
        Assert.IsEmpty(policy.Starts);
    }

    [TestMethod]
    public async Task NegotiateAsync_PolicyOffersAnonymousItself_IsNotListedTwice()
    {
        var connection = Connection(SaslHeader);

        await Negotiation(new UnitTestAmqp1SaslPolicy("ANONYMOUS"), connection, new() { AllowsAnonymous = true }).NegotiateAsync();

        var mechanisms = Hex(Amqp1Frame.Carrying(0, new Amqp1SaslMechanisms([new("ANONYMOUS")])));
        Assert.AreEqual(SaslHeader + mechanisms, Convert.ToHexString(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task NegotiateAsync_AnonymousOfferedByThePolicyWhenNotAllowed_RunsThroughThePolicy()
    {
        var policy = new UnitTestAmqp1SaslPolicy("ANONYMOUS")
        {
            ScriptedSteps = [new SaslLoginStep(SaslLoginOutcome.RefusedCredentials, ReadOnlyMemory<byte>.Empty, null, null)],
        };
        var connection = Connection(SaslHeader + Hex(Init("ANONYMOUS", "trace")));

        var result = await Negotiation(policy, connection).NegotiateAsync();

        Assert.IsFalse(result.IsReadyForAmqp);
        Assert.HasCount(1, policy.Starts);
        StringAssert.EndsWith(Convert.ToHexString(connection.WrittenBytes), OutcomeAuth);
    }

    [TestMethod]
    public async Task NegotiateAsync_PlainAmqpHeaderWhenAnonymousIsOff_IsAnsweredWithTheSaslHeaderAndClosed()
    {
        var connection = Connection(AmqpHeader);

        var result = await Negotiation(new UnitTestAmqp1SaslPolicy("PLAIN"), connection).NegotiateAsync();

        Assert.IsFalse(result.IsReadyForAmqp);
        Assert.AreEqual(SaslHeader, Convert.ToHexString(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task NegotiateAsync_PlainAmqpHeaderWhenAnonymousIsOn_IsAnsweredInKindWithNoAccount()
    {
        var connection = Connection(AmqpHeader);

        var result = await Negotiation(new UnitTestAmqp1SaslPolicy("PLAIN"), connection, new() { AllowsAnonymous = true }).NegotiateAsync();

        Assert.IsTrue(result.IsReadyForAmqp);
        Assert.IsNull(result.AccountName);
        Assert.AreEqual(AmqpHeader, Convert.ToHexString(connection.WrittenBytes));
        Assert.IsFalse(connection.WritesCompleted);
    }

    [TestMethod]
    [DataRow("414D515000010100", DisplayName = "AMQP 1.1.0")]
    [DataRow("414D515003020000", DisplayName = "SASL 2.0.0")]
    [DataRow("414D515000000901", DisplayName = "AMQP 0-9-1")]
    [DataRow("474554202F204854", DisplayName = "not AMQP")]
    public async Task NegotiateAsync_UnsupportedHeader_IsAnsweredWithTheSaslHeaderAndClosed(string header)
    {
        var connection = Connection(header);

        var result = await Negotiation(new UnitTestAmqp1SaslPolicy("PLAIN"), connection, new() { AllowsAnonymous = true }).NegotiateAsync();

        Assert.IsFalse(result.IsReadyForAmqp);
        Assert.AreEqual(SaslHeader, Convert.ToHexString(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task NegotiateAsync_SaslFrameAfterTheOutcome_IsAnsweredWithTheAmqpHeaderAndClosed()
    {
        var connection = Connection(SaslHeader + InitPlainAliceSecret + InitPlainAliceSecret);

        var result = await Negotiation(new UnitTestAmqp1SaslPolicy("PLAIN"), connection).NegotiateAsync();

        Assert.IsFalse(result.IsReadyForAmqp);
        Assert.AreEqual(SaslHeader + MechanismsPlain + OutcomeOk + AmqpHeader, Convert.ToHexString(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task NegotiateAsync_ClientClosesAfterTheOutcome_EndsClosedWithNoHeader()
    {
        var connection = Connection(SaslHeader + InitPlainAliceSecret);

        var result = await Negotiation(new UnitTestAmqp1SaslPolicy("PLAIN"), connection).NegotiateAsync();

        Assert.IsFalse(result.IsReadyForAmqp);
        Assert.AreEqual(SaslHeader + MechanismsPlain + OutcomeOk, Convert.ToHexString(connection.WrittenBytes));
    }

    [TestMethod]
    [DataRow("", DisplayName = "nothing")]
    [DataRow("414D5150", DisplayName = "half a header")]
    public async Task NegotiateAsync_ClientClosesBeforeAWholeHeader_WritesNothing(string bytes)
    {
        var connection = Connection(bytes);

        var result = await Negotiation(new UnitTestAmqp1SaslPolicy("PLAIN"), connection).NegotiateAsync();

        Assert.AreSame(Amqp1SecurityNegotiationResult.Closed, result);
        Assert.IsEmpty(connection.WrittenBytes);
    }

    [TestMethod]
    public async Task NegotiateAsync_TlsHeaderWithACertificate_IsAnsweredUpgradedAndNegotiatedAgainInsideTls()
    {
        var policy = new UnitTestAmqp1SaslPolicy("PLAIN");
        var connection = Connection(TlsHeader + SaslHeader + InitPlainAliceSecret + AmqpHeader);

        var result = await Negotiation(policy, connection, new() { CanUpgradeToTls = true }).NegotiateAsync();

        Assert.IsTrue(result.IsReadyForAmqp);
        Assert.IsTrue(connection.UpgradeRequested);
        Assert.AreEqual(TlsHeader + SaslHeader + MechanismsPlain + OutcomeOk + AmqpHeader, Convert.ToHexString(connection.WrittenBytes));
        Assert.AreSame(InMemoryConnection.DefaultUpgradeTlsSession, policy.OfferRequests.Single().TlsSession);
        Assert.AreSame(InMemoryConnection.DefaultUpgradeTlsSession, policy.Starts.Single().TlsSession);
    }

    [TestMethod]
    public async Task NegotiateAsync_TlsHeaderWithoutACertificate_IsAnsweredWithTheSaslHeaderAndClosed()
    {
        var connection = Connection(TlsHeader);

        var result = await Negotiation(new UnitTestAmqp1SaslPolicy("PLAIN"), connection).NegotiateAsync();

        Assert.IsFalse(result.IsReadyForAmqp);
        Assert.IsFalse(connection.UpgradeRequested);
        Assert.AreEqual(SaslHeader, Convert.ToHexString(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task NegotiateAsync_TlsHeaderInsideTls_IsAnsweredWithTheSaslHeaderAndClosed()
    {
        var connection = Connection(TlsHeader + TlsHeader);

        var result = await Negotiation(new UnitTestAmqp1SaslPolicy("PLAIN"), connection, new() { CanUpgradeToTls = true }).NegotiateAsync();

        Assert.IsFalse(result.IsReadyForAmqp);
        Assert.AreEqual(TlsHeader + SaslHeader, Convert.ToHexString(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task NegotiateAsync_TlsHandshakeFails_Throws()
    {
        var connection = new InMemoryConnection([Convert.FromHexString(TlsHeader)], upgradeFails: true);

        await Assert.ThrowsExactlyAsync<TlsHandshakeException>(
            async () => await Negotiation(new UnitTestAmqp1SaslPolicy("PLAIN"), connection, new() { CanUpgradeToTls = true }).NegotiateAsync());
    }

    [TestMethod]
    public async Task NegotiateAsync_NoMechanismOffered_SendsAnEmptyArray()
    {
        var connection = Connection(SaslHeader + Hex(Init("PLAIN", "\0alice\0secret")));

        var result = await Negotiation(new UnitTestAmqp1SaslPolicy(), connection).NegotiateAsync();

        Assert.IsFalse(result.IsReadyForAmqp);
        Assert.AreEqual(SaslHeader + "0000001202010000" + "005340C00501E00200A3" + OutcomeAuth, Convert.ToHexString(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task NegotiateAsync_ChallengeMechanism_SendsEachChallengeAndHandsOnEachResponse()
    {
        var policy = new UnitTestAmqp1SaslPolicy("CRAM-MD5")
        {
            ScriptedSteps =
            [
                UnitTestAmqp1SaslPolicy.Challenge("<1.2@surl>"),
                new SaslLoginStep(SaslLoginOutcome.Accepted, ReadOnlyMemory<byte>.Empty, "alice", new CheckedLogin("CRAM-MD5", "alice", true)),
            ],
        };
        var response = Hex(Amqp1Frame.Carrying(0, new Amqp1SaslResponse(Encoding.ASCII.GetBytes("alice digest"))));
        var connection = Connection(SaslHeader + Hex(Init("CRAM-MD5", null)) + response + AmqpHeader);

        var result = await Negotiation(policy, connection).NegotiateAsync();

        Assert.IsTrue(result.IsReadyForAmqp);
        Assert.AreEqual("alice", result.AccountName);
        Assert.IsNull(policy.Starts.Single().InitialResponse);
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("alice digest"), policy.ScriptedResponses.Single());
        var challenge = Hex(Amqp1Frame.Carrying(0, new Amqp1SaslChallenge(Encoding.ASCII.GetBytes("<1.2@surl>"))));
        var mechanisms = Hex(Amqp1Frame.Carrying(0, new Amqp1SaslMechanisms([new("CRAM-MD5")])));
        Assert.AreEqual(SaslHeader + mechanisms + challenge + OutcomeOk + AmqpHeader, Convert.ToHexString(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task NegotiateAsync_ScramServerFinal_IsSentAsTheOutcomesAdditionalData()
    {
        var policy = new UnitTestAmqp1SaslPolicy("SCRAM-SHA-256")
        {
            ScriptedSteps =
            [
                UnitTestAmqp1SaslPolicy.Challenge("server-first"),
                UnitTestAmqp1SaslPolicy.Challenge("v=proof"),
                new SaslLoginStep(SaslLoginOutcome.Accepted, ReadOnlyMemory<byte>.Empty, "alice", null),
            ],
        };
        var response = Hex(Amqp1Frame.Carrying(0, new Amqp1SaslResponse(Encoding.ASCII.GetBytes("client-final"))));
        var connection = Connection(SaslHeader + Hex(Init("SCRAM-SHA-256", "client-first")) + response + AmqpHeader);

        var result = await Negotiation(policy, connection).NegotiateAsync();

        Assert.IsTrue(result.IsReadyForAmqp);
        Assert.HasCount(2, policy.ScriptedResponses);
        Assert.IsEmpty(policy.ScriptedResponses[1]);
        var challenge = Hex(Amqp1Frame.Carrying(0, new Amqp1SaslChallenge(Encoding.ASCII.GetBytes("server-first"))));
        var outcome = Hex(Amqp1Frame.Carrying(0, new Amqp1SaslOutcome(0) { AdditionalData = Encoding.ASCII.GetBytes("v=proof") }));
        StringAssert.EndsWith(Convert.ToHexString(connection.WrittenBytes), challenge + outcome + AmqpHeader);
    }

    [TestMethod]
    public async Task NegotiateAsync_ScramRefusedAtTheServerFinal_AnswersOutcomeAuth()
    {
        var policy = new UnitTestAmqp1SaslPolicy("SCRAM-SHA-1")
        {
            ScriptedSteps =
            [
                UnitTestAmqp1SaslPolicy.Challenge("server-first"),
                UnitTestAmqp1SaslPolicy.Challenge("v=proof"),
                new SaslLoginStep(SaslLoginOutcome.RefusedCredentials, ReadOnlyMemory<byte>.Empty, null, null),
            ],
        };
        var response = Hex(Amqp1Frame.Carrying(0, new Amqp1SaslResponse(Encoding.ASCII.GetBytes("client-final"))));
        var connection = Connection(SaslHeader + Hex(Init("SCRAM-SHA-1", "client-first")) + response);

        var result = await Negotiation(policy, connection).NegotiateAsync();

        Assert.IsFalse(result.IsReadyForAmqp);
        StringAssert.EndsWith(Convert.ToHexString(connection.WrittenBytes), OutcomeAuth);
    }

    [TestMethod]
    public async Task NegotiateAsync_AcceptedWithAdditionalSuccessData_SendsItInTheOutcome()
    {
        var policy = new UnitTestAmqp1SaslPolicy("EXTERNAL")
        {
            ScriptedSteps = [new SaslLoginStep(SaslLoginOutcome.AcceptedUnchecked, ReadOnlyMemory<byte>.Empty, null, null, AdditionalSuccessData: new byte[] { 1, 2 })],
        };
        var connection = Connection(SaslHeader + Hex(Init("EXTERNAL", "")) + AmqpHeader);

        var result = await Negotiation(policy, connection).NegotiateAsync();

        Assert.IsTrue(result.IsReadyForAmqp);
        CollectionAssert.AreEqual(Array.Empty<byte>(), policy.Starts.Single().InitialResponse!.Value.ToArray());
        var outcome = Hex(Amqp1Frame.Carrying(0, new Amqp1SaslOutcome(0) { AdditionalData = [1, 2] }));
        StringAssert.EndsWith(Convert.ToHexString(connection.WrittenBytes), outcome + AmqpHeader);
    }

    [TestMethod]
    public async Task NegotiateAsync_ClientSendsSomethingElseInsteadOfAResponse_ClosesWithNoBytes()
    {
        var policy = new UnitTestAmqp1SaslPolicy("CRAM-MD5") { ScriptedSteps = [UnitTestAmqp1SaslPolicy.Challenge("<1.2@surl>")] };
        var connection = Connection(SaslHeader + Hex(Init("CRAM-MD5", null)) + Hex(Init("CRAM-MD5", null)));

        var result = await Negotiation(policy, connection).NegotiateAsync();

        Assert.IsFalse(result.IsReadyForAmqp);
        var challenge = Hex(Amqp1Frame.Carrying(0, new Amqp1SaslChallenge(Encoding.ASCII.GetBytes("<1.2@surl>"))));
        StringAssert.EndsWith(Convert.ToHexString(connection.WrittenBytes), challenge);
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    [DataRow("", DisplayName = "client closes")]
    [DataRow("0000000802010000", DisplayName = "empty SASL frame")]
    [DataRow("0000001100010000" + "005341C00401A30150", DisplayName = "sasl-init in an AMQP frame")]
    [DataRow("0000001102010000" + "005343C00401A00178", DisplayName = "sasl-response instead of sasl-init")]
    [DataRow("0000000C02010000" + "00534145", DisplayName = "sasl-init without its mechanism")]
    [DataRow("0000020102010000", DisplayName = "SASL frame above 512 bytes")]
    public async Task NegotiateAsync_AnythingButASaslInit_ClosesWithNoBytes(string frame)
    {
        var connection = Connection(SaslHeader + frame);

        var result = await Negotiation(new UnitTestAmqp1SaslPolicy("PLAIN"), connection).NegotiateAsync();

        Assert.IsFalse(result.IsReadyForAmqp);
        Assert.AreEqual(SaslHeader + MechanismsPlain, Convert.ToHexString(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task NegotiateAsync_Mechanisms_AreAskedForWithTheSchemeAndNoTlsSession()
    {
        var policy = new UnitTestAmqp1SaslPolicy("PLAIN");

        await Negotiation(policy, Connection(SaslHeader + InitPlainAliceSecret)).NegotiateAsync();

        Assert.AreEqual(new SaslOfferRequest("amqp1", null), policy.OfferRequests.Single());
        Assert.AreEqual("amqp1", policy.Starts.Single().Scheme);
        Assert.AreEqual("PLAIN", policy.Starts.Single().Mechanism);
    }

    private static InMemoryConnection Connection(string hex) => new([Convert.FromHexString(hex)]);

    private static Amqp1Frame Init(string mechanism, string? initialResponse) =>
        Amqp1Frame.Carrying(0, new Amqp1SaslInit(new Amqp1Symbol(mechanism))
        {
            InitialResponse = initialResponse is null ? null : Encoding.UTF8.GetBytes(initialResponse),
        });

    private static string Hex(Amqp1Frame frame) => Convert.ToHexString(frame.Encode());

    private Amqp1SecurityNegotiation Negotiation(
        UnitTestAmqp1SaslPolicy policy,
        InMemoryConnection connection,
        Amqp1SecurityNegotiationOptions? options = null,
        IExchangeLog? log = null) =>
        new(policy, options ?? Amqp1SecurityNegotiationOptions.Default, connection, Context(log ?? new RecordingExchangeLog()));

    private ExchangeContext Context(IExchangeLog log) => new(
        1,
        new ListenUrl("amqp1", "127.0.0.1", 5672).WithBoundPort(5672),
        new IPEndPoint(IPAddress.Loopback, 5672),
        new IPEndPoint(IPAddress.Loopback, 50000),
        log,
        TimeProvider.System,
        TestContext.CancellationToken);
}
