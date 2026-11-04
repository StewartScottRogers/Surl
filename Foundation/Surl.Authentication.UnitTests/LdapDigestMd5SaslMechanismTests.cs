using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Surl.Cryptography.Rc4;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// LDAP's SASL <c>DIGEST-MD5</c> bind, started with <see cref="SaslExchangeStart.CanCarrySecurityLayer"/>
/// (ADR-0072, decision 4): the recording <c>Fixtures/ldap-digest-md5</c> of pinned upstream curl
/// 8.21.0's <c>WinLDAP</c> answering Surl's LDAP challenge for <c>alice:secret</c>, the
/// <c>auth-int</c> and <c>auth-conf</c> layers checked against RFC 2831 sections 2.3 and 2.4's
/// layout computed here independently, and the refusals around them.
/// </summary>
[TestClass]
public sealed class LdapDigestMd5SaslMechanismTests
{
    private const string LdapChallenge =
        "realm=\"surl\",nonce=\"MDEyMzQ1Njc4OWFiY2RlZg==\",qop=\"auth,auth-int,auth-conf\",cipher=\"3des,rc4\",maxbuf=65536,charset=utf-8,algorithm=md5-sess";

    private const string MailChallenge =
        "realm=\"surl\",nonce=\"MDEyMzQ1Njc4OWFiY2RlZg==\",qop=\"auth\",charset=utf-8,algorithm=md5-sess";

    private const string RecordedResponse =
        "username=\"alice\",realm=\"\",nonce=\"MDEyMzQ1Njc4OWFiY2RlZg==\",digest-uri=\"ldap/127.0.0.1\",cnonce=\"8aa82d051ad9706a2cbc26aceeb8f21a\",nc=00000001,response=a5161e5d54e3950512ceb4bd61f5a564,qop=auth-conf,cipher=3des,charset=utf-8";

    // Computed for the recorded response with PowerShell's MD5 alone when it was recorded.
    private const string RecordedResponseAuth = "rspauth=317c080c54526d1d62d9f392f87cf69a";

    private const string RecordedSessionKey = "68d9795fbde795dac0fdfdbd4461a106";

    private const string ClientToServer = "client-to-server";

    private const string ServerToClient = "server-to-client";

    // The base search of ADR-0072's simple-bind transcript.
    private static readonly byte[] BaseSearch = Convert.FromHexString(
        "30840000003E020102638400000035041164633D6578616D706C652C64633D636F6D0A01000A0100020100020100010100870B4F626A656374436C617373308400000000");

    private static readonly HashSet<AuthenticationMethod> EveryMethod = [.. Enum.GetValues<AuthenticationMethod>()];

    private readonly ManualTimeProvider clock = new();

    private AuthenticationPolicy Policy(bool allowAnonymous = false) =>
        PolicyFixture.CreateWithFixedNonces(PolicyFixture.AliceAndToken, clock, allowAnonymous, EveryMethod);

    private static ISaslExchange Start(AuthenticationPolicy policy, ReadOnlyMemory<byte>? initialResponse = null, bool canCarrySecurityLayer = true) =>
        policy.StartSaslExchange(new SaslExchangeStart("ldap", "DIGEST-MD5", initialResponse, null, canCarrySecurityLayer));

    private async Task<SaslLoginStep> LoginAsync(ISaslExchange exchange, string response)
    {
        var challenge = await exchange.BeginAsync(CancellationToken.None);
        Assert.AreEqual(SaslLoginOutcome.Challenge, challenge.Outcome);

        var pending = exchange.ContinueAsync(Encoding.Latin1.GetBytes(response), CancellationToken.None).AsTask();
        clock.Advance(AuthenticationPolicy.RefusalDelay);

        return await pending;
    }

    // A response from user with password to the fixed nonce, choosing qop and cipher.
    private static (string Text, DigestMd5Response Response) ClientResponse(
        string qop, string? cipher, string user = "alice", string password = "secret")
    {
        var unsigned = new DigestMd5Response(
            user, string.Empty, "MDEyMzQ1Njc4OWFiY2RlZg==", "8aa82d051ad9706a2cbc26aceeb8f21a", "00000001", qop, "ldap/127.0.0.1", string.Empty, null, cipher);
        var digest = Convert.ToHexStringLower(DigestMd5Calculation.ComputeResponse(unsigned, Encoding.UTF8.GetBytes(password)));
        var cipherDirective = cipher is null ? string.Empty : $",cipher={cipher}";
        var text = $"username=\"{user}\",realm=\"\",nonce=\"MDEyMzQ1Njc4OWFiY2RlZg==\",digest-uri=\"ldap/127.0.0.1\",cnonce=\"8aa82d051ad9706a2cbc26aceeb8f21a\",nc=00000001,response={digest},qop={qop}{cipherDirective},charset=utf-8";

        return (text, DigestMd5Response.Read(text)!);
    }

    private static ISaslSecurityLayer ClientLayer(DigestMd5Response response) =>
        DigestMd5SecurityLayer.ForInitiator(DigestMd5Calculation.ComputeSessionKey(response, "secret"u8), response)!;

    private static List<byte[]> RecordedCredentials() =>
        [.. RecordedFixture.ReadText("ldap-digest-md5", "transcript.txt")
            .Split("\r\n")
            .Where(line => line.Contains("sasl DIGEST-MD5 credentials ", StringComparison.Ordinal))
            .Select(line => Convert.FromHexString(line[(line.IndexOf("credentials ", StringComparison.Ordinal) + "credentials ".Length)..]))];

    // ---- RFC 2831 sections 2.3 and 2.4, computed here without DigestMd5SecurityLayer ----

    private static byte[] Md5(byte[] sessionKey, string magic) => MD5.HashData([.. sessionKey, .. Encoding.ASCII.GetBytes(magic)]);

    private static byte[] IntegrityKey(byte[] sessionKey, string mode) => Md5(sessionKey, $"Digest session key to {mode} signing key magic constant");

    private static byte[] ConfidentialityKey(byte[] sessionKey, string mode) => Md5(sessionKey, $"Digest H(A1) to {mode} sealing key magic constant");

    private static byte[] Mac(byte[] integrityKey, uint sequenceNumber, byte[] message)
    {
        var sequence = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(sequence, sequenceNumber);

        byte[] signed = [.. sequence, .. message];

        return HMACMD5.HashData(integrityKey, signed)[..10];
    }

    private static byte[] Trailer(uint sequenceNumber, ushort messageType = 1)
    {
        var trailer = new byte[6];
        BinaryPrimitives.WriteUInt16BigEndian(trailer, messageType);
        BinaryPrimitives.WriteUInt32BigEndian(trailer.AsSpan(2), sequenceNumber);

        return trailer;
    }

    // Each 7-bit group of the 56-bit key half, high bits first, in the top of its own byte.
    private static byte[] DesKey(ReadOnlySpan<byte> half)
    {
        ulong bits = 0;
        foreach (var value in half)
        {
            bits = (bits << 8) | value;
        }

        return [.. Enumerable.Range(0, 8).Select(index => (byte)(((bits >> (49 - (7 * index))) & 0x7F) << 1))];
    }

    // The first 3des body a direction sends: plain encrypted with its key, IV the key's last 8 bytes.
    private static byte[] TripleDesFirstBody(byte[] confidentialityKey, byte[] plain)
    {
        using var cipher = TripleDES.Create();
        var first = DesKey(confidentialityKey.AsSpan(0, 7));
        cipher.Key = [.. first, .. DesKey(confidentialityKey.AsSpan(7, 7)), .. first];

        return cipher.EncryptCbc(plain, confidentialityKey[8..], PaddingMode.None);
    }

    private static byte[] Padding(int messageLength)
    {
        var count = 8 - ((messageLength + 10) % 8);

        return [.. Enumerable.Repeat((byte)count, count)];
    }

    [TestMethod]
    public async Task WinLdapsRecordedBind_GetsTheLdapChallengeThenIsAcceptedWithRspauthAndATripleDesLayer()
    {
        var credentials = RecordedCredentials();
        var exchange = Start(Policy(), credentials[0]);

        var challenge = await exchange.BeginAsync(CancellationToken.None);
        var step = await exchange.ContinueAsync(credentials[1], CancellationToken.None);

        Assert.IsEmpty(credentials[0]);
        Assert.AreEqual(RecordedResponse, Encoding.ASCII.GetString(credentials[1]));
        Assert.AreEqual(SaslLoginOutcome.Challenge, challenge.Outcome);
        Assert.AreEqual(LdapChallenge, Encoding.ASCII.GetString(challenge.Challenge.Span));
        Assert.AreEqual(SaslLoginOutcome.Accepted, step.Outcome);
        Assert.AreEqual("alice", step.AccountName);
        Assert.AreEqual("Login accepted: DIGEST-MD5 alice", step.CheckedLogin?.Note);
        Assert.IsTrue(step.Challenge.IsEmpty);
        Assert.AreEqual(RecordedResponseAuth, Encoding.ASCII.GetString(step.AdditionalSuccessData.Span));
        Assert.AreEqual(DigestMd5SecurityLayer.OfferedMaximumBuffer, step.SecurityLayer!.MaximumProtectedBytes);
    }

    [TestMethod]
    public void RecordedResponse_HasTheSessionKeyAndResponseAuthComputedOutsideSurl()
    {
        var response = DigestMd5Response.Read(RecordedResponse)!;

        Assert.AreEqual(DigestMd5Response.Confidentiality, response.Qop);
        Assert.AreEqual(DigestMd5Response.TripleDesCipher, response.Cipher);
        Assert.AreEqual(RecordedSessionKey, Convert.ToHexStringLower(DigestMd5Calculation.ComputeSessionKey(response, "secret"u8)));
        Assert.AreEqual("a5161e5d54e3950512ceb4bd61f5a564", Convert.ToHexStringLower(DigestMd5Calculation.ComputeResponse(response, "secret"u8)));
        Assert.AreEqual(RecordedResponseAuth, "rspauth=" + Convert.ToHexStringLower(DigestMd5Calculation.ComputeResponseAuth(response, "secret"u8)));
    }

    [TestMethod]
    public async Task RecordedLogin_TripleDesLayer_IsRfc2831sLayoutBothDirections()
    {
        var credentials = RecordedCredentials();
        var exchange = Start(Policy(), credentials[0]);
        await exchange.BeginAsync(CancellationToken.None);
        var layer = (await exchange.ContinueAsync(credentials[1], CancellationToken.None)).SecurityLayer!;
        var sessionKey = Convert.FromHexString(RecordedSessionKey);

        byte[] toClientPlain = [.. BaseSearch, .. Padding(BaseSearch.Length), .. Mac(IntegrityKey(sessionKey, ServerToClient), 0, BaseSearch)];
        byte[] expectedToClient = [.. TripleDesFirstBody(ConfidentialityKey(sessionKey, ServerToClient), toClientPlain), .. Trailer(0)];
        byte[] fromClientPlain = [.. BaseSearch, .. Padding(BaseSearch.Length), .. Mac(IntegrityKey(sessionKey, ClientToServer), 0, BaseSearch)];
        byte[] fromClient = [.. TripleDesFirstBody(ConfidentialityKey(sessionKey, ClientToServer), fromClientPlain), .. Trailer(0)];

        CollectionAssert.AreEqual(expectedToClient, layer.Protect(BaseSearch));
        Assert.IsTrue(layer.TryUnprotect(fromClient, out var message));
        CollectionAssert.AreEqual(BaseSearch, message);
    }

    [TestMethod]
    public async Task Rc4AndIntegrityLayers_AreRfc2831sLayoutBothDirections()
    {
        foreach (var (qop, cipher) in new[] { ("auth-conf", "rc4"), ("auth-int", (string?)null) })
        {
            var (text, response) = ClientResponse(qop, cipher);
            var layer = (await LoginAsync(Start(Policy()), text)).SecurityLayer!;
            var sessionKey = DigestMd5Calculation.ComputeSessionKey(response, "secret"u8);
            byte[] toClient = [.. BaseSearch, .. Mac(IntegrityKey(sessionKey, ServerToClient), 0, BaseSearch)];
            byte[] fromClient = [.. BaseSearch, .. Mac(IntegrityKey(sessionKey, ClientToServer), 0, BaseSearch)];
            if (cipher is not null)
            {
                new Rc4(ConfidentialityKey(sessionKey, ServerToClient), 0).ApplyKeyStream(toClient, toClient);
                new Rc4(ConfidentialityKey(sessionKey, ClientToServer), 0).ApplyKeyStream(fromClient, fromClient);
            }

            CollectionAssert.AreEqual((byte[])[.. toClient, .. Trailer(0)], layer.Protect(BaseSearch), qop);
            Assert.IsTrue(layer.TryUnprotect([.. fromClient, .. Trailer(0)], out var message), qop);
            CollectionAssert.AreEqual(BaseSearch, message, qop);
        }
    }

    [TestMethod]
    [DataRow("auth-conf", "3des")]
    [DataRow("auth-conf", "rc4")]
    [DataRow("auth-int", null)]
    public async Task Layer_ServerAndClient_RoundTripSeveralMessagesBothDirections(string qop, string? cipher)
    {
        var (text, response) = ClientResponse(qop, cipher);
        var server = (await LoginAsync(Start(Policy()), text)).SecurityLayer!;
        var client = ClientLayer(response);

        foreach (var length in new[] { 0, 6, 7, 13, BaseSearch.Length, 300 })
        {
            var message = RandomNumberGenerator.GetBytes(length);
            Assert.IsTrue(server.TryUnprotect(client.Protect(message), out var atServer));
            CollectionAssert.AreEqual(message, atServer);
            Assert.IsTrue(client.TryUnprotect(server.Protect(message), out var atClient));
            CollectionAssert.AreEqual(message, atClient);
        }
    }

    [TestMethod]
    [DataRow("auth-conf", "3des")]
    [DataRow("auth-conf", "rc4")]
    [DataRow("auth-int", null)]
    public async Task Layer_BadMacWrongSequenceWrongTypeOrShortBuffer_IsRefused(string qop, string? cipher)
    {
        var (text, response) = ClientResponse(qop, cipher);

        async Task<(ISaslSecurityLayer Server, ISaslSecurityLayer Client)> LayersAsync() =>
            ((await LoginAsync(Start(Policy()), text)).SecurityLayer!, ClientLayer(response));

        var (server, client) = await LayersAsync();
        var tampered = client.Protect(BaseSearch);
        tampered[^7] ^= 1;
        Assert.IsFalse(server.TryUnprotect(tampered, out var message));
        Assert.IsEmpty(message);

        (server, client) = await LayersAsync();
        client.Protect(BaseSearch);
        Assert.IsFalse(server.TryUnprotect(client.Protect(BaseSearch), out _), "sequence number 1 where 0 is due");

        (server, client) = await LayersAsync();
        var replayed = client.Protect(BaseSearch);
        Assert.IsTrue(server.TryUnprotect(replayed, out _));
        Assert.IsFalse(server.TryUnprotect(replayed, out _), "sequence number 0 again");

        (server, client) = await LayersAsync();
        var wrongType = client.Protect(BaseSearch);
        wrongType[^5] = 2;
        Assert.IsFalse(server.TryUnprotect(wrongType, out _), "message type 2");

        (server, _) = await LayersAsync();
        Assert.IsFalse(server.TryUnprotect(new byte[15], out _), "shorter than a MAC and a trailer");
    }

    [TestMethod]
    public async Task TripleDesLayer_BodyThatIsNotWholeBlocks_IsRefused()
    {
        var server = (await LoginAsync(Start(Policy()), ClientResponse("auth-conf", "3des").Text)).SecurityLayer!;

        Assert.IsFalse(server.TryUnprotect([.. new byte[17], .. Trailer(0)], out _));
    }

    // Each head is what precedes a 10-byte MAC of zeros, so its last byte is the padding count.
    [TestMethod]
    [DataRow("0000000000000000000000000000", DisplayName = "padding 0")]
    [DataRow("0000000000000000000000000009", DisplayName = "padding 9")]
    [DataRow("0000000000000001070707070707", DisplayName = "padding 7 with a byte that is not 7")]
    [DataRow("080808080808", DisplayName = "padding 8, more than precedes the MAC")]
    public async Task TripleDesLayer_BadlyPaddedBody_IsRefused(string headHex)
    {
        var (text, response) = ClientResponse("auth-conf", "3des");
        var server = (await LoginAsync(Start(Policy()), text)).SecurityLayer!;
        var sessionKey = DigestMd5Calculation.ComputeSessionKey(response, "secret"u8);
        byte[] plain = [.. Convert.FromHexString(headHex), .. new byte[10]];

        var body = TripleDesFirstBody(ConfidentialityKey(sessionKey, ClientToServer), plain);

        Assert.IsFalse(server.TryUnprotect([.. body, .. Trailer(0)], out _));
    }

    [TestMethod]
    public async Task AuthQop_IsAcceptedWithRspauthAndNoLayer()
    {
        var (text, response) = ClientResponse("auth", null);

        var step = await LoginAsync(Start(Policy()), text);

        Assert.AreEqual(SaslLoginOutcome.Accepted, step.Outcome);
        Assert.IsNull(step.SecurityLayer);
        Assert.AreEqual(
            "rspauth=" + Convert.ToHexStringLower(DigestMd5Calculation.ComputeResponseAuth(response, "secret"u8)),
            Encoding.ASCII.GetString(step.AdditionalSuccessData.Span));
        Assert.IsNull(DigestMd5SecurityLayer.ForAcceptor(new byte[16], response));
    }

    [TestMethod]
    [DataRow("auth-conf", "des", DisplayName = "a cipher not offered")]
    [DataRow("auth-conf", null, DisplayName = "auth-conf with no cipher")]
    [DataRow("auth-other", null, DisplayName = "a qop not offered")]
    public async Task Response_ChoosingWhatWasNotOffered_IsRefused(string qop, string? cipher)
    {
        var step = await LoginAsync(Start(Policy()), ClientResponse(qop, cipher).Text);

        Assert.AreEqual(SaslLoginOutcome.RefusedCredentials, step.Outcome);
        Assert.AreEqual("Login refused: DIGEST-MD5 alice", step.CheckedLogin?.Note);
        Assert.IsNull(step.RefusalNote);
    }

    [TestMethod]
    public async Task NonEmptyInitialResponse_IsRefused()
    {
        var pending = Start(Policy(), "username=\"alice\""u8.ToArray()).BeginAsync(CancellationToken.None).AsTask();
        clock.Advance(AuthenticationPolicy.RefusalDelay);

        Assert.AreEqual(SaslLoginOutcome.RefusedCredentials, (await pending).Outcome);
    }

    [TestMethod]
    public async Task AllowAnonymous_UserWithNoAccount_IsRefusedWithTheSecurityLayerNote()
    {
        var step = await LoginAsync(Start(Policy(allowAnonymous: true)), ClientResponse("auth-conf", "3des", user: "nobody").Text);

        Assert.AreEqual(SaslLoginOutcome.RefusedCredentials, step.Outcome);
        Assert.AreEqual(NtlmSaslExchange.SecurityLayerNeedsPasswordNote, step.RefusalNote);
    }

    [TestMethod]
    public async Task AllowAnonymous_KnownUser_IsStillChecked()
    {
        var accepted = await LoginAsync(Start(Policy(allowAnonymous: true)), ClientResponse("auth-conf", "3des").Text);
        var refused = await LoginAsync(Start(Policy(allowAnonymous: true)), ClientResponse("auth-conf", "3des", password: "wrong").Text);

        Assert.AreEqual(SaslLoginOutcome.Accepted, accepted.Outcome);
        Assert.IsNotNull(accepted.SecurityLayer);
        Assert.AreEqual(SaslLoginOutcome.RefusedCredentials, refused.Outcome);
        Assert.IsNull(refused.RefusalNote);
    }

    [TestMethod]
    public async Task MailExchange_SameStart_KeepsTheMailChallengeAndRefusesALayer()
    {
        var exchange = Start(Policy(), canCarrySecurityLayer: false);

        var challenge = await exchange.BeginAsync(CancellationToken.None);
        var pending = exchange.ContinueAsync(Encoding.Latin1.GetBytes(ClientResponse("auth-conf", "3des").Text), CancellationToken.None).AsTask();
        clock.Advance(AuthenticationPolicy.RefusalDelay);

        Assert.AreEqual(MailChallenge, Encoding.ASCII.GetString(challenge.Challenge.Span));
        Assert.AreEqual(SaslLoginOutcome.RefusedCredentials, (await pending).Outcome);
    }

    [TestMethod]
    public async Task MailExchange_AuthLogin_SendsRspauthAsAChallengeWithNoSuccessDataOrLayer()
    {
        var exchange = Start(Policy(), canCarrySecurityLayer: false);
        await exchange.BeginAsync(CancellationToken.None);

        var rspauth = await exchange.ContinueAsync(Encoding.Latin1.GetBytes(ClientResponse("auth", null).Text), CancellationToken.None);
        var step = await exchange.ContinueAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        Assert.AreEqual(SaslLoginOutcome.Challenge, rspauth.Outcome);
        Assert.StartsWith("rspauth=", Encoding.ASCII.GetString(rspauth.Challenge.Span));
        Assert.AreEqual(SaslLoginOutcome.Accepted, step.Outcome);
        Assert.IsTrue(step.AdditionalSuccessData.IsEmpty);
        Assert.IsNull(step.SecurityLayer);
    }

    [TestMethod]
    public async Task MailExchange_EmptyInitialResponse_IsStillRefused()
    {
        var pending = Start(Policy(), ReadOnlyMemory<byte>.Empty, canCarrySecurityLayer: false).BeginAsync(CancellationToken.None).AsTask();
        clock.Advance(AuthenticationPolicy.RefusalDelay);

        Assert.AreEqual(SaslLoginOutcome.RefusedCredentials, (await pending).Outcome);
    }
}
