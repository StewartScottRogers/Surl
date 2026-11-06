using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaTestExchanges;

namespace Surl.Extended.Protocol.Kafka.UnitTests;

/// <summary>
/// SaslAuthenticate v0 and v1 with <c>PLAIN</c> (BL-518; ADR-0105 decision 5; RFC 4616), every
/// byte written by hand from the Kafka protocol guide's SaslAuthenticate schema.
/// </summary>
[TestClass]
public sealed class KafkaSaslAuthenticateAnswererTests
{
    private static readonly byte[] PlainHandshake = Request(KafkaApiKey.SaslHandshake, 1, String("PLAIN"));

    private static readonly byte[] HandshakeAccepted = Response([0, 0, 0, 0, 0, 1, .. String("PLAIN")]);

    // error_code 0, error_message null, auth_bytes empty.
    private static readonly byte[] Accepted = [0, 0, 0xFF, 0xFF, 0, 0, 0, 0];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task AnswerAsync_NullArguments_Throw()
    {
        var answerer = new KafkaSaslAuthenticateAnswerer();
        var request = new KafkaRequest(new KafkaRequestHeader(36, 0, 1, null), KafkaServedApis.Find(36)!, new KafkaPrimitiveReader(Array.Empty<byte>()), new KafkaConnectionState(null), new InMemoryConnection([]));

        Assert.AreEqual(KafkaApiKey.SaslAuthenticate, answerer.ApiKey);
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await answerer.AnswerAsync(null!, Context(TestContext.CancellationToken)));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await answerer.AnswerAsync(request, null!));
    }

    [TestMethod]
    public async Task ServeAsync_GoodPlainOnVersionZero_IsAcceptedAndMetadataIsServedAfterIt()
    {
        var policy = Policy();
        var metadata = EmptyBusMetadata();

        var (written, log) = await ServeAsync(
            policy,
            metadata,
            TestContext.CancellationToken,
            PlainHandshake,
            Request(KafkaApiKey.SaslAuthenticate, 0, Bytes("\0alice\0secret")),
            Request(KafkaApiKey.Metadata, 0, [0, 0, 0, 0]));

        byte[] metadataZero = Response([0, 0, 0, 1, 0, 0, 0, 0, .. String("127.0.0.1"), 0, 0, 0x23, 0x84, 0, 0, 0, 0]);
        CollectionAssert.AreEqual(HandshakeAccepted.Concat(Response(Accepted)).Concat(metadataZero).ToArray(), written);
        Assert.AreEqual("Login accepted: PLAIN alice", log.Notes.Single());
        Assert.AreEqual(new SaslExchangeStart("kafka", "PLAIN", policy.Starts.Single().InitialResponse, null), policy.Starts.Single());
        Assert.AreEqual(1, policy.AnonymousChecks);
    }

    [TestMethod]
    public async Task ServeAsync_GoodPlainOnVersionOne_AddsSessionLifetimeZero()
    {
        var (written, _) = await ServeAsync(Policy(), EmptyBusMetadata(), TestContext.CancellationToken, PlainHandshake, Request(KafkaApiKey.SaslAuthenticate, 1, Bytes("alice\0alice\0secret")));

        CollectionAssert.AreEqual(HandshakeAccepted.Concat(Response([.. Accepted, 0, 0, 0, 0, 0, 0, 0, 0])).ToArray(), written);
    }

    [TestMethod]
    [DataRow((short)0, new byte[0])]
    [DataRow((short)1, new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 })]
    public async Task ServeAsync_BadPlain_IsSaslAuthenticationFailedThenClosed(short version, byte[] sessionLifetime)
    {
        var (written, log) = await ServeAsync(
            Policy(),
            EmptyBusMetadata(),
            TestContext.CancellationToken,
            PlainHandshake,
            Request(KafkaApiKey.SaslAuthenticate, version, Bytes("\0alice\0wrong")),
            Request(KafkaApiKey.ApiVersions, 0));

        byte[] refused = [0, 58, .. String("Authentication failed"), 0, 0, 0, 0, .. sessionLifetime];
        CollectionAssert.AreEqual(HandshakeAccepted.Concat(Response(refused)).ToArray(), written);
        CollectionAssert.AreEqual(
            new[] { "Login refused: PLAIN alice", "PLAIN: wrong password", "The SASL login was refused; closed after the reply." },
            log.Notes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_SaslAuthenticateWithoutAHandshake_ClosesWithNoBytes()
    {
        var (written, log) = await ServeAsync(Policy(), EmptyBusMetadata(), TestContext.CancellationToken, Request(KafkaApiKey.SaslAuthenticate, 0, Bytes("\0alice\0secret")));

        Assert.IsEmpty(written);
        Assert.AreEqual("A SaslAuthenticate came without a SaslHandshake first; closed with no reply.", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_SaslAuthenticateAfterARefusedHandshake_ClosesWithNoBytes()
    {
        var (written, _) = await ServeAsync(
            Policy(),
            EmptyBusMetadata(),
            TestContext.CancellationToken,
            Request(KafkaApiKey.SaslHandshake, 1, String("GSSAPI")),
            Request(KafkaApiKey.SaslAuthenticate, 0, Bytes("x")));

        CollectionAssert.AreEqual(Response([0, 33, 0, 0, 0, 1, .. String("PLAIN")]), written);
    }

    [TestMethod]
    public async Task ServeAsync_GoodPlainInARawTokenAfterAVersionZeroHandshake_IsAnsweredWithAnEmptyToken()
    {
        var (written, _) = await ServeAsync(
            Policy(),
            EmptyBusMetadata(),
            TestContext.CancellationToken,
            Request(KafkaApiKey.SaslHandshake, 0, String("PLAIN")),
            Bytes("\0alice\0secret"),
            Request(KafkaApiKey.Metadata, 0, [0, 0, 0, 0]));

        byte[] metadataZero = Response([0, 0, 0, 1, 0, 0, 0, 0, .. String("127.0.0.1"), 0, 0, 0x23, 0x84, 0, 0, 0, 0]);
        CollectionAssert.AreEqual(HandshakeAccepted.Concat(new byte[] { 0, 0, 0, 0 }).Concat(metadataZero).ToArray(), written);
    }

    [TestMethod]
    public async Task ServeAsync_BadPlainInARawToken_ClosesWithNoBytes()
    {
        var (written, log) = await ServeAsync(
            Policy(),
            EmptyBusMetadata(),
            TestContext.CancellationToken,
            Request(KafkaApiKey.SaslHandshake, 0, String("PLAIN")),
            Bytes("\0alice\0wrong"));

        CollectionAssert.AreEqual(HandshakeAccepted, written);
        Assert.AreEqual("The SASL login was refused; closed with no reply.", log.Notes.Last());
    }

    [TestMethod]
    public async Task ServeAsync_ChallengeInARawToken_IsSentAsARawToken()
    {
        var policy = new UnitTestKafkaAuthenticationPolicy(false, "SCRAM-SHA-256")
        {
            ScriptedSteps = [UnitTestKafkaAuthenticationPolicy.Challenge("r=1")],
        };

        var (written, _) = await ServeAsync(
            policy,
            EmptyBusMetadata(),
            TestContext.CancellationToken,
            Request(KafkaApiKey.SaslHandshake, 0, String("SCRAM-SHA-256")),
            Bytes("n,,n=alice,r=0"));

        CollectionAssert.AreEqual(Response([0, 0, 0, 0, 0, 1, .. String("SCRAM-SHA-256")]).Concat(Bytes("r=1")).ToArray(), written);
    }

    private static UnitTestKafkaAuthenticationPolicy Policy() => new(false, "PLAIN");
}
