using System.Formats.Asn1;

namespace Surl.Kerberos.TestKdc;

[TestClass]
public sealed class KerberosTestKdcTests
{
    private static readonly string[] HttpServiceName = ["HTTP", "web.surl.test"];

    private readonly SettableTimeProvider clock = new(KdcClient.Now);

    [TestMethod]
    public void Answer_AsRequestWithoutPreAuthentication_IsPreAuthRequiredWithEtypeInfo2NamingTheDefaultSalt()
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock);

        KdcError error = KdcError.Read(kdc.Answer(KdcClient.AsRequest([23, 18, 17, 18])));

        Assert.AreEqual(KerberosErrorCode.PreAuthenticationRequired, error.ErrorCode);
        Assert.AreEqual(kdc.UserPrincipal, error.Client);
        Assert.AreEqual(KerberosTestKdc.TicketGrantingPrincipal, error.Server);
        List<(int Type, byte[] Value)> hints = KdcReply.ReadMethodData(new AsnReader(error.ErrorData, KerberosDer.Rules));
        CollectionAssert.AreEqual(new[] { 19, 2 }, hints.Select(hint => hint.Type).ToArray());
        Assert.IsEmpty(hints[1].Value);
        CollectionAssert.AreEqual(
            new[] { (18, "SURL.TESTtester"), (17, "SURL.TESTtester") },
            ReadEncryptionTypeInfo2(hints[0].Value));
    }

    [TestMethod]
    public void Answer_AsRequestWithAValidEncryptedTimestamp_IsAnAsReplyWhoseTgtTheTicketGrantingKeyDecrypts()
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock);

        (KdcReply reply, EncKdcReplyPart part) = KdcClient.LogIn(kdc);

        Assert.AreEqual(11, reply.MessageType);
        Assert.AreEqual(kdc.UserPrincipal, reply.Client);
        Assert.AreEqual(18, reply.ReplyEncryptionType);
        Assert.AreEqual(19, reply.PreAuthenticationData.Single().Type);
        CollectionAssert.AreEqual(new[] { (18, "SURL.TESTtester") }, ReadEncryptionTypeInfo2(reply.PreAuthenticationData[0].Value));
        Assert.AreEqual(25, part.ApplicationTag);
        Assert.AreEqual(KdcClient.Nonce, part.Nonce);
        Assert.AreEqual(KerberosTestKdc.TicketGrantingPrincipal, part.Server);
        CollectionAssert.AreEqual(new byte[] { 0x00, 0x60, 0x00, 0x00 }, part.Flags);
        Assert.AreEqual(KdcClient.Now, part.AuthTime);
        Assert.AreEqual(KdcClient.Now, part.StartTime);
        Assert.AreEqual(KdcClient.Now + KerberosTestKdc.MaximumTicketLifetime, part.EndTime);
        (KerberosPrincipalName server, KerberosEncryptedData encryptedPart) = reply.ReadTicket();
        Assert.AreEqual(KerberosTestKdc.TicketGrantingPrincipal, server);
        Assert.AreEqual(KerberosTestKdc.KeyVersionNumber, encryptedPart.KeyVersionNumber);
        KerberosTicketPart ticket = reply.OpenTicket(kdc.GetServerKey(KerberosTestKdc.TicketGrantingPrincipal, KerberosEncryptionType.Aes256CtsHmacSha196).Span);
        Assert.AreEqual(kdc.UserPrincipal, ticket.Client);
        Assert.AreEqual((int)KerberosEncryptionType.Aes256CtsHmacSha196, ticket.SessionKey.KeyTypeNumber);
        CollectionAssert.AreEqual(part.Key, ticket.SessionKey.KeyValue);
        Assert.AreEqual(part.EndTime, ticket.EndTime);
    }

    [TestMethod]
    [DataRow(new[] { 17, 18 }, 17)]
    [DataRow(new[] { 18, 17 }, 18)]
    [DataRow(new[] { 23, 20, 18 }, 20)]
    [DataRow(new[] { -135, 19, 17 }, 19)]
    public void Answer_AsRequest_ChoosesTheFirstAesEnctypeInTheClientsOrder(int[] encryptionTypes, int expected)
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock);
        KerberosEncryptionType chosen = (KerberosEncryptionType)expected;
        byte[] request = KdcClient.AsRequest(encryptionTypes, [KdcClient.EncryptedTimestamp(chosen, kdc.GetUserKey(chosen).Span, KdcClient.Now)]);

        KdcReply reply = KdcReply.Read(kdc.Answer(request));

        Assert.AreEqual(expected, reply.ReplyEncryptionType);
        Assert.AreEqual((int)chosen, reply.ReadTicket().EncryptedPart.EncryptionTypeNumber);
        Assert.AreEqual(chosen, reply.Open(kdc.GetUserKey(chosen).Span, 3).KeyType);
    }

    [TestMethod]
    [DataRow(new[] { 23 })]
    [DataRow(new[] { 23, 3, 1 })]
    [DataRow(new int[0])]
    public void Answer_AsRequestWithoutAnAesEnctype_IsEtypeNoSupport(int[] encryptionTypes)
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock);

        KdcError error = KdcError.Read(kdc.Answer(KdcClient.AsRequest(encryptionTypes)));

        Assert.AreEqual(KerberosErrorCode.EncryptionTypeNotSupported, error.ErrorCode);
        Assert.IsNull(error.ErrorData);
    }

    [TestMethod]
    public void Answer_EncryptedTimestampUnderAnotherPassword_IsPreAuthFailed()
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock);
        byte[] wrongKey = KerberosStringToKey.DeriveKey(KerberosEncryptionType.Aes256CtsHmacSha196, "wrong", kdc.UserSalt);

        KdcError error = KdcError.Read(kdc.Answer(KdcClient.AsRequest(KdcClient.Aes, [KdcClient.EncryptedTimestamp(KerberosEncryptionType.Aes256CtsHmacSha196, wrongKey, KdcClient.Now)])));

        Assert.AreEqual(KerberosErrorCode.PreAuthenticationFailed, error.ErrorCode);
    }

    [TestMethod]
    [DataRow(301)]
    [DataRow(-301)]
    public void Answer_EncryptedTimestampMoreThanFiveMinutesOff_IsClockSkew(int offsetSeconds)
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock);
        byte[] key = kdc.GetUserKey(KerberosEncryptionType.Aes256CtsHmacSha196).ToArray();

        KdcError error = KdcError.Read(kdc.Answer(KdcClient.AsRequest(
            KdcClient.Aes, [KdcClient.EncryptedTimestamp(KerberosEncryptionType.Aes256CtsHmacSha196, key, KdcClient.Now.AddSeconds(offsetSeconds))])));

        Assert.AreEqual(KerberosErrorCode.ClockSkew, error.ErrorCode);
    }

    [TestMethod]
    public void Answer_EncryptedTimestampFiveMinutesOff_IsAccepted()
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock);
        byte[] key = kdc.GetUserKey(KerberosEncryptionType.Aes256CtsHmacSha196).ToArray();

        byte[] answer = kdc.Answer(KdcClient.AsRequest(
            KdcClient.Aes, [(128, [0x30, 0x00]), KdcClient.EncryptedTimestamp(KerberosEncryptionType.Aes256CtsHmacSha196, key, KdcClient.Now.AddSeconds(-300))]));

        Assert.AreEqual(11, KdcReply.Read(answer).MessageType);
    }

    [TestMethod]
    public void Answer_EncryptedTimestampInRc4_IsEtypeNoSupport()
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock);
        byte[] key = kdc.GetUserKey(KerberosEncryptionType.Aes256CtsHmacSha196).ToArray();

        KdcError error = KdcError.Read(kdc.Answer(KdcClient.AsRequest(
            KdcClient.Aes, [KdcClient.EncryptedTimestamp(KerberosEncryptionType.Aes256CtsHmacSha196, key, KdcClient.Now, claimedEncryptionType: 23)])));

        Assert.AreEqual(KerberosErrorCode.EncryptionTypeNotSupported, error.ErrorCode);
    }

    [TestMethod]
    public void Answer_EncryptedTimestampThatIsNotEncryptedData_IsGeneric()
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock);

        KdcError error = KdcError.Read(kdc.Answer(KdcClient.AsRequest(KdcClient.Aes, [(2, [0x04, 0x00])])));

        Assert.AreEqual(KerberosErrorCode.Generic, error.ErrorCode);
        Assert.AreEqual("malformed request", error.Text);
        Assert.IsNull(error.Client);
    }

    [TestMethod]
    public void Answer_EncryptedTimestampWithTrailingBytes_IsGeneric()
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock);
        (int type, byte[] value) = KdcClient.EncryptedTimestamp(KerberosEncryptionType.Aes256CtsHmacSha196, kdc.GetUserKey(KerberosEncryptionType.Aes256CtsHmacSha196).Span, KdcClient.Now);

        KdcError error = KdcError.Read(kdc.Answer(KdcClient.AsRequest(KdcClient.Aes, [(type, [.. value, 0x05, 0x00])])));

        Assert.AreEqual(KerberosErrorCode.Generic, error.ErrorCode);
    }

    [TestMethod]
    [DataRow("tester@SURL.TEST")]
    [DataRow("tester")]
    public void Answer_AsRequestForTheUserOrItsEnterpriseName_IsAnswered(string clientName)
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock);
        byte[] key = kdc.GetUserKey(KerberosEncryptionType.Aes256CtsHmacSha196).ToArray();

        KdcReply reply = KdcReply.Read(kdc.Answer(KdcClient.AsRequest(
            KdcClient.Aes, [KdcClient.EncryptedTimestamp(KerberosEncryptionType.Aes256CtsHmacSha196, key, KdcClient.Now)], clientName: [clientName])));

        Assert.AreEqual(kdc.UserPrincipal, reply.Client);
    }

    [TestMethod]
    [DataRow(new[] { "somebody" })]
    [DataRow(new[] { "tester@OTHER.TEST" })]
    [DataRow(new[] { "@SURL.TEST" })]
    [DataRow(new[] { "tester", "admin" })]
    public void Answer_AsRequestForAnotherClient_IsClientPrincipalUnknown(string[] clientName)
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock);

        KdcError error = KdcError.Read(kdc.Answer(KdcClient.AsRequest(KdcClient.Aes, clientName: clientName)));

        Assert.AreEqual(KerberosErrorCode.ClientPrincipalUnknown, error.ErrorCode);
        Assert.AreEqual(new KerberosPrincipalName(KerberosTestKdc.Realm, clientName), error.Client);
    }

    [TestMethod]
    public void Answer_AsRequestForAServiceItKnows_IssuesTheServiceTicketDirectly()
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock);
        byte[] key = kdc.GetUserKey(KerberosEncryptionType.Aes256CtsHmacSha196).ToArray();

        KdcReply reply = KdcReply.Read(kdc.Answer(KdcClient.AsRequest(
            KdcClient.Aes, [KdcClient.EncryptedTimestamp(KerberosEncryptionType.Aes256CtsHmacSha196, key, KdcClient.Now)], serverName: HttpServiceName)));

        Assert.AreEqual(kdc.ServicePrincipals[0], reply.ReadTicket().Server);
    }

    [TestMethod]
    public void Answer_AsRequestForAnUnknownServer_IsServerPrincipalUnknownNamingIt()
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock);

        KdcError error = KdcError.Read(kdc.Answer(KdcClient.AsRequest(KdcClient.Aes, serverName: ["HTTP", "other.surl.test"])));

        Assert.AreEqual(KerberosErrorCode.ServerPrincipalUnknown, error.ErrorCode);
        Assert.AreEqual(new KerberosPrincipalName(KerberosTestKdc.Realm, ["HTTP", "other.surl.test"]), error.Server);
    }

    [TestMethod]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public void Answer_AsRequestWithoutAClientOrAServer_IsGeneric(bool omitClientName, bool omitServerName)
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock);

        KdcError error = KdcError.Read(kdc.Answer(KdcClient.AsRequest(KdcClient.Aes, omitClientName: omitClientName, omitServerName: omitServerName)));

        Assert.AreEqual(KerberosErrorCode.Generic, error.ErrorCode);
    }

    [TestMethod]
    public void Answer_TillBeforeTheLongestLifetime_EndsTheTicketThen()
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock);
        byte[] key = kdc.GetUserKey(KerberosEncryptionType.Aes256CtsHmacSha196).ToArray();
        DateTimeOffset till = KdcClient.Now.AddHours(1);

        KdcReply reply = KdcReply.Read(kdc.Answer(KdcClient.AsRequest(
            KdcClient.Aes, [KdcClient.EncryptedTimestamp(KerberosEncryptionType.Aes256CtsHmacSha196, key, KdcClient.Now)], till: till)));

        Assert.AreEqual(till, reply.Open(key, 3).EndTime);
    }

    [TestMethod]
    public void Answer_TillOf19700101_EndsTheTicketAtTheLongestLifetime()
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock);
        byte[] key = kdc.GetUserKey(KerberosEncryptionType.Aes256CtsHmacSha196).ToArray();

        KdcReply reply = KdcReply.Read(kdc.Answer(KdcClient.AsRequest(
            KdcClient.Aes, [KdcClient.EncryptedTimestamp(KerberosEncryptionType.Aes256CtsHmacSha196, key, KdcClient.Now)], till: DateTimeOffset.UnixEpoch)));

        Assert.AreEqual(KdcClient.Now + KerberosTestKdc.MaximumTicketLifetime, reply.Open(key, 3).EndTime);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-60)]
    public void Answer_AsRequestTillNotAfterNow_IsNeverValid(int offsetSeconds)
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock);
        byte[] key = kdc.GetUserKey(KerberosEncryptionType.Aes256CtsHmacSha196).ToArray();

        KdcError error = KdcError.Read(kdc.Answer(KdcClient.AsRequest(
            KdcClient.Aes, [KdcClient.EncryptedTimestamp(KerberosEncryptionType.Aes256CtsHmacSha196, key, KdcClient.Now)], till: KdcClient.Now.AddSeconds(offsetSeconds))));

        Assert.AreEqual(KerberosErrorCode.NeverValid, error.ErrorCode);
    }

    [TestMethod]
    public void Answer_TgsRequestTillBeforeNow_IsNeverValid()
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock);
        (KdcReply asReply, EncKdcReplyPart asPart) = KdcClient.LogIn(kdc);
        clock.Now = KdcClient.Now.AddHours(2);

        KdcError error = KdcError.Read(kdc.Answer(KdcClient.TgsRequest(
            KdcClient.ApRequest(asReply.Ticket, asPart.KeyType, asPart.Key, 7, [KdcClient.UserName], KdcClient.Now), HttpServiceName, KdcClient.Aes, KdcClient.Now.AddHours(1))));

        Assert.AreEqual(KerberosErrorCode.NeverValid, error.ErrorCode);
    }

    [TestMethod]
    public void Answer_AsRequestWithTheOptionalBodyFieldsItReadsPast_IsAnswered()
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock);
        byte[] key = kdc.GetUserKey(KerberosEncryptionType.Aes256CtsHmacSha196).ToArray();
        byte[] request = KdcClient.KdcRequest(
            10, 10, [KdcClient.EncryptedTimestamp(KerberosEncryptionType.Aes256CtsHmacSha196, key, KdcClient.Now)], [KdcClient.UserName], KdcClient.TicketGrantingService, KdcClient.Aes, KdcClient.FarTill, withSkippedFields: true);

        Assert.AreEqual(11, KdcReply.Read(kdc.Answer(request)).MessageType);
    }

    [TestMethod]
    public void Answer_RequestWithoutANonce_IsGeneric()
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock);
        byte[] request = KdcClient.KdcRequest(10, 10, null, [KdcClient.UserName], KdcClient.TicketGrantingService, KdcClient.Aes, KdcClient.FarTill, omitNonce: true);

        Assert.AreEqual(KerberosErrorCode.Generic, KdcError.Read(kdc.Answer(request)).ErrorCode);
    }

    [TestMethod]
    public void Answer_RequestWithAnEnctypeBeyondInt32_IsGeneric()
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock);
        byte[] request = KdcClient.KdcRequest(10, 10, null, [KdcClient.UserName], KdcClient.TicketGrantingService, KdcClient.Aes, KdcClient.FarTill, extraEncryptionType: 1L << 40);

        Assert.AreEqual(KerberosErrorCode.Generic, KdcError.Read(kdc.Answer(request)).ErrorCode);
    }

    [TestMethod]
    [DataRow(10, 12)]
    [DataRow(12, 10)]
    public void Answer_RequestWhoseMessageTypeIsNotItsTags_IsGeneric(int applicationTag, int messageType)
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock);
        byte[] request = KdcClient.KdcRequest(applicationTag, messageType, null, [KdcClient.UserName], KdcClient.TicketGrantingService, KdcClient.Aes, KdcClient.FarTill);

        Assert.AreEqual(KerberosErrorCode.Generic, KdcError.Read(kdc.Answer(request)).ErrorCode);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("6a00")]
    [DataRow("300100")]
    [DataRow("6a023000")]
    public void Answer_BytesThatAreNotAKdcRequest_AreGeneric(string requestHex)
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock);

        KdcError error = KdcError.Read(kdc.Answer(Convert.FromHexString(requestHex)));

        Assert.AreEqual(KerberosErrorCode.Generic, error.ErrorCode);
        Assert.AreEqual(KerberosTestKdc.TicketGrantingPrincipal, error.Server);
    }

    [TestMethod]
    public void Answer_RequestLongerThan64KiB_IsFieldTooLong()
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock);

        KdcError error = KdcError.Read(kdc.Answer(new byte[KerberosTestKdc.MaximumRequestLength + 1]));

        Assert.AreEqual(KerberosErrorCode.FieldTooLong, error.ErrorCode);
    }

    [TestMethod]
    [DataRow(KerberosEncryptionType.Aes256CtsHmacSha196)]
    [DataRow(KerberosEncryptionType.Aes128CtsHmacSha196)]
    [DataRow(KerberosEncryptionType.Aes128CtsHmacSha256128)]
    [DataRow(KerberosEncryptionType.Aes256CtsHmacSha384192)]
    public void Answer_TgsRequestForAConfiguredService_IsATicketKerberosAcceptorAcceptsFromTheWrittenKeytab(KerberosEncryptionType encryptionType)
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock);
        KerberosKeytab keytab = KerberosKeytab.Read(kdc.WriteServiceKeytab()).Keytab!;
        (KdcReply asReply, EncKdcReplyPart asPart) = KdcClient.LogIn(kdc);

        KdcReply reply = KdcReply.Read(kdc.Answer(KdcClient.TgsRequest(
            KdcClient.ApRequest(asReply.Ticket, asPart.KeyType, asPart.Key, 7, [KdcClient.UserName], KdcClient.Now), HttpServiceName, [23, (int)encryptionType])));
        EncKdcReplyPart part = reply.Open(asPart.Key, 8);
        byte[] token = GssApiToken.Frame(GssApiToken.ApRequestTokenId, KdcClient.ApRequest(
            reply.Ticket, part.KeyType, part.Key, 11, [KdcClient.UserName], KdcClient.Now, withGssApiChecksum: true));
        KerberosAcceptResult result = new KerberosAcceptor(keytab, new KerberosReplayCache(clock), clock, new SeededRandomSource()).Accept(token, "HTTP");

        Assert.AreEqual(13, reply.MessageType);
        Assert.IsEmpty(reply.PreAuthenticationData);
        Assert.AreEqual(kdc.UserPrincipal, reply.Client);
        Assert.AreEqual(26, part.ApplicationTag);
        Assert.AreEqual(encryptionType, part.KeyType);
        Assert.AreEqual(KdcClient.Nonce, part.Nonce);
        Assert.AreEqual(kdc.ServicePrincipals[0], part.Server);
        CollectionAssert.AreEqual(new byte[] { 0x00, 0x20, 0x00, 0x00 }, part.Flags);
        Assert.IsNull(result.RefusalReason);
        Assert.AreEqual("tester@SURL.TEST", result.Context!.ClientPrincipal.ToString());
    }

    [TestMethod]
    public void Answer_TgsRequestWithASubkey_EncryptsTheReplyUnderTheSubkey()
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock);
        byte[] subkey = Convert.FromHexString("000102030405060708090a0b0c0d0e0f");
        (KdcReply asReply, EncKdcReplyPart asPart) = KdcClient.LogIn(kdc);

        KdcReply reply = KdcReply.Read(kdc.Answer(KdcClient.TgsRequest(
            KdcClient.ApRequest(asReply.Ticket, asPart.KeyType, asPart.Key, 7, [KdcClient.UserName], KdcClient.Now, (17, subkey)), HttpServiceName, KdcClient.Aes)));

        Assert.AreEqual(17, reply.ReplyEncryptionType);
        Assert.AreEqual(KerberosEncryptionType.Aes256CtsHmacSha196, reply.Open(subkey, 9).KeyType);
    }

    [TestMethod]
    public void Answer_TgsRequestTill_EndsTheTicketNoLaterThanTheTgt()
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock);
        (KdcReply asReply, EncKdcReplyPart asPart) = KdcClient.LogIn(kdc);
        clock.Now = KdcClient.Now.AddHours(1);

        KdcReply reply = KdcReply.Read(kdc.Answer(KdcClient.TgsRequest(
            KdcClient.ApRequest(asReply.Ticket, asPart.KeyType, asPart.Key, 7, [KdcClient.UserName], KdcClient.Now), HttpServiceName, KdcClient.Aes)));
        EncKdcReplyPart part = reply.Open(asPart.Key, 8);

        Assert.AreEqual(KdcClient.Now, part.AuthTime);
        Assert.AreEqual(KdcClient.Now.AddHours(1), part.StartTime);
        Assert.AreEqual(asPart.EndTime, part.EndTime);
    }

    [TestMethod]
    public void Answer_TgsRequestForAnUnknownService_IsServerPrincipalUnknown()
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock);

        KdcError error = KdcError.Read(KdcClient.AskForServiceTicket(kdc, ["HTTP", "unknown.surl.test"]));

        Assert.AreEqual(KerberosErrorCode.ServerPrincipalUnknown, error.ErrorCode);
        Assert.IsNull(error.Client);
    }

    [TestMethod]
    public void Answer_TgsRequestForRc4Alone_IsEtypeNoSupport()
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock);

        KdcError error = KdcError.Read(KdcClient.AskForServiceTicket(kdc, HttpServiceName, [23]));

        Assert.AreEqual(KerberosErrorCode.EncryptionTypeNotSupported, error.ErrorCode);
    }

    [TestMethod]
    public void Answer_TgsRequestWithAnRc4Subkey_IsEtypeNoSupport()
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock);

        KdcError error = KdcError.Read(KdcClient.AskForServiceTicket(kdc, HttpServiceName, subkey: (23, new byte[16])));

        Assert.AreEqual(KerberosErrorCode.EncryptionTypeNotSupported, error.ErrorCode);
        Assert.AreEqual("unsupported enctype 23", error.Text);
    }

    [TestMethod]
    public void Answer_TgsRequestWithoutAPaTgsReq_IsGeneric()
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock);

        KdcError error = KdcError.Read(kdc.Answer(KdcClient.TgsRequest(null, HttpServiceName, KdcClient.Aes)));

        Assert.AreEqual(KerberosErrorCode.Generic, error.ErrorCode);
    }

    [TestMethod]
    public void Answer_TgsRequestWithoutAServer_IsGeneric()
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock);

        KdcError error = KdcError.Read(KdcClient.AskForServiceTicket(kdc, null!));

        Assert.AreEqual(KerberosErrorCode.Generic, error.ErrorCode);
    }

    [TestMethod]
    public void Answer_TgsRequestWithAServiceTicketForTgt_IsNotUs()
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock);
        KdcReply serviceReply = KdcReply.Read(KdcClient.AskForServiceTicket(kdc, HttpServiceName));
        (_, EncKdcReplyPart asPart) = KdcClient.LogIn(kdc);

        KdcError error = KdcError.Read(kdc.Answer(KdcClient.TgsRequest(
            KdcClient.ApRequest(serviceReply.Ticket, asPart.KeyType, asPart.Key, 7, [KdcClient.UserName], KdcClient.Now), HttpServiceName, KdcClient.Aes)));

        Assert.AreEqual(KerberosErrorCode.NotUs, error.ErrorCode);
    }

    [TestMethod]
    public void Answer_TgsRequestWithATamperedTgt_IsBadIntegrity()
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock);
        (KdcReply asReply, EncKdcReplyPart asPart) = KdcClient.LogIn(kdc);
        byte[] tamperedTicket = [.. asReply.Ticket];
        tamperedTicket[^5] ^= 0x01;

        KdcError error = KdcError.Read(kdc.Answer(KdcClient.TgsRequest(
            KdcClient.ApRequest(tamperedTicket, asPart.KeyType, asPart.Key, 7, [KdcClient.UserName], KdcClient.Now), HttpServiceName, KdcClient.Aes)));

        Assert.AreEqual(KerberosErrorCode.BadIntegrity, error.ErrorCode);
    }

    [TestMethod]
    public void Answer_TgsRequestWithATgtClaimingRc4_IsEtypeNoSupport()
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock);
        (KdcReply asReply, EncKdcReplyPart asPart) = KdcClient.LogIn(kdc);
        byte[] rc4Ticket = KerberosKdcMessages.WriteTicket(KerberosTestKdc.TicketGrantingPrincipal, (KerberosEncryptionType)23, 1, asReply.ReadTicket().EncryptedPart.CipherText);

        KdcError error = KdcError.Read(kdc.Answer(KdcClient.TgsRequest(
            KdcClient.ApRequest(rc4Ticket, asPart.KeyType, asPart.Key, 7, [KdcClient.UserName], KdcClient.Now), HttpServiceName, KdcClient.Aes)));

        Assert.AreEqual(KerberosErrorCode.EncryptionTypeNotSupported, error.ErrorCode);
    }

    [TestMethod]
    public void Answer_TgsRequestWithAnExpiredTgt_IsTicketExpired()
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock);
        (KdcReply asReply, EncKdcReplyPart asPart) = KdcClient.LogIn(kdc);
        clock.Now = asPart.EndTime.AddSeconds(1);

        KdcError error = KdcError.Read(kdc.Answer(KdcClient.TgsRequest(
            KdcClient.ApRequest(asReply.Ticket, asPart.KeyType, asPart.Key, 7, [KdcClient.UserName], clock.Now), HttpServiceName, KdcClient.Aes)));

        Assert.AreEqual(KerberosErrorCode.TicketExpired, error.ErrorCode);
    }

    [TestMethod]
    public void Answer_TgsRequestWithAnAuthenticatorUnderAnotherKey_IsBadIntegrity()
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock);
        (KdcReply asReply, EncKdcReplyPart asPart) = KdcClient.LogIn(kdc);

        KdcError error = KdcError.Read(kdc.Answer(KdcClient.TgsRequest(
            KdcClient.ApRequest(asReply.Ticket, asPart.KeyType, new byte[32], 7, [KdcClient.UserName], KdcClient.Now), HttpServiceName, KdcClient.Aes)));

        Assert.AreEqual(KerberosErrorCode.BadIntegrity, error.ErrorCode);
    }

    [TestMethod]
    public void Answer_TgsRequestWhoseAuthenticatorNamesAnotherClient_IsBadMatch()
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock);
        (KdcReply asReply, EncKdcReplyPart asPart) = KdcClient.LogIn(kdc);

        KdcError error = KdcError.Read(kdc.Answer(KdcClient.TgsRequest(
            KdcClient.ApRequest(asReply.Ticket, asPart.KeyType, asPart.Key, 7, ["intruder"], KdcClient.Now), HttpServiceName, KdcClient.Aes)));

        Assert.AreEqual(KerberosErrorCode.BadMatch, error.ErrorCode);
    }

    [TestMethod]
    public void WriteServiceKeytab_HoldsEveryEnctypeOfEveryServiceAtKeyVersion1()
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock, "HTTP/web.surl.test", "smtp/mail.surl.test");

        KerberosKeytabReadResult result = KerberosKeytab.Read(kdc.WriteServiceKeytab());

        Assert.IsEmpty(result.SkippedEntries);
        Assert.HasCount(8, result.Keytab!.Entries);
        foreach (KerberosKeytabEntry entry in result.Keytab.Entries)
        {
            Assert.AreEqual(KerberosTestKdc.KeyVersionNumber, entry.KeyVersionNumber);
            CollectionAssert.AreEqual(kdc.GetServerKey(entry.Principal, entry.EncryptionType).ToArray(), entry.Key.ToArray());
        }

        CollectionAssert.AreEqual(
            new[] { "HTTP/web.surl.test@SURL.TEST", "smtp/mail.surl.test@SURL.TEST" },
            result.Keytab.Entries.Select(entry => entry.Principal.ToString()).Distinct().ToArray());
    }

    [TestMethod]
    public void WriteServiceKeytab_OneComponentService_IsWrittenAsAPrincipal()
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock, "service");

        byte[] keytab = kdc.WriteServiceKeytab();

        Assert.AreEqual("service@SURL.TEST", KerberosKeytab.Read(keytab).Keytab!.Entries[0].Principal.ToString());
        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 1 }, keytab[28..32]);
    }

    [TestMethod]
    [DataRow("HTTP//web.surl.test")]
    [DataRow("")]
    [DataRow("HTTP/")]
    public void Constructor_ServicePrincipalWithAnEmptyComponent_Throws(string servicePrincipal)
    {
        Assert.ThrowsExactly<ArgumentException>(() => KdcClient.NewKdc(clock, servicePrincipal));
    }

    private static (int, string)[] ReadEncryptionTypeInfo2(byte[] value)
    {
        AsnReader outer = new(value, KerberosDer.Rules);
        AsnReader entries = outer.ReadSequence();
        List<(int, string)> read = [];
        while (entries.HasData)
        {
            AsnReader entry = entries.ReadSequence();
            read.Add((KerberosDer.ReadInt32Field(entry, 0), KerberosDer.ReadStringField(entry, 1)));
            entry.ThrowIfNotEmpty();
        }

        return [.. read];
    }
}
