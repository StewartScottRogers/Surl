using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaTestExchanges;

namespace Surl.Extended.Protocol.Kafka.UnitTests;

/// <summary>
/// How a login carries a mechanism's steps (BL-518; ADR-0105 decision 5): challenges in
/// <c>auth_bytes</c>, and SCRAM's server-final with the success (RFC 5802; RFC 4422 section 5).
/// </summary>
[TestClass]
public sealed class KafkaSaslLoginTests
{
    private static readonly byte[] ScramHandshake = Request(KafkaApiKey.SaslHandshake, 1, String("SCRAM-SHA-256"));

    private static readonly byte[] HandshakeAccepted = Response([0, 0, 0, 0, 0, 1, .. String("SCRAM-SHA-256")]);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_ScramLogin_SendsServerFirstThenServerFinalWithTheSuccess()
    {
        var policy = Scram(
            UnitTestKafkaAuthenticationPolicy.Challenge("r=0s,s=c2FsdA==,i=4096"),
            UnitTestKafkaAuthenticationPolicy.Challenge("v=proof"),
            new SaslLoginStep(SaslLoginOutcome.Accepted, ReadOnlyMemory<byte>.Empty, "alice", new CheckedLogin("SCRAM-SHA-256", "alice", true)));

        var (written, log) = await ServeAsync(
            policy,
            EmptyBusMetadata(),
            TestContext.CancellationToken,
            ScramHandshake,
            Request(KafkaApiKey.SaslAuthenticate, 0, Bytes("n,,n=alice,r=0")),
            Request(KafkaApiKey.SaslAuthenticate, 0, Bytes("c=biws,r=0s,p=x")),
            Request(KafkaApiKey.Metadata, 0, [0, 0, 0, 0]));

        byte[] metadataZero = Response([0, 0, 0, 1, 0, 0, 0, 0, .. String("127.0.0.1"), 0, 0, 0x23, 0x84, 0, 0, 0, 0]);
        byte[][] expected =
        [
            HandshakeAccepted,
            Response([0, 0, 0xFF, 0xFF, .. Bytes("r=0s,s=c2FsdA==,i=4096")]),
            Response([0, 0, 0xFF, 0xFF, .. Bytes("v=proof")]),
            metadataZero,
        ];
        CollectionAssert.AreEqual(expected.SelectMany(bytes => bytes).ToArray(), written);
        CollectionAssert.AreEqual(new[] { "c=biws,r=0s,p=x", string.Empty }, policy.ScriptedResponses.Select(bytes => Encoding.ASCII.GetString(bytes)).ToArray());
        Assert.AreEqual("Login accepted: SCRAM-SHA-256 alice", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_SecondChallengeOfAMechanismOtherThanScram_IsSentAsAChallenge()
    {
        var policy = new UnitTestKafkaAuthenticationPolicy(false, "PLAIN")
        {
            ScriptedSteps = [UnitTestKafkaAuthenticationPolicy.Challenge("one"), UnitTestKafkaAuthenticationPolicy.Challenge("two")],
        };

        var (written, _) = await ServeAsync(
            policy,
            EmptyBusMetadata(),
            TestContext.CancellationToken,
            Request(KafkaApiKey.SaslHandshake, 1, String("PLAIN")),
            Request(KafkaApiKey.SaslAuthenticate, 0, Bytes("a")),
            Request(KafkaApiKey.SaslAuthenticate, 0, Bytes("b")));

        Assert.IsTrue(written.AsSpan().EndsWith(Response([0, 0, 0xFF, 0xFF, .. Bytes("two")])));
        CollectionAssert.AreEqual(new[] { "b" }, policy.ScriptedResponses.Select(bytes => Encoding.ASCII.GetString(bytes)).ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_ScramProofRefused_IsSaslAuthenticationFailed()
    {
        var policy = Scram(
            UnitTestKafkaAuthenticationPolicy.Challenge("r=0s"),
            new SaslLoginStep(SaslLoginOutcome.RefusedCredentials, ReadOnlyMemory<byte>.Empty, null, null));

        var (written, _) = await ServeAsync(
            policy,
            EmptyBusMetadata(),
            TestContext.CancellationToken,
            ScramHandshake,
            Request(KafkaApiKey.SaslAuthenticate, 0, Bytes("n,,n=alice,r=0")),
            Request(KafkaApiKey.SaslAuthenticate, 0, Bytes("c=biws,r=0s,p=x")));

        byte[][] expected =
        [
            HandshakeAccepted,
            Response([0, 0, 0xFF, 0xFF, .. Bytes("r=0s")]),
            Response([0, 58, .. String("Authentication failed"), 0, 0, 0, 0]),
        ];
        CollectionAssert.AreEqual(expected.SelectMany(bytes => bytes).ToArray(), written);
    }

    [TestMethod]
    public async Task ServeAsync_ScramServerFinalNotFollowedByAnAcceptance_IsRefused()
    {
        var policy = Scram(
            UnitTestKafkaAuthenticationPolicy.Challenge("r=0s"),
            UnitTestKafkaAuthenticationPolicy.Challenge("v=proof"),
            new SaslLoginStep(SaslLoginOutcome.RefusedCredentials, ReadOnlyMemory<byte>.Empty, null, null, "SCRAM: proof mismatch"));

        var (written, log) = await ServeAsync(
            policy,
            EmptyBusMetadata(),
            TestContext.CancellationToken,
            ScramHandshake,
            Request(KafkaApiKey.SaslAuthenticate, 0, Bytes("n,,n=alice,r=0")),
            Request(KafkaApiKey.SaslAuthenticate, 0, Bytes("c=biws,r=0s,p=x")));

        Assert.IsTrue(written.AsSpan().EndsWith(Response([0, 58, .. String("Authentication failed"), 0, 0, 0, 0])));
        Assert.AreEqual("SCRAM: proof mismatch", log.Notes[0]);
    }

    [TestMethod]
    public async Task ServeAsync_LoginAcceptedUnchecked_AuthenticatesWithNoAccount()
    {
        var policy = Scram(new SaslLoginStep(SaslLoginOutcome.AcceptedUnchecked, ReadOnlyMemory<byte>.Empty, null, null));

        var (written, log) = await ServeAsync(
            policy,
            EmptyBusMetadata(),
            TestContext.CancellationToken,
            ScramHandshake,
            Request(KafkaApiKey.SaslAuthenticate, 0, Bytes("n,,n=alice,r=0")),
            Request(KafkaApiKey.Metadata, 0, [0, 0, 0, 0]));

        Assert.HasCount(HandshakeAccepted.Length + 16 + 35, written);
        Assert.IsEmpty(log.Notes);
        Assert.AreEqual(1, policy.AnonymousChecks);
    }

    private static UnitTestKafkaAuthenticationPolicy Scram(params SaslLoginStep[] steps) =>
        new(false, "SCRAM-SHA-256") { ScriptedSteps = steps };
}
