namespace Surl.Kerberos;

/// <summary>
/// Drives <see cref="KerberosAcceptor" /> with AP-REQs made by hand (ADR-0057 decision 11) and
/// pins each check of decision 4, with every refusal returned as a reason, never thrown.
/// </summary>
[TestClass]
public sealed class KerberosAcceptorTests
{
    private static readonly KerberosPrincipalName User = new(ApRequestBuilder.Realm, ["user"]);

    [TestMethod]
    [DataRow(KerberosEncryptionType.Aes128CtsHmacSha196, DisplayName = "17")]
    [DataRow(KerberosEncryptionType.Aes256CtsHmacSha196, DisplayName = "18")]
    [DataRow(KerberosEncryptionType.Aes128CtsHmacSha256128, DisplayName = "19")]
    [DataRow(KerberosEncryptionType.Aes256CtsHmacSha384192, DisplayName = "20")]
    public void Accept_TicketOfEachEnctype_GivesAContextForTheTicketsClient(KerberosEncryptionType encryptionType)
    {
        byte[] token = new ApRequestBuilder { TicketEncryptionType = encryptionType }.Build();

        KerberosAcceptResult result = CreateAcceptor().Accept(token, "HTTP");

        Assert.IsNull(result.RefusalReason);
        Assert.AreEqual(User, result.Context!.ClientPrincipal);
    }

    [TestMethod]
    public void Accept_SessionKeyOfAnotherEnctypeThanTheTicket_IsAccepted()
    {
        byte[] token = new ApRequestBuilder
        {
            TicketEncryptionType = KerberosEncryptionType.Aes256CtsHmacSha384192,
            SessionKeyTypeNumber = (int)KerberosEncryptionType.Aes128CtsHmacSha196,
        }.Build();

        KerberosAcceptResult result = CreateAcceptor().Accept(token, "HTTP");

        Assert.IsNull(result.RefusalReason);
    }

    [TestMethod]
    public void Accept_EveryOptionalFieldPresent_IsAccepted()
    {
        byte[] token = new ApRequestBuilder
        {
            TicketHasUncheckedFields = true,
            AuthenticatorHasAuthorizationData = true,
            SubkeyTypeNumber = (int)KerberosEncryptionType.Aes128CtsHmacSha256128,
        }.Build();

        KerberosAcceptResult result = CreateAcceptor().Accept(token, "HTTP");

        Assert.IsNull(result.RefusalReason);
    }

    [TestMethod]
    public void Accept_NoStartTimeKvnoOrSequenceNumber_IsAccepted()
    {
        byte[] token = new ApRequestBuilder { StartTime = null, TicketKeyVersionNumber = null, SequenceNumber = null }.Build();

        KerberosAcceptResult result = CreateAcceptor().Accept(token, "HTTP");

        Assert.IsNull(result.RefusalReason);
    }

    // Older Windows clients write seq-number as a negative INTEGER, 0xFFFFFFFE as -2; with no
    // AP-REP surl's first wrap token carries the client's sequence number.
    [TestMethod]
    [DataRow(-2L, 0xFFFFFFFEUL, DisplayName = "-2")]
    [DataRow(0xFFFFFFFFL, 0xFFFFFFFFUL, DisplayName = "0xFFFFFFFF")]
    [DataRow(null, 0UL, DisplayName = "absent")]
    public void Accept_SequenceNumber_IsTheClientsFirstAsAUInt32(long? sequenceNumber, ulong expected)
    {
        byte[] token = new ApRequestBuilder { SequenceNumber = sequenceNumber, MutualRequired = false }.Build();

        KerberosAcceptResult result = CreateAcceptor().Accept(token, "HTTP");

        Assert.AreEqual(expected, System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(result.Context!.Wrap([]).AsSpan(8)));
    }

    [TestMethod]
    [DataRow(0x100000000L, DisplayName = "2^32")]
    [DataRow(-2147483649L, DisplayName = "below Int32.MinValue")]
    public void Accept_SequenceNumberOutOfUInt32Range_IsMalformed(long sequenceNumber)
    {
        byte[] token = new ApRequestBuilder { SequenceNumber = sequenceNumber }.Build();

        KerberosAcceptResult result = CreateAcceptor().Accept(token, "HTTP");

        Assert.AreEqual("malformed token", result.RefusalReason);
    }

    [TestMethod]
    [DataRow("HTTP", DisplayName = "as written")]
    [DataRow("http", DisplayName = "lower case")]
    public void Accept_ServiceInAnyCase_Matches(string service)
    {
        byte[] token = new ApRequestBuilder { ServerName = ["Http", ApRequestBuilder.Host], ServerRealm = "example.com" }.Build();

        KerberosAcceptResult result = CreateAcceptor().Accept(token, service);

        Assert.IsNull(result.RefusalReason);
    }

    [TestMethod]
    public void Accept_MicrosoftKerberosOidOutsideSpnego_IsMalformed()
    {
        byte[] token = new ApRequestBuilder { Oid = ApRequestBuilder.MicrosoftKerberosOid }.Build();

        KerberosAcceptResult result = CreateAcceptor().Accept(token, "HTTP");

        Assert.AreEqual("malformed token", result.RefusalReason);
        Assert.IsNull(result.Context);
    }

    [TestMethod]
    [DataRow(ApRequestBuilder.MicrosoftKerberosOid, DisplayName = "Microsoft's OID")]
    [DataRow(ApRequestBuilder.KerberosOid, DisplayName = "the Kerberos OID")]
    public void AcceptInsideSpnego_EitherKerberosOid_IsAccepted(string oid)
    {
        byte[] token = new ApRequestBuilder { Oid = oid }.Build();

        KerberosAcceptResult result = CreateAcceptor().AcceptInsideSpnego(token, "HTTP");

        Assert.IsNull(result.RefusalReason);
    }

    [TestMethod]
    public void AcceptInsideSpnego_SpnegoOid_IsMalformed()
    {
        byte[] token = new ApRequestBuilder { Oid = "1.3.6.1.5.5.2" }.Build();

        KerberosAcceptResult result = CreateAcceptor().AcceptInsideSpnego(token, "HTTP");

        Assert.AreEqual("malformed token", result.RefusalReason);
    }

    [TestMethod]
    public void Accept_WrongService_IsRefusedAsNoKey()
    {
        byte[] token = new ApRequestBuilder().Build();

        KerberosAcceptResult result = CreateAcceptor().Accept(token, "smtp");

        Assert.AreEqual("no key for HTTP/web01.example.com@EXAMPLE.COM aes256-cts-hmac-sha1-96 kvno 3", result.RefusalReason);
    }

    [TestMethod]
    public void Accept_ServerNameOfOneComponent_IsRefusedAsNoKey()
    {
        byte[] token = new ApRequestBuilder { ServerName = ["HTTP"] }.Build();

        KerberosAcceptResult result = CreateAcceptor().Accept(token, "HTTP");

        Assert.AreEqual("no key for HTTP@EXAMPLE.COM aes256-cts-hmac-sha1-96 kvno 3", result.RefusalReason);
    }

    [TestMethod]
    [DataRow(17, 3U, "no key for HTTP/other.example.com@EXAMPLE.COM aes128-cts-hmac-sha1-96 kvno 3", "other.example.com", DisplayName = "other host")]
    [DataRow(18, 4U, "no key for HTTP/web01.example.com@EXAMPLE.COM aes256-cts-hmac-sha1-96 kvno 4", ApRequestBuilder.Host, DisplayName = "other kvno")]
    [DataRow(23, 3U, "no key for HTTP/web01.example.com@EXAMPLE.COM enctype 23 kvno 3", ApRequestBuilder.Host, DisplayName = "rc4-hmac")]
    [DataRow(19, null, "no key for HTTP/web01.example.com@EXAMPLE.COM aes128-cts-hmac-sha256-128 kvno any", "other.example.com", DisplayName = "no kvno")]
    [DataRow(20, 9U, "no key for HTTP/web01.example.com@EXAMPLE.COM aes256-cts-hmac-sha384-192 kvno 9", ApRequestBuilder.Host, DisplayName = "enctype 20")]
    public void Accept_NoKeyForThePrincipalEnctypeAndKvno_IsRefusedNamingThem(int encryptionTypeNumber, uint? kvno, string reason, string host)
    {
        byte[] token = new ApRequestBuilder
        {
            ServerName = ["HTTP", host],
            TicketEncryptionTypeNumber = encryptionTypeNumber,
            TicketKeyVersionNumber = kvno,
        }.Build();

        KerberosAcceptResult result = CreateAcceptor().Accept(token, "HTTP");

        Assert.AreEqual(reason.Replace("web01.example.com", host, StringComparison.Ordinal), result.RefusalReason);
    }

    [TestMethod]
    public void Accept_WrongServiceKey_RefusesWithoutTheKeyOrAnyDecryptedField()
    {
        byte[] wrongKey = Enumerable.Repeat((byte)0x77, 32).ToArray();
        byte[] token = new ApRequestBuilder { TicketKey = wrongKey }.Build();

        KerberosAcceptResult result = CreateAcceptor().Accept(token, "HTTP");

        Assert.AreEqual("integrity check failed", result.RefusalReason);
        string reason = result.RefusalReason!;
        foreach (string secret in new[]
        {
            Convert.ToHexString(wrongKey),
            Convert.ToHexString(ApRequestBuilder.ServiceKeyOf(KerberosEncryptionType.Aes256CtsHmacSha196)),
            Convert.ToHexString(new ApRequestBuilder().SessionKey),
            "user",
            "2026",
        })
        {
            Assert.DoesNotContain(secret, reason, StringComparison.OrdinalIgnoreCase);
        }
    }

    [TestMethod]
    public void Accept_TamperedTicket_IsRefusedAsAnIntegrityFailure()
    {
        byte[] token = new ApRequestBuilder { TamperTicket = true }.Build();

        KerberosAcceptResult result = CreateAcceptor().Accept(token, "HTTP");

        Assert.AreEqual("integrity check failed", result.RefusalReason);
    }

    [TestMethod]
    public void Accept_TamperedAuthenticator_IsRefusedAsAnIntegrityFailure()
    {
        byte[] token = new ApRequestBuilder().Build();
        token[^5] ^= 0x01;

        KerberosAcceptResult result = CreateAcceptor().Accept(token, "HTTP");

        Assert.AreEqual("integrity check failed", result.RefusalReason);
    }

    [TestMethod]
    public void Accept_JustInsideEndTimePlusSkew_IsAccepted()
    {
        DateTimeOffset endTime = ApRequestBuilder.Now.AddHours(1);
        byte[] token = new ApRequestBuilder { EndTime = endTime, ClientTime = endTime.AddSeconds(300), ClientMicroseconds = 0 }.Build();

        KerberosAcceptResult result = CreateAcceptor(endTime.AddSeconds(300)).Accept(token, "HTTP");

        Assert.IsNull(result.RefusalReason);
    }

    [TestMethod]
    public void Accept_PastEndTimePlusSkew_IsRefusedAsExpired()
    {
        DateTimeOffset endTime = ApRequestBuilder.Now.AddHours(1);
        byte[] token = new ApRequestBuilder { EndTime = endTime, ClientTime = endTime.AddSeconds(301) }.Build();

        KerberosAcceptResult result = CreateAcceptor(endTime.AddSeconds(301)).Accept(token, "HTTP");

        Assert.AreEqual("ticket expired", result.RefusalReason);
    }

    [TestMethod]
    public void Accept_BeforeStartTimeLessSkew_IsRefusedAsNotYetValid()
    {
        byte[] token = new ApRequestBuilder { StartTime = ApRequestBuilder.Now.AddSeconds(301) }.Build();

        KerberosAcceptResult result = CreateAcceptor().Accept(token, "HTTP");

        Assert.AreEqual("ticket not yet valid", result.RefusalReason);
    }

    [TestMethod]
    public void Accept_BeforeAuthTimeLessSkewWithNoStartTime_IsRefusedAsNotYetValid()
    {
        byte[] token = new ApRequestBuilder { StartTime = null, AuthTime = ApRequestBuilder.Now.AddSeconds(301) }.Build();

        KerberosAcceptResult result = CreateAcceptor().Accept(token, "HTTP");

        Assert.AreEqual("ticket not yet valid", result.RefusalReason);
    }

    [TestMethod]
    [DataRow(-301, DisplayName = "301 s behind")]
    [DataRow(301, DisplayName = "301 s ahead")]
    public void Accept_AuthenticatorOutsideTheSkew_IsRefusedAsClockSkew(int offsetSeconds)
    {
        byte[] token = new ApRequestBuilder { ClientTime = ApRequestBuilder.Now.AddSeconds(offsetSeconds), ClientMicroseconds = 0 }.Build();

        KerberosAcceptResult result = CreateAcceptor().Accept(token, "HTTP");

        Assert.AreEqual("clock skew", result.RefusalReason);
    }

    [TestMethod]
    [DataRow(-300, DisplayName = "300 s behind")]
    [DataRow(300, DisplayName = "300 s ahead")]
    public void Accept_AuthenticatorAtTheSkewsEdge_IsAccepted(int offsetSeconds)
    {
        byte[] token = new ApRequestBuilder { ClientTime = ApRequestBuilder.Now.AddSeconds(offsetSeconds), ClientMicroseconds = 0 }.Build();

        KerberosAcceptResult result = CreateAcceptor().Accept(token, "HTTP");

        Assert.IsNull(result.RefusalReason);
    }

    [TestMethod]
    public void Accept_UseSessionKey_IsRefused()
    {
        byte[] token = new ApRequestBuilder { UseSessionKey = true }.Build();

        KerberosAcceptResult result = CreateAcceptor().Accept(token, "HTTP");

        Assert.AreEqual("use-session-key not supported", result.RefusalReason);
    }

    [TestMethod]
    public void Accept_InvalidTicket_IsRefused()
    {
        byte[] token = new ApRequestBuilder { TicketInvalid = true }.Build();

        KerberosAcceptResult result = CreateAcceptor().Accept(token, "HTTP");

        Assert.AreEqual("ticket invalid", result.RefusalReason);
    }

    [TestMethod]
    [DataRow(ApRequestBuilder.Realm, new[] { "other" }, DisplayName = "other cname")]
    [DataRow("OTHER.COM", new[] { "user" }, DisplayName = "other crealm")]
    [DataRow("example.com", new[] { "user" }, DisplayName = "crealm in another case")]
    public void Accept_AuthenticatorClientDiffersFromTheTickets_IsRefused(string realm, string[] name)
    {
        byte[] token = new ApRequestBuilder { AuthenticatorClientRealm = realm, AuthenticatorClientName = name }.Build();

        KerberosAcceptResult result = CreateAcceptor().Accept(token, "HTTP");

        Assert.AreEqual("authenticator client differs from ticket client", result.RefusalReason);
    }

    [TestMethod]
    public void Accept_ChecksumNotOfType0x8003_IsRefused()
    {
        byte[] token = new ApRequestBuilder { ChecksumType = 16 }.Build();

        KerberosAcceptResult result = CreateAcceptor().Accept(token, "HTTP");

        Assert.AreEqual("bad GSS-API checksum", result.RefusalReason);
    }

    [TestMethod]
    public void Accept_NoChecksum_IsRefused()
    {
        byte[] token = new ApRequestBuilder { ChecksumType = null }.Build();

        KerberosAcceptResult result = CreateAcceptor().Accept(token, "HTTP");

        Assert.AreEqual("bad GSS-API checksum", result.RefusalReason);
    }

    [TestMethod]
    public void Accept_ChecksumShorterThan24Bytes_IsRefused()
    {
        byte[] token = new ApRequestBuilder { Checksum = ApRequestBuilder.GssApiChecksumBytes(0x3E)[..23] }.Build();

        KerberosAcceptResult result = CreateAcceptor().Accept(token, "HTTP");

        Assert.AreEqual("bad GSS-API checksum", result.RefusalReason);
    }

    [TestMethod]
    public void Accept_ReplayedAuthenticator_IsRefusedTheSecondTime()
    {
        byte[] token = new ApRequestBuilder().Build();
        KerberosAcceptor acceptor = CreateAcceptor();

        KerberosAcceptResult first = acceptor.Accept(token, "HTTP");
        KerberosAcceptResult second = acceptor.Accept(token, "HTTP");

        Assert.IsNull(first.RefusalReason);
        Assert.AreEqual("replayed authenticator", second.RefusalReason);
    }

    [TestMethod]
    public void Accept_ReplayCacheFull_IsRefusedWithoutEvictingAnEntry()
    {
        KerberosReplayCache replayCache = new(new SettableTimeProvider(ApRequestBuilder.Now));
        for (int index = 0; index < KerberosReplayCache.Capacity; index++)
        {
            replayCache.TryAdd(BitConverter.GetBytes(index), ApRequestBuilder.Now);
        }

        KerberosAcceptResult result = CreateAcceptor(replayCache: replayCache).Accept(new ApRequestBuilder().Build(), "HTTP");

        Assert.AreEqual("replay cache full", result.RefusalReason);
        Assert.AreEqual(KerberosReplayCache.Capacity, replayCache.Count);
        Assert.AreEqual(KerberosReplayCacheOutcome.Replayed, replayCache.TryAdd(BitConverter.GetBytes(0), ApRequestBuilder.Now));
    }

    [TestMethod]
    public void Accept_SessionKeyOfAnEnctypeSurlDoesNotAccept_IsRefused()
    {
        byte[] token = new ApRequestBuilder { SessionKeyTypeNumber = 23 }.Build();

        KerberosAcceptResult result = CreateAcceptor().Accept(token, "HTTP");

        Assert.AreEqual("unsupported enctype 23", result.RefusalReason);
    }

    [TestMethod]
    public void Accept_SessionKeyNotAsLongAsItsEnctypes_IsMalformed()
    {
        byte[] token = new ApRequestBuilder { TicketSessionKeyValue = new byte[16] }.Build();

        KerberosAcceptResult result = CreateAcceptor().Accept(token, "HTTP");

        Assert.AreEqual("malformed token", result.RefusalReason);
    }

    [TestMethod]
    public void Accept_SubkeyOfAnEnctypeSurlDoesNotAccept_IsRefused()
    {
        byte[] token = new ApRequestBuilder { SubkeyTypeNumber = 1 }.Build();

        KerberosAcceptResult result = CreateAcceptor().Accept(token, "HTTP");

        Assert.AreEqual("unsupported enctype 1", result.RefusalReason);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x00 }, DisplayName = "one byte after the token")]
    [DataRow(new byte[] { 0x30, 0x00 }, DisplayName = "an empty SEQUENCE after the token")]
    public void Accept_TrailingBytes_IsMalformed(byte[] trailingBytes)
    {
        byte[] token = new ApRequestBuilder { TrailingBytes = trailingBytes }.Build();

        KerberosAcceptResult result = CreateAcceptor().Accept(token, "HTTP");

        Assert.AreEqual("malformed token", result.RefusalReason);
    }

    [TestMethod]
    [DataRow(new byte[] { }, DisplayName = "empty")]
    [DataRow(new byte[] { 0x60 }, DisplayName = "tag only")]
    [DataRow(new byte[] { 0x60, 0x05, 0x06, 0x01 }, DisplayName = "length past the end")]
    [DataRow(new byte[] { 0x30, 0x00 }, DisplayName = "a SEQUENCE, not [APPLICATION 0]")]
    [DataRow(new byte[] { 0x60, 0x02, 0x04, 0x00 }, DisplayName = "no OID")]
    [DataRow(new byte[] { 0x60, 0x0C, 0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x12, 0x01, 0x02, 0x02, 0x01 }, DisplayName = "half a TOK_ID")]
    [DataRow(new byte[] { 0x60, 0x0D, 0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x12, 0x01, 0x02, 0x02, 0x02, 0x00 }, DisplayName = "an AP-REP's TOK_ID")]
    [DataRow(new byte[] { 0x60, 0x0F, 0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x12, 0x01, 0x02, 0x02, 0x01, 0x00, 0x30, 0x00 }, DisplayName = "not an AP-REQ")]
    public void Accept_MalformedDer_IsMalformed(byte[] token)
    {
        KerberosAcceptResult result = CreateAcceptor().Accept(token, "HTTP");

        Assert.AreEqual("malformed token", result.RefusalReason);
        Assert.IsNull(result.Context);
    }

    [TestMethod]
    [DataRow(4, 14, 5, 5, DisplayName = "pvno 4")]
    [DataRow(5, 15, 5, 5, DisplayName = "msg-type 15")]
    [DataRow(5, 14, 4, 5, DisplayName = "tkt-vno 4")]
    [DataRow(5, 14, 5, 4, DisplayName = "authenticator-vno 4")]
    public void Accept_VersionOrMessageTypeNotRfc4120s_IsMalformed(int protocolVersion, int messageType, int ticketVersion, int authenticatorVersion)
    {
        byte[] token = new ApRequestBuilder
        {
            ProtocolVersion = protocolVersion,
            MessageType = messageType,
            TicketVersion = ticketVersion,
            AuthenticatorVersion = authenticatorVersion,
        }.Build();

        KerberosAcceptResult result = CreateAcceptor().Accept(token, "HTTP");

        Assert.AreEqual("malformed token", result.RefusalReason);
    }

    [TestMethod]
    [DataRow(-1, DisplayName = "negative")]
    [DataRow(1000000, DisplayName = "a whole second")]
    public void Accept_CusecOutOfRange_IsMalformed(int microseconds)
    {
        byte[] token = new ApRequestBuilder { ClientMicroseconds = microseconds }.Build();

        KerberosAcceptResult result = CreateAcceptor().Accept(token, "HTTP");

        Assert.AreEqual("malformed token", result.RefusalReason);
    }

    [TestMethod]
    public void Accept_ClientNameWithNoComponents_IsMalformed()
    {
        byte[] token = new ApRequestBuilder { ClientName = [] }.Build();

        KerberosAcceptResult result = CreateAcceptor().Accept(token, "HTTP");

        Assert.AreEqual("malformed token", result.RefusalReason);
    }

    [TestMethod]
    public void Accept_NullService_Throws()
    {
        byte[] token = new ApRequestBuilder().Build();
        KerberosAcceptor acceptor = CreateAcceptor();

        Assert.ThrowsExactly<ArgumentNullException>(() => acceptor.Accept(token, null!));
    }

    [TestMethod]
    [DataRow(17, "aes128-cts-hmac-sha1-96", DisplayName = "17")]
    [DataRow(18, "aes256-cts-hmac-sha1-96", DisplayName = "18")]
    [DataRow(19, "aes128-cts-hmac-sha256-128", DisplayName = "19")]
    [DataRow(20, "aes256-cts-hmac-sha384-192", DisplayName = "20")]
    [DataRow(23, "enctype 23", DisplayName = "23")]
    public void NameEncryptionType_Number_GivesItsRfcName(int number, string name)
    {
        Assert.AreEqual(name, KerberosAcceptor.NameEncryptionType(number));
    }

    [TestMethod]
    public void ClockSkew_Is300Seconds()
    {
        Assert.AreEqual(TimeSpan.FromSeconds(300), KerberosAcceptor.ClockSkew);
    }

    internal static KerberosAcceptor CreateAcceptor(DateTimeOffset? now = null, KerberosReplayCache? replayCache = null)
    {
        SettableTimeProvider clock = new(now ?? ApRequestBuilder.Now);
        return new KerberosAcceptor(ApRequestBuilder.Keytab(), replayCache ?? new KerberosReplayCache(clock), clock, new CountingRandomSource());
    }
}
