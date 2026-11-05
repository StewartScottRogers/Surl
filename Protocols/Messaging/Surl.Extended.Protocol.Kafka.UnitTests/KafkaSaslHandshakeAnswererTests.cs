using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaTestExchanges;

namespace Surl.Extended.Protocol.Kafka.UnitTests;

/// <summary>
/// SaslHandshake v0 and v1 (BL-518; ADR-0105 decision 5), every byte written by hand from the
/// Kafka protocol guide's SaslHandshake schema.
/// </summary>
[TestClass]
public sealed class KafkaSaslHandshakeAnswererTests
{
    private static readonly byte[] Offered = [0, 0, 0, 2, .. String("PLAIN"), .. String("SCRAM-SHA-256")];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_NullPolicy_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new KafkaSaslHandshakeAnswerer(null!));

    [TestMethod]
    public async Task AnswerAsync_NullArguments_Throw()
    {
        var answerer = new KafkaSaslHandshakeAnswerer(Policy());
        var request = new KafkaRequest(new KafkaRequestHeader(17, 1, 1, null), KafkaServedApis.Ranges[4], new KafkaPrimitiveReader(Array.Empty<byte>()), new KafkaConnectionState(null));

        Assert.AreEqual(KafkaApiKey.SaslHandshake, answerer.ApiKey);
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await answerer.AnswerAsync(null!, Context(TestContext.CancellationToken)));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await answerer.AnswerAsync(request, null!));
    }

    [TestMethod]
    [DataRow((short)0)]
    [DataRow((short)1)]
    public async Task ServeAsync_HandshakeForPlain_IsAcceptedWithTheOfferedList(short version)
    {
        var (written, log) = await ServeAsync(Policy(), EmptyBusMetadata(), TestContext.CancellationToken, Request(KafkaApiKey.SaslHandshake, version, String("PLAIN")));

        CollectionAssert.AreEqual(Response([0, 0, .. Offered]), written);
        Assert.IsEmpty(log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_HandshakeForGssapi_IsUnsupportedSaslMechanismAndASecondHandshakeIsAnswered()
    {
        var (written, _) = await ServeAsync(
            Policy(),
            EmptyBusMetadata(),
            TestContext.CancellationToken,
            Request(KafkaApiKey.SaslHandshake, 1, String("GSSAPI")),
            Request(KafkaApiKey.SaslHandshake, 1, String("PLAIN")));

        CollectionAssert.AreEqual(Response([0, 33, .. Offered]).Concat(Response([0, 0, .. Offered])).ToArray(), written);
    }

    [TestMethod]
    public async Task ServeAsync_MechanismNamedInAnotherCase_IsUnsupportedSaslMechanism()
    {
        var (written, _) = await ServeAsync(Policy(), EmptyBusMetadata(), TestContext.CancellationToken, Request(KafkaApiKey.SaslHandshake, 1, String("plain")));

        CollectionAssert.AreEqual(Response([0, 33, .. Offered]), written);
    }

    [TestMethod]
    public async Task ServeAsync_PolicyOffers_AreKeptToKafkasMechanismsInKafkasOrder()
    {
        var policy = new UnitTestKafkaAuthenticationPolicy(false, "SCRAM-SHA-512", "GSSAPI", "scram-sha-1", "plain", "OAUTHBEARER");

        var (written, _) = await ServeAsync(policy, EmptyBusMetadata(), TestContext.CancellationToken, Request(KafkaApiKey.SaslHandshake, 1, String("PLAIN")));

        CollectionAssert.AreEqual(Response([0, 0, 0, 0, 0, 2, .. String("PLAIN"), .. String("SCRAM-SHA-512")]), written);
        Assert.AreEqual(new SaslOfferRequest("kafka", null), policy.OfferRequests.Single());
    }

    [TestMethod]
    public async Task ServeAsync_PolicyOffersNothing_AnswersAnEmptyList()
    {
        var (written, _) = await ServeAsync(new UnitTestKafkaAuthenticationPolicy(false), EmptyBusMetadata(), TestContext.CancellationToken, Request(KafkaApiKey.SaslHandshake, 1, String("PLAIN")));

        CollectionAssert.AreEqual(Response([0, 33, 0, 0, 0, 0]), written);
    }

    [TestMethod]
    public async Task ServeAsync_SecondHandshakeAfterAMechanismWasChosen_ClosesWithNoBytes()
    {
        var (written, log) = await ServeAsync(
            Policy(),
            EmptyBusMetadata(),
            TestContext.CancellationToken,
            Request(KafkaApiKey.SaslHandshake, 1, String("PLAIN")),
            Request(KafkaApiKey.SaslHandshake, 1, String("PLAIN")));

        CollectionAssert.AreEqual(Response([0, 0, .. Offered]), written);
        Assert.AreEqual("A SaslHandshake came after a mechanism was chosen or the login ended; closed with no reply.", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_HandshakeAfterTheLogin_ClosesWithNoBytes()
    {
        var (written, log) = await ServeAsync(
            Policy(),
            EmptyBusMetadata(),
            TestContext.CancellationToken,
            Request(KafkaApiKey.SaslHandshake, 1, String("PLAIN")),
            Request(KafkaApiKey.SaslAuthenticate, 0, Bytes("\0alice\0secret")),
            Request(KafkaApiKey.SaslHandshake, 1, String("PLAIN")));

        CollectionAssert.AreEqual(Response([0, 0, .. Offered]).Concat(Response([0, 0, 0xFF, 0xFF, 0, 0, 0, 0])).ToArray(), written);
        Assert.AreEqual("A SaslHandshake came after a mechanism was chosen or the login ended; closed with no reply.", log.Notes.Last());
    }

    private static UnitTestKafkaAuthenticationPolicy Policy() => new(false, "PLAIN", "SCRAM-SHA-256");
}
