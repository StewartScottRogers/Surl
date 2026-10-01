using System.Formats.Asn1;
using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ldap.LdapRequestBytes;
using static Surl.Protocol.Ldap.LdapServerExchange;
using static Surl.Protocol.Ldap.UnitTestSaslAuthenticationPolicy;

namespace Surl.Protocol.Ldap;

/// <summary>
/// ADR-0072 decision 4 in the server: the SASL and Sicily binds the pinned Windows build sends,
/// replayed from its recordings through a fake SASL policy, the multi-step bind, the refusals, and
/// the security layer's framing.
/// </summary>
[TestClass]
public sealed class LdapProtocolServerSaslBindTests
{
    // The CHALLENGE_MESSAGE the recordings were answered with (Fixtures/README.md).
    private static readonly byte[] NtlmChallenge = Hex(
        "4E544C4D53535000020000000800080030000000 35828AE0 0123456789ABCDEF 0000000000000000"
        + " 1C001C0038000000 5300550052004C00 020008005300550052004C00 010008005300550052004C00 00000000");

    // WinLDAP's NEGOTIATE_MESSAGE in every NTLM recording.
    private static readonly byte[] NtlmNegotiate = Hex("4E544C4D5353500001000000B78208E2000000000000000000000000000000000A00F4650000000F");

    private static readonly byte[] DigestChallenge = Encoding.ASCII.GetBytes(
        "realm=\"surl\",nonce=\"MDEyMzQ1Njc4OWFiY2RlZg==\",qop=\"auth,auth-int,auth-conf\",cipher=\"3des,rc4\",maxbuf=65536,charset=utf-8,algorithm=md5-sess");

    private static readonly byte[] DigestRspAuth = "rspauth=317c080c54526d1d62d9f392f87cf69a"u8.ToArray();

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_RecordedSicilyNtlmBind_RunsTheNtlmExchangeAndSealsWhatFollows()
    {
        var messages = RecordedFixture.ReadRequestMessages("ldap-ntlm-sealed");
        var layer = new UnitTestSecurityLayer();
        var sasl = new UnitTestSaslAuthenticationPolicy(_ => [Challenge(NtlmChallenge), Accepted("alice", layer)], _ => ["GSS-SPNEGO", "NTLM"]);
        var log = new RecordingExchangeLog();

        var connection = await ServeAsync(sasl, log, messages.Take(4));

        var start = sasl.Starts.Single();
        Assert.AreEqual("NTLM", start.Mechanism);
        CollectionAssert.AreEqual(NtlmNegotiate, start.InitialResponse!.Value.ToArray());
        Assert.IsTrue(start.CanCarrySecurityLayer);
        CollectionAssert.AreEqual(SicilyTokenOf(messages[2]), sasl.Responses.Single());
        var written = LdapResponseTranscript.Encodings(connection.WrittenBytes);
        Assert.HasCount(4, written);
        CollectionAssert.AreEqual(BindResponse(2, LdapResultCode.Success, matchedDn: NtlmChallenge), written[2]);
        CollectionAssert.AreEqual(BindResponse(3, LdapResultCode.Success), written[3]);
        Assert.HasCount(0x54, layer.BuffersFromTheClient.Single());
        CollectionAssert.AreEqual(
            new[] { "Login accepted: UNIT alice", "LDAP security layer: NTLM", "A security-layer buffer failed its check; closed with no reply." },
            log.Notes.Where(note => !note.StartsWith("LDAP search", StringComparison.Ordinal)).ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_RecordedGssSpnegoBind_IsSaslBindInProgressThenSuccessAndSealsWhatFollows()
    {
        var messages = RecordedFixture.ReadRequestMessages("ldap-negotiate-sealed");
        var layer = new UnitTestSecurityLayer();
        var sasl = new UnitTestSaslAuthenticationPolicy(_ => [Challenge(NtlmChallenge), Accepted("alice", layer)], _ => ["GSS-SPNEGO", "NTLM"]);

        var connection = await ServeAsync(sasl, null, messages.Take(5));

        var start = sasl.Starts.Single();
        Assert.AreEqual("GSS-SPNEGO", start.Mechanism);
        CollectionAssert.AreEqual(NtlmNegotiate, start.InitialResponse!.Value.ToArray());
        CollectionAssert.AreEqual(SaslCredentialsOf(messages[3]), sasl.Responses.Single());
        var written = LdapResponseTranscript.Encodings(connection.WrittenBytes);
        Assert.AreEqual("#2 searchResultEntry : supportedSASLMechanisms=GSS-SPNEGO,NTLM", LdapResponseTranscript.Of(written[2]).Single());
        CollectionAssert.AreEqual(BindResponse(3, LdapResultCode.SaslBindInProgress, serverSaslCredentials: NtlmChallenge), written[4]);
        CollectionAssert.AreEqual(BindResponse(4, LdapResultCode.Success), written[5]);
        Assert.HasCount(0x54, layer.BuffersFromTheClient.Single());
    }

    [TestMethod]
    public async Task ServeAsync_RecordedDigestMd5Bind_StartsWithEmptyCredentialsAndSucceedsWithRspAuth()
    {
        var messages = RecordedFixture.ReadRequestMessages("ldap-digest-md5");
        var sasl = new UnitTestSaslAuthenticationPolicy(
            _ => [Challenge(DigestChallenge), Accepted("alice", new UnitTestSecurityLayer(), DigestRspAuth)], _ => ["DIGEST-MD5"]);
        var log = new RecordingExchangeLog();

        var connection = await ServeAsync(sasl, log, messages.Take(4));

        var start = sasl.Starts.Single();
        Assert.AreEqual("DIGEST-MD5", start.Mechanism);
        Assert.IsTrue(start.InitialResponse.HasValue);
        Assert.IsTrue(start.InitialResponse.Value.IsEmpty);
        StringAssert.StartsWith(Encoding.ASCII.GetString(sasl.Responses.Single()), "username=\"alice\",realm=\"\",nonce=\"MDEyMzQ1Njc4OWFiY2RlZg==\"");
        var written = LdapResponseTranscript.Encodings(connection.WrittenBytes);
        CollectionAssert.AreEqual(BindResponse(3, LdapResultCode.SaslBindInProgress, serverSaslCredentials: DigestChallenge), written[4]);
        CollectionAssert.AreEqual(BindResponse(4, LdapResultCode.Success, serverSaslCredentials: DigestRspAuth), written[5]);
        CollectionAssert.AreEqual(new[] { "Login accepted: UNIT alice", "LDAP security layer: DIGEST-MD5" }, log.Notes.Where(note => !note.StartsWith("LDAP search", StringComparison.Ordinal)).ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_MultiStepBind_ContinuesTheExchangeWhileTheMechanismIsTheSame()
    {
        var sasl = new UnitTestSaslAuthenticationPolicy(_ => [Challenge("one"u8.ToArray()), Challenge("two"u8.ToArray()), Accepted("alice")]);

        var connection = await ServeAsync(
            sasl,
            null,
            [SaslBind(1, "X-UNIT", "first"u8.ToArray()), SaslBind(2, "x-unit", "second"u8.ToArray()), SaslBind(3, "X-UNIT", null), SearchBase(4)]);

        CollectionAssert.AreEqual(
            new[]
            {
                "#1 bindResponse saslBindInProgress [7] one",
                "#2 bindResponse saslBindInProgress [7] two",
                "#3 bindResponse success",
                "#4 searchResultEntry dc=example,dc=com: objectClass=domain; dc=example",
                "#4 searchResultDone success",
            },
            LdapResponseTranscript.Of(connection.WrittenBytes).ToArray());
        Assert.AreEqual("first", Encoding.ASCII.GetString(sasl.Starts.Single().InitialResponse!.Value.Span));
        CollectionAssert.AreEqual(new[] { "second", string.Empty }, sasl.Responses.Select(response => Encoding.ASCII.GetString(response)).ToArray());
    }

    [TestMethod]
    [DataRow((int)SaslLoginOutcome.RefusedCredentials, "#1 bindResponse invalidCredentials", DisplayName = "Refused credentials")]
    [DataRow((int)SaslLoginOutcome.RefusedPlaintext, "#1 bindResponse confidentialityRequired \"SASL mechanism needs TLS or --allow-plaintext-auth\"", DisplayName = "Plain text without TLS")]
    [DataRow((int)SaslLoginOutcome.RefusedMechanism, "#1 bindResponse authMethodNotSupported \"authentication method not accepted\"", DisplayName = "Mechanism refused")]
    public async Task ServeAsync_RefusedSaslBind_IsAnsweredWithItsCodeAndLeavesTheConnectionAnonymous(int outcome, string expected)
    {
        var sasl = new UnitTestSaslAuthenticationPolicy(_ => [Refused((SaslLoginOutcome)outcome)]);

        var connection = await ServeAsync(sasl, null, [SaslBind(1, "PLAIN", "\0alice\0secret"u8.ToArray()), SearchBase(2)]);

        CollectionAssert.AreEqual(
            new[] { expected, "#2 searchResultDone insufficientAccessRights \"bind first\"" },
            LdapResponseTranscript.Of(connection.WrittenBytes).ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_RefusedCredentials_NotesTheCheckedLoginThenTheReason()
    {
        var refused = new SaslLoginStep(
            SaslLoginOutcome.RefusedCredentials, ReadOnlyMemory<byte>.Empty, null, new CheckedLogin("NTLM", "alice", false), "the security layer needs the account's password");
        var log = new RecordingExchangeLog();

        await ServeAsync(new UnitTestSaslAuthenticationPolicy(_ => [refused]), log, [SaslBind(1, "NTLM", NtlmNegotiate)]);

        CollectionAssert.AreEqual(new[] { "Login refused: NTLM alice", "the security layer needs the account's password" }, log.Notes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_UnknownMechanism_IsNotedAsARefusedBind()
    {
        var log = new RecordingExchangeLog();

        await ServeAsync(new UnitTestSaslAuthenticationPolicy(), log, [SaslBind(1, "X-NONE", null)]);

        Assert.AreEqual("LDAP bind refused: authMethodNotSupported: authentication method not accepted", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_AcceptedUncheckedSaslBind_IsSuccessWithNoLoginNote()
    {
        var acceptedUnchecked = new SaslLoginStep(SaslLoginOutcome.AcceptedUnchecked, ReadOnlyMemory<byte>.Empty, null, null);
        var log = new RecordingExchangeLog();

        var connection = await ServeAsync(new UnitTestSaslAuthenticationPolicy(_ => [acceptedUnchecked]), log, [SaslBind(1, "ANONYMOUS", null), SearchBase(2)]);

        Assert.AreEqual("#2 searchResultDone success", LdapResponseTranscript.Of(connection.WrittenBytes)[^1]);
        Assert.IsEmpty(log.Notes.Where(note => note.StartsWith("Login", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow(true, "#1 bindResponse success matched NTLM", DisplayName = "NTLM offered")]
    [DataRow(false, "#1 bindResponse authMethodNotSupported \"authentication method not accepted\"", DisplayName = "NTLM not offered")]
    public async Task ServeAsync_SicilyPackageDiscovery_NamesNtlmWhenThePolicyOffersIt(bool isNtlmOffered, string expected)
    {
        var sasl = new UnitTestSaslAuthenticationPolicy(offer: _ => isNtlmOffered ? ["GSS-SPNEGO", "ntlm"] : ["GSS-SPNEGO"]);

        var connection = await ServeAsync(sasl, null, [SicilyBind(1, LdapSicilyChoice.PackageDiscovery, [])]);

        Assert.AreEqual(expected, LdapResponseTranscript.Of(connection.WrittenBytes).Single());
        Assert.IsEmpty(sasl.Starts);
    }

    [TestMethod]
    public async Task ServeAsync_SicilyResponseWithNoNegotiate_IsProtocolError()
    {
        var log = new RecordingExchangeLog();

        var connection = await ServeAsync(new UnitTestSaslAuthenticationPolicy(), log, [SicilyBind(1, LdapSicilyChoice.Response, [1, 2, 3])]);

        Assert.AreEqual("#1 bindResponse protocolError \"sicilyResponse without sicilyNegotiate\"", LdapResponseTranscript.Of(connection.WrittenBytes).Single());
        Assert.AreEqual("LDAP bind refused: protocolError: sicilyResponse without sicilyNegotiate", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_SimpleBindBetweenSicilyNegotiateAndResponse_AbandonsTheExchange()
    {
        var sasl = new UnitTestSaslAuthenticationPolicy(_ => [Challenge(NtlmChallenge), Accepted("alice")]);

        var connection = await ServeAsync(
            sasl, null, [SicilyBind(1, LdapSicilyChoice.Negotiate, NtlmNegotiate), Message(2, SimpleBind(3, "alice", "secret")), SicilyBind(3, LdapSicilyChoice.Response, [1])]);

        Assert.AreEqual("#3 bindResponse protocolError \"sicilyResponse without sicilyNegotiate\"", LdapResponseTranscript.Of(connection.WrittenBytes)[^1]);
        Assert.IsEmpty(sasl.Responses);
    }

    [TestMethod]
    public async Task ServeAsync_SaslBindInProgressThenSicilyResponse_IsProtocolError()
    {
        var sasl = new UnitTestSaslAuthenticationPolicy(_ => [Challenge(NtlmChallenge), Accepted("alice")]);

        var connection = await ServeAsync(sasl, null, [SaslBind(1, "NTLM", NtlmNegotiate), SicilyBind(2, LdapSicilyChoice.Response, [1])]);

        Assert.AreEqual("#2 bindResponse protocolError \"sicilyResponse without sicilyNegotiate\"", LdapResponseTranscript.Of(connection.WrittenBytes)[^1]);
    }

    [TestMethod]
    public async Task ServeAsync_SicilyInProgressThenASaslBind_StartsANewExchange()
    {
        var sasl = new UnitTestSaslAuthenticationPolicy(_ => [Challenge(NtlmChallenge), Accepted("alice")]);

        await ServeAsync(sasl, null, [SicilyBind(1, LdapSicilyChoice.Negotiate, NtlmNegotiate), SaslBind(2, "NTLM", NtlmNegotiate)]);

        Assert.HasCount(2, sasl.Starts);
        Assert.IsEmpty(sasl.Responses);
    }

    [TestMethod]
    [DataRow("X-OTHER", 3, DisplayName = "Another mechanism")]
    [DataRow("X-UNIT", 1, DisplayName = "A bind of LDAP version 1")]
    public async Task ServeAsync_BindThatDoesNotContinueTheExchange_AbandonsIt(string mechanism, int version)
    {
        var sasl = new UnitTestSaslAuthenticationPolicy(_ => [Challenge("one"u8.ToArray()), Accepted("alice")]);

        await ServeAsync(sasl, null, [SaslBind(1, "X-UNIT", null), SaslBind(2, mechanism, null, version), SaslBind(3, "X-UNIT", null)]);

        Assert.AreEqual(version == 3 ? 3 : 2, sasl.Starts.Count);
        Assert.IsEmpty(sasl.Responses);
    }

    [TestMethod]
    public async Task ServeAsync_InsideTheSecurityLayer_EveryMessageIsOneProtectedBufferBothWays()
    {
        var layer = new UnitTestSecurityLayer();
        var sasl = new UnitTestSaslAuthenticationPolicy(_ => [Accepted("alice", layer)]);
        var bind = SaslBind(1, "X-UNIT", null);

        var connection = await ServeAsync(
            sasl, null, [bind, UnitTestSecurityLayer.ClientBuffer(SearchBase(2)), UnitTestSecurityLayer.ClientBuffer(Message(3, SimpleBind(3, "alice", "secret")))]);

        var bindResponse = LdapResponseTranscript.Encodings(connection.WrittenBytes[..BindResponse(1, LdapResultCode.Success).Length]).Single();
        CollectionAssert.AreEqual(BindResponse(1, LdapResultCode.Success), bindResponse);
        CollectionAssert.AreEqual(
            new[]
            {
                "#2 searchResultEntry dc=example,dc=com: objectClass=domain; dc=example",
                "#2 searchResultDone success",
                "#3 bindResponse success",
            },
            LdapResponseTranscript.Of(UnitTestSecurityLayer.ServerMessages(connection.WrittenBytes, bindResponse.Length)).ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_MalformedMessageInsideTheSecurityLayer_SendsAProtectedNoticeOfDisconnection()
    {
        var sasl = new UnitTestSaslAuthenticationPolicy(_ => [Accepted("alice", new UnitTestSecurityLayer())]);

        var connection = await ServeAsync(sasl, null, [SaslBind(1, "X-UNIT", null), UnitTestSecurityLayer.ClientBuffer([0x31, 0x00])]);

        var bindLength = BindResponse(1, LdapResultCode.Success).Length;
        Assert.AreEqual(
            "#0 extendedResponse protocolError \"an unexpected tag\" [10] 1.3.6.1.4.1.1466.20036",
            LdapResponseTranscript.Of(UnitTestSecurityLayer.ServerMessages(connection.WrittenBytes, bindLength)).Single());
    }

    [TestMethod]
    [DataRow(10, 0, DisplayName = "Past the layer's maximum")]
    [DataRow(65536, 40, DisplayName = "Past --max-message")]
    public async Task ServeAsync_SecurityLayerBufferTooLarge_ClosesWithNoReply(int maximumProtectedBytes, int maxMessageBytes)
    {
        var layer = new UnitTestSecurityLayer(maximumProtectedBytes);
        var sasl = new UnitTestSaslAuthenticationPolicy(_ => [Accepted("alice", layer)]);
        var log = new RecordingExchangeLog();
        var limits = ExchangeLimits.Default with { MaxMessageBytes = maxMessageBytes };
        var buffer = UnitTestSecurityLayer.Framed(new byte[41]);

        var connection = await ServeAsync(sasl, log, [SaslBind(1, "X-UNIT", null), buffer], limits);

        Assert.AreEqual("#1 bindResponse success", LdapResponseTranscript.Of(connection.WrittenBytes).Single());
        Assert.IsEmpty(layer.BuffersFromTheClient);
        Assert.AreEqual(
            "A security-layer buffer of 41 bytes is past what the layer or --max-message allows; closed with no reply.",
            log.Notes[^1]);
    }

    [TestMethod]
    public async Task ServeAsync_ConnectionClosedInsideASecurityLayerBuffer_NotesIt()
    {
        var log = new RecordingExchangeLog();
        var sasl = new UnitTestSaslAuthenticationPolicy(_ => [Accepted("alice", new UnitTestSecurityLayer())]);

        var connection = await ServeAsync(sasl, log, [SaslBind(1, "X-UNIT", null), [0, 0]]);

        Assert.AreEqual("#1 bindResponse success", LdapResponseTranscript.Of(connection.WrittenBytes).Single());
        Assert.AreEqual("The client closed the connection part way through a message.", log.Notes[^1]);
    }

    [TestMethod]
    public async Task ServeAsync_LaterBindWithAnotherLayer_ReplacesTheFirstAfterItsResponse()
    {
        var first = new UnitTestSecurityLayer();
        var second = new UnitTestSecurityLayer();
        var layers = new Queue<UnitTestSecurityLayer>([first, second]);
        var sasl = new UnitTestSaslAuthenticationPolicy(_ => [Accepted("alice", layers.Dequeue())]);

        await ServeAsync(
            sasl,
            null,
            [SaslBind(1, "X-UNIT", null), UnitTestSecurityLayer.ClientBuffer(SaslBind(2, "X-UNIT", null)), UnitTestSecurityLayer.ClientBuffer(SearchBase(3))]);

        Assert.HasCount(1, first.BuffersFromTheClient);
        Assert.HasCount(1, second.BuffersFromTheClient);
    }

    private async Task<InMemoryConnection> ServeAsync(
        UnitTestSaslAuthenticationPolicy sasl, RecordingExchangeLog? log, IEnumerable<byte[]> messages, ExchangeLimits? limits = null)
    {
        var connection = new InMemoryConnection(messages.Select(message => new ReadOnlyMemory<byte>(message)));
        await Server(new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted), sasl)
            .ServeAsync(connection, Context(TestContext.CancellationToken, log: log, limits: limits));

        return connection;
    }

    private static byte[] SearchBase(int messageId) => Message(messageId, Search(Present("objectClass"), scope: 0));

    private static byte[] SaslBind(int messageId, string mechanism, byte[]? credentials, int version = 3) => Message(messageId, writer =>
    {
        using var bind = writer.PushSequence(LdapTags.BindRequest);
        writer.WriteInteger(version);
        Text(writer, string.Empty);
        using var sasl = writer.PushSequence(LdapTags.Context(3, isConstructed: true));
        Text(writer, mechanism);
        if (credentials is not null)
        {
            writer.WriteOctetString(credentials);
        }
    });

    private static byte[] SicilyBind(int messageId, LdapSicilyChoice choice, byte[] token) => Message(messageId, writer =>
    {
        using var bind = writer.PushSequence(LdapTags.BindRequest);
        writer.WriteInteger(3);
        Text(writer, choice == LdapSicilyChoice.Negotiate ? "NTLM" : string.Empty);
        writer.WriteOctetString(token, LdapTags.Context((int)choice));
    });

    // A BindResponse straight from RFC 4511 appendix B and MS-ADTS 5.1.1.1.3, never from the server's encoder.
    private static byte[] BindResponse(int messageId, LdapResultCode code, byte[]? matchedDn = null, byte[]? serverSaslCredentials = null)
    {
        var writer = new AsnWriter(AsnEncodingRules.BER);
        using (writer.PushSequence())
        {
            writer.WriteInteger(messageId);
            using var response = writer.PushSequence(new Asn1Tag(TagClass.Application, 1, isConstructed: true));
            writer.WriteEnumeratedValue(code);
            writer.WriteOctetString(matchedDn ?? []);
            writer.WriteOctetString([]);
            if (serverSaslCredentials is not null)
            {
                writer.WriteOctetString(serverSaslCredentials, new Asn1Tag(TagClass.ContextSpecific, 7));
            }
        }

        return writer.Encode();
    }

    // The [11] token of a recorded Sicily bind, read with AsnReader.
    private static byte[] SicilyTokenOf(byte[] message)
    {
        var bind = BindOf(message);
        bind.ReadInteger();
        bind.ReadOctetString();
        return bind.ReadOctetString(new Asn1Tag(TagClass.ContextSpecific, 11));
    }

    // The credentials of a recorded SASL bind, read with AsnReader.
    private static byte[] SaslCredentialsOf(byte[] message)
    {
        var bind = BindOf(message);
        bind.ReadInteger();
        bind.ReadOctetString();
        var sasl = bind.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 3, isConstructed: true));
        sasl.ReadOctetString();
        return sasl.ReadOctetString();
    }

    private static AsnReader BindOf(byte[] message)
    {
        var sequence = new AsnReader(message, AsnEncodingRules.BER).ReadSequence();
        sequence.ReadInteger();
        return sequence.ReadSequence(new Asn1Tag(TagClass.Application, 0, isConstructed: true));
    }
}
