using System.Formats.Asn1;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ldap.LdapRequestBytes;
using static Surl.Protocol.Ldap.LdapServerExchange;

namespace Surl.Protocol.Ldap;

/// <summary>
/// The operations upstream curl never sends but a client may (ADR-0072 decision 3), and the
/// binds the Windows build makes only through SASL, built by hand since no curl case records
/// them.
/// </summary>
[TestClass]
public sealed class LdapProtocolServerOperationTests
{
    private const string NoticeOfDisconnection = "[10] 1.3.6.1.4.1.1466.20036";

    private static readonly byte[] AliceBind = Message(1, SimpleBind(3, "alice", "secret"));

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_NoPolicy_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new LdapProtocolServer(null!, new UnitTestSaslAuthenticationPolicy()));
        Assert.ThrowsExactly<ArgumentNullException>(() => new LdapProtocolServer(new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted), null!));
    }

    [TestMethod]
    public async Task ServeAsync_NoConnectionOrNoContext_Throws()
    {
        var server = Server(new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted));

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(null!, Context(TestContext.CancellationToken)));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(new InMemoryConnection([]), null!));
    }

    [TestMethod]
    public void Schemes_AreLdap()
    {
        CollectionAssert.AreEqual(new[] { "ldap" }, new LdapProtocolServer(new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted), new UnitTestSaslAuthenticationPolicy()).Schemes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_PublicServerOverItsEmptyDirectory_AnswersNoSuchObject()
    {
        var server = new LdapProtocolServer(new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted), new UnitTestSaslAuthenticationPolicy());
        var connection = new InMemoryConnection([AliceBind, Message(2, Search(Present("objectClass"), scope: 0))]);

        await server.ServeAsync(connection, Context(TestContext.CancellationToken));

        Assert.AreEqual("#2 searchResultDone noSuchObject", LdapResponseTranscript.Of(connection.WrittenBytes)[^1]);
    }

    [TestMethod]
    public async Task ServeAsync_ConnectionClosedBeforeAnyMessage_SendsAndNotesNothing()
    {
        var log = new RecordingExchangeLog();

        var connection = await RunAsync(new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted), TestContext.CancellationToken, log);

        Assert.IsEmpty(connection.WrittenBytes);
        Assert.IsEmpty(log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_ConnectionClosedPartWayThroughAMessage_NotesItAndSendsNothing()
    {
        var log = new RecordingExchangeLog();

        var connection = await RunAsync(new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted), TestContext.CancellationToken, log, AliceBind[..10]);

        Assert.IsEmpty(connection.WrittenBytes);
        CollectionAssert.AreEqual(new[] { "The client closed the connection part way through a message." }, log.Notes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_RootDseSearchWithNoBind_IsAnsweredWithoutAskingThePolicy()
    {
        var policy = new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted);

        var connection = await RunAsync(policy, TestContext.CancellationToken, null, Message(1, Search(Present("objectClass"), baseObject: string.Empty, scope: 0, attributes: "+")));

        CollectionAssert.AreEqual(
            new[]
            {
                "#1 searchResultEntry : namingContexts=dc=example,dc=com,o=other; supportedLDAPVersion=3",
                "#1 searchResultDone success",
            },
            LdapResponseTranscript.Of(connection.WrittenBytes).ToArray());
        Assert.IsEmpty(policy.Logins);
    }

    [TestMethod]
    public async Task ServeAsync_Unbind_ClosesWithNothingSentAndReadsNoFurther()
    {
        var connection = await RunAsync(
            new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted), TestContext.CancellationToken, null, Hex("30050201034200"), AliceBind);

        Assert.IsEmpty(connection.WrittenBytes);
        Assert.IsFalse(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task ServeAsync_Abandon_HasNoAnswerAndTheNextMessageIsAnswered()
    {
        // messageID 3, AbandonRequest 2.
        var connection = await RunAsync(
            new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted), TestContext.CancellationToken, null, Hex("3006020103500102"), AliceBind);

        CollectionAssert.AreEqual(new[] { "#1 bindResponse success" }, LdapResponseTranscript.Of(connection.WrittenBytes).ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_UnknownOperation_IsTheNoticeOfDisconnectionThenTheClose()
    {
        var log = new RecordingExchangeLog();

        // messageID 2, an [APPLICATION 30] no request is, then a bind that is never read.
        var connection = await RunAsync(
            new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted), TestContext.CancellationToken, log, Hex("30050201027E00"), AliceBind);

        // messageID 0, ExtendedResponse { protocolError, "", "unknown operation", [10] the notice's OID }.
        CollectionAssert.AreEqual(
            Hex("3035020100 7830 0A0102 0400 0411 756E6B6E6F776E206F7065726174696F6E 8A16 312E332E362E312E342E312E313436362E3230303336"),
            connection.WrittenBytes);
        Assert.IsTrue(connection.WritesCompleted);
        CollectionAssert.AreEqual(new[] { "LDAP Notice of Disconnection: protocolError: unknown operation" }, log.Notes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_ContextTaggedOperation_IsAnUnknownOperation()
    {
        var connection = await RunAsync(new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted), TestContext.CancellationToken, null, Hex("3005020102A600"));

        Assert.AreEqual($"#0 extendedResponse protocolError \"unknown operation\" {NoticeOfDisconnection}", LdapResponseTranscript.Of(connection.WrittenBytes).Single());
    }

    [TestMethod]
    [DataRow("30800201024200000000", "an indefinite length", DisplayName = "Indefinite length")]
    [DataRow("3185000000000100", "not an LDAPMessage", DisplayName = "A SET")]
    [DataRow("3085000000000100", "a length of more than four octets", DisplayName = "Five length octets")]
    [DataRow("300602010260020201", "a malformed tag or length", DisplayName = "A bind cut short")]
    [DataRow("3008020102420000", "a malformed tag or length", DisplayName = "Unbind longer than its message")]
    [DataRow("30060201024201FF", "an invalid value", DisplayName = "Unbind not NULL")]
    public async Task ServeAsync_MalformedMessage_IsTheNoticeOfDisconnectionNamingTheFault(string hex, string diagnostic)
    {
        var connection = await RunAsync(new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted), TestContext.CancellationToken, null, Hex(hex), AliceBind);

        Assert.AreEqual($"#0 extendedResponse protocolError \"{diagnostic}\" {NoticeOfDisconnection}", LdapResponseTranscript.Of(connection.WrittenBytes).Single());
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task ServeAsync_FilterNestedPastTheLimit_IsTheNoticeOfDisconnection()
    {
        Action<AsnWriter> filter = Present("cn");
        for (var depth = 0; depth < LdapProtocolServer.MaxFilterDepth + 1; depth++)
        {
            filter = Not(filter);
        }

        var connection = await RunAsync(new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted), TestContext.CancellationToken, null, Message(2, Search(filter)));

        Assert.AreEqual($"#0 extendedResponse protocolError \"a filter nested too deep\" {NoticeOfDisconnection}", LdapResponseTranscript.Of(connection.WrittenBytes).Single());
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(4)]
    public async Task ServeAsync_BindOfAnotherVersion_IsProtocolErrorUnchecked(int version)
    {
        var policy = new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted);

        var connection = await RunAsync(policy, TestContext.CancellationToken, null, Message(1, SimpleBind(version, "alice", "secret")));

        Assert.AreEqual("#1 bindResponse protocolError \"only LDAP versions 2 and 3 are answered\"", LdapResponseTranscript.Of(connection.WrittenBytes).Single());
        Assert.IsEmpty(policy.Logins);
    }

    [TestMethod]
    public async Task ServeAsync_ReservedAuthenticationChoice_IsAuthMethodNotSupported()
    {
        var log = new RecordingExchangeLog();

        // messageID 1, BindRequest { 3, "", [1] "" }: a choice RFC 4511 reserves.
        var connection = await RunAsync(
            new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted), TestContext.CancellationToken, log, Hex("300C020101600702010304008100"));

        Assert.AreEqual("#1 bindResponse authMethodNotSupported \"authentication method not accepted\"", LdapResponseTranscript.Of(connection.WrittenBytes).Single());
        Assert.AreEqual("LDAP bind refused: authMethodNotSupported: authentication method not accepted", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_NameWithAnEmptyPassword_IsUnwillingToPerformUnchecked()
    {
        var policy = new UnitTestAuthenticationPolicy(PasswordLoginVerdict.AcceptedUnchecked);

        var connection = await RunAsync(policy, TestContext.CancellationToken, null, Message(1, SimpleBind(3, "alice", string.Empty)));

        Assert.AreEqual("#1 bindResponse unwillingToPerform \"unauthenticated bind refused\"", LdapResponseTranscript.Of(connection.WrittenBytes).Single());
        Assert.IsEmpty(policy.Logins);
    }

    [TestMethod]
    public async Task ServeAsync_AnonymousBindWithoutAllowAnonymous_IsInappropriateAuthentication()
    {
        var log = new RecordingExchangeLog();

        var connection = await RunAsync(new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted), TestContext.CancellationToken, log, Message(1, SimpleBind(3, string.Empty, string.Empty)));

        Assert.AreEqual("#1 bindResponse inappropriateAuthentication \"anonymous bind refused\"", LdapResponseTranscript.Of(connection.WrittenBytes).Single());
        Assert.AreEqual("LDAP bind refused: inappropriateAuthentication: anonymous bind refused", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_AnonymousBindUnderAllowAnonymous_IsSuccessAndTheConnectionMayRead()
    {
        var policy = new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted, anonymousVerdict: PasswordLoginVerdict.AcceptedUnchecked);

        var connection = await RunAsync(policy, TestContext.CancellationToken, null, Message(1, SimpleBind(3, string.Empty, string.Empty)), Message(2, Search(Present("objectClass"), scope: 0)));

        CollectionAssert.AreEqual(
            new[] { "#1 bindResponse success", "#2 searchResultEntry dc=example,dc=com: objectClass=domain; dc=example", "#2 searchResultDone success" },
            LdapResponseTranscript.Of(connection.WrittenBytes).ToArray());
        Assert.IsTrue(policy.Logins.All(login => login.UserName is null && login.Password is null));
    }

    [TestMethod]
    public async Task ServeAsync_AnonymousLoginThePolicyChecks_NotesTheLoginWithNoUser()
    {
        var log = new RecordingExchangeLog();
        var policy = new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted, anonymousVerdict: PasswordLoginVerdict.Accepted);

        await RunAsync(policy, TestContext.CancellationToken, log, Message(1, SimpleBind(3, string.Empty, string.Empty)));

        Assert.AreEqual("Login accepted: simple", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_VerdictTheServerDoesNotKnow_IsRefusedNotBound()
    {
        var connection = await RunAsync(new UnitTestAuthenticationPolicy((PasswordLoginVerdict)99), TestContext.CancellationToken, null, AliceBind);

        Assert.AreEqual("#1 bindResponse confidentialityRequired \"simple bind needs TLS or --allow-plaintext-auth\"", LdapResponseTranscript.Of(connection.WrittenBytes).Single());
    }

    [TestMethod]
    public async Task ServeAsync_EmptyNameWithAPassword_GoesToThePolicyWithNoUser()
    {
        var policy = new UnitTestAuthenticationPolicy(PasswordLoginVerdict.RefusedAnonymous);

        var connection = await RunAsync(policy, TestContext.CancellationToken, null, Message(1, SimpleBind(3, string.Empty, "secret")));

        Assert.AreEqual("#1 bindResponse inappropriateAuthentication \"anonymous bind refused\"", LdapResponseTranscript.Of(connection.WrittenBytes).Single());
        Assert.IsNull(policy.Logins.Single().UserName);
    }

    [TestMethod]
    public async Task ServeAsync_BindDn_IsCheckedAsItsLeftmostCnAndNotedEscaped()
    {
        var log = new RecordingExchangeLog();
        var policy = new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted);

        await RunAsync(policy, TestContext.CancellationToken, log, Message(1, SimpleBind(3, "cn=al\\5Cice,dc=example,dc=com", "secret")));

        Assert.AreEqual("al\\ice", policy.Logins.Single().UserName);
        Assert.AreEqual("Login accepted: simple al\\x5Cice", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_BindAfterABoundOneFails_LeavesTheConnectionAnonymous()
    {
        var connection = await RunAsync(
            new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted),
            TestContext.CancellationToken,
            null,
            AliceBind,
            Message(2, SimpleBind(3, "alice", string.Empty)),
            Message(3, Search(Present("objectClass"), scope: 0)));

        Assert.AreEqual("#3 searchResultDone insufficientAccessRights \"bind first\"", LdapResponseTranscript.Of(connection.WrittenBytes)[^1]);
    }

    [TestMethod]
    public async Task ServeAsync_ExtendedRequest_IsProtocolErrorWithNoResponseName()
    {
        var startTls = Message(2, writer =>
        {
            using var extended = writer.PushSequence(LdapTags.ExtendedRequest);
            Text(writer, "1.3.6.1.4.1.1466.20037", LdapTags.Context(0));
        });

        var connection = await RunAsync(new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted), TestContext.CancellationToken, null, startTls, AliceBind);

        CollectionAssert.AreEqual(
            new[] { "#2 extendedResponse protocolError \"unsupported extended operation\"", "#1 bindResponse success" },
            LdapResponseTranscript.Of(connection.WrittenBytes).ToArray());
    }

    [TestMethod]
    [DataRow(6, "modifyResponse", "modify", DisplayName = "Modify")]
    [DataRow(8, "addResponse", "add", DisplayName = "Add")]
    [DataRow(12, "modDNResponse", "modifyDN", DisplayName = "Modify DN")]
    public async Task ServeAsync_ConstructedWrite_IsUnwillingToPerform(int tagNumber, string responseName, string operationName)
    {
        var log = new RecordingExchangeLog();
        var write = Message(4, writer =>
        {
            using var request = writer.PushSequence(new Asn1Tag(TagClass.Application, tagNumber, isConstructed: true));
            Text(writer, "cn=alice,dc=example,dc=com");
        });

        var connection = await RunAsync(new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted), TestContext.CancellationToken, log, AliceBind, write);

        Assert.AreEqual($"#4 {responseName} unwillingToPerform \"the directory is read-only over LDAP\"", LdapResponseTranscript.Of(connection.WrittenBytes)[^1]);
        Assert.AreEqual($"LDAP {operationName} refused: the directory is read-only", log.Notes[^1]);
    }

    [TestMethod]
    public async Task ServeAsync_Delete_IsUnwillingToPerform()
    {
        // messageID 4, DelRequest [APPLICATION 10] "o=other", primitive.
        var connection = await RunAsync(new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted), TestContext.CancellationToken, null, Hex("300C020104 4A07 6F3D6F74686572"));

        Assert.AreEqual("#4 delResponse unwillingToPerform \"the directory is read-only over LDAP\"", LdapResponseTranscript.Of(connection.WrittenBytes).Single());
    }

    [TestMethod]
    [DataRow("cn=alice,dc=example,dc=com", "sn", "SMITH", "compareTrue", DisplayName = "Equal by caseIgnoreMatch")]
    [DataRow("cn=alice,dc=example,dc=com", "sn", "Jones", "compareFalse", DisplayName = "Not equal")]
    [DataRow("cn=alice,dc=example,dc=com", "uidNumber", "x", "compareFalse", DisplayName = "Undefined is false")]
    [DataRow("cn=alice,dc=example,dc=com", "telephoneNumber", "1", "undefinedAttributeType", DisplayName = "A type the entry lacks")]
    [DataRow("cn=nobody,dc=example,dc=com", "sn", "x", "noSuchObject matched dc=example,dc=com", DisplayName = "No such entry")]
    [DataRow("cn=nobody", "sn", "x", "noSuchObject", DisplayName = "No superior either")]
    [DataRow("not a dn", "sn", "x", "invalidDnSyntax \"the entry is not an RFC 4514 DN\"", DisplayName = "Not a DN")]
    public async Task ServeAsync_CompareWhenBound_IsAnsweredByTheEqualityRule(string entry, string type, string value, string answer)
    {
        var connection = await RunAsync(new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted), TestContext.CancellationToken, null, AliceBind, Compare(5, entry, type, value));

        Assert.AreEqual($"#5 compareResponse {answer}", LdapResponseTranscript.Of(connection.WrittenBytes)[^1]);
    }

    [TestMethod]
    public async Task ServeAsync_CompareWithNoBind_IsInsufficientAccessRights()
    {
        var connection = await RunAsync(
            new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted), TestContext.CancellationToken, null, Compare(5, "cn=alice,dc=example,dc=com", "sn", "Smith"));

        Assert.AreEqual("#5 compareResponse insufficientAccessRights \"bind first\"", LdapResponseTranscript.Of(connection.WrittenBytes).Single());
    }

    [TestMethod]
    public async Task ServeAsync_CriticalControlOnAnOperationWithAResponse_IsUnavailableCriticalExtension()
    {
        var connection = await RunAsync(
            new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted),
            TestContext.CancellationToken,
            null,
            Message(1, SimpleBind(3, "alice", "secret"), Control("1.2.840.113556.1.4.319", critical: true)),
            Message(2, Search(Present("objectClass"), scope: 0), Control("1.2.840.113556.1.4.319", critical: true)),
            Compare(3, "cn=alice,dc=example,dc=com", "sn", "Smith", Control("1.2.3", critical: true)));

        CollectionAssert.AreEqual(
            new[]
            {
                "#1 bindResponse unavailableCriticalExtension \"critical control not supported\"",
                "#2 searchResultDone unavailableCriticalExtension \"critical control not supported\"",
                "#3 compareResponse unavailableCriticalExtension \"critical control not supported\"",
            },
            LdapResponseTranscript.Of(connection.WrittenBytes).ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_CriticalControlOnAnExtendedRequest_IsUnavailableCriticalExtension()
    {
        var extended = Message(
            2,
            writer =>
            {
                using var request = writer.PushSequence(LdapTags.ExtendedRequest);
                Text(writer, "1.2.3", LdapTags.Context(0));
            },
            Control("1.2.3", critical: true));

        var connection = await RunAsync(new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted), TestContext.CancellationToken, null, extended);

        Assert.AreEqual("#2 extendedResponse unavailableCriticalExtension \"critical control not supported\"", LdapResponseTranscript.Of(connection.WrittenBytes).Single());
    }

    [TestMethod]
    public async Task ServeAsync_NonCriticalControl_IsIgnored()
    {
        var connection = await RunAsync(
            new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted),
            TestContext.CancellationToken,
            null,
            Message(1, SimpleBind(3, "alice", "secret"), Control("1.2.840.113556.1.4.319", critical: false)));

        Assert.AreEqual("#1 bindResponse success", LdapResponseTranscript.Of(connection.WrittenBytes).Single());
    }

    [TestMethod]
    public async Task ServeAsync_CriticalControlOnAbandonOrUnbind_IsIgnoredSinceNeitherHasAResponse()
    {
        var abandon = Message(3, writer => writer.WriteInteger(2, LdapTags.AbandonRequest), Control("1.2.3", critical: true));
        var unbind = Message(4, writer => writer.WriteNull(LdapTags.UnbindRequest), Control("1.2.3", critical: true));

        var connection = await RunAsync(new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted), TestContext.CancellationToken, null, abandon, unbind, AliceBind);

        Assert.IsEmpty(connection.WrittenBytes);
    }

    private static byte[] Compare(int messageId, string entry, string type, string value, Action<AsnWriter>? controls = null) =>
        Message(
            messageId,
            writer =>
            {
                using var compare = writer.PushSequence(LdapTags.CompareRequest);
                Text(writer, entry);
                using var assertion = writer.PushSequence();
                Text(writer, type);
                Text(writer, value);
            },
            controls);

    private static Action<AsnWriter> Control(string type, bool critical) => writer =>
    {
        using var controls = writer.PushSequence(LdapTags.Controls);
        using var control = writer.PushSequence();
        Text(writer, type);
        writer.WriteBoolean(critical);
    };
}
