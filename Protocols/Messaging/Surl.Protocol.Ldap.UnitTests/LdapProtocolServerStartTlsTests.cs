using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ldap.LdapRequestBytes;
using static Surl.Protocol.Ldap.LdapServerExchange;
using static Surl.Protocol.Ldap.UnitTestSaslAuthenticationPolicy;

namespace Surl.Protocol.Ldap;

/// <summary>
/// ADR-0072 decision 5 in the server: <c>StartTLS</c> with and without a certificate, the bytes
/// pipelined after it, its refusals, the root DSE before and after the upgrade, a bind the
/// plain-text rule refused allowed over TLS, and <c>ldaps</c>.
/// </summary>
[TestClass]
public sealed class LdapProtocolServerStartTlsTests
{
    private const string StartTlsAccepted = "#1 extendedResponse success [10] 1.3.6.1.4.1.1466.20037";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_StartTlsWithACertificate_AnswersSuccessThenUpgrades()
    {
        var policy = new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted);
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection([StartTls(1), Message(2, SimpleBind(3, "alice", "secret"))]);

        await Server(policy, isTlsUpgradeAvailable: true).ServeAsync(connection, Context(TestContext.CancellationToken, log: log));

        Assert.IsTrue(connection.UpgradeRequested);
        Assert.AreSame(InMemoryConnection.DefaultUpgradeTlsSession, connection.TlsSession);
        CollectionAssert.AreEqual(new[] { StartTlsAccepted, "#2 bindResponse success" }, LdapResponseTranscript.Of(connection.WrittenBytes).ToArray());
        Assert.AreSame(InMemoryConnection.DefaultUpgradeTlsSession, policy.Logins.Single().TlsSession);
        Assert.AreEqual("LDAP StartTLS accepted", log.Notes[0]);
    }

    [TestMethod]
    public async Task ServeAsync_BytesPipelinedAfterStartTls_AreDiscardedNeverAnswered()
    {
        var pipelined = Message(2, SimpleBind(3, "alice", "secret"));
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection([(byte[])[.. StartTls(1), .. pipelined], RootDse(3)]);

        await Server(new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted), isTlsUpgradeAvailable: true)
            .ServeAsync(connection, Context(TestContext.CancellationToken, log: log));

        var transcript = LdapResponseTranscript.Of(connection.WrittenBytes);
        Assert.AreEqual(StartTlsAccepted, transcript[0]);
        Assert.IsTrue(transcript.Skip(1).All(line => line.StartsWith("#3 ", StringComparison.Ordinal)));
        CollectionAssert.AreEqual(
            new[] { "LDAP StartTLS accepted", $"Discarded {pipelined.Length} bytes sent after StartTLS" },
            log.Notes.Take(2).ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_StartTlsWithoutACertificate_IsProtocolErrorWithNoNameAndNoUpgrade()
    {
        var connection = new InMemoryConnection([StartTls(1)]);

        await Server(new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted)).ServeAsync(connection, Context(TestContext.CancellationToken));

        Assert.IsFalse(connection.UpgradeRequested);
        Assert.AreEqual("#1 extendedResponse protocolError \"unsupported extended operation\"", LdapResponseTranscript.Of(connection.WrittenBytes).Single());
    }

    [TestMethod]
    public async Task ServeAsync_OtherExtendedOperationWithACertificate_IsProtocolErrorWithNoName()
    {
        var whoAmI = Message(1, writer =>
        {
            using var extended = writer.PushSequence(LdapTags.ExtendedRequest);
            Text(writer, "1.3.6.1.4.1.4203.1.11.3", LdapTags.Context(0));
        });
        var connection = new InMemoryConnection([whoAmI]);

        await Server(new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted), isTlsUpgradeAvailable: true)
            .ServeAsync(connection, Context(TestContext.CancellationToken));

        Assert.IsFalse(connection.UpgradeRequested);
        Assert.AreEqual("#1 extendedResponse protocolError \"unsupported extended operation\"", LdapResponseTranscript.Of(connection.WrittenBytes).Single());
    }

    [TestMethod]
    [DataRow(true, DisplayName = "With a certificate")]
    [DataRow(false, DisplayName = "Without a certificate")]
    public async Task ServeAsync_StartTlsOnATlsConnection_IsOperationsError(bool isTlsUpgradeAvailable)
    {
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection([StartTls(1)], initialTlsSession: InMemoryConnection.DefaultUpgradeTlsSession);

        await Server(new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted), isTlsUpgradeAvailable: isTlsUpgradeAvailable)
            .ServeAsync(connection, Context(TestContext.CancellationToken, log: log, scheme: "ldaps"));

        Assert.IsFalse(connection.UpgradeRequested);
        Assert.AreEqual(
            "#1 extendedResponse operationsError \"TLS is already established\" [10] 1.3.6.1.4.1.1466.20037",
            LdapResponseTranscript.Of(connection.WrittenBytes).Single());
        Assert.AreEqual("LDAP StartTLS refused: operationsError", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_StartTlsDuringASaslBind_IsOperationsError()
    {
        var sasl = new UnitTestSaslAuthenticationPolicy(_ => [Challenge("one"u8.ToArray()), Accepted("alice")]);
        var connection = new InMemoryConnection([SaslBind(1, "X-UNIT"), StartTls(2)]);

        await Server(new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted), sasl, isTlsUpgradeAvailable: true)
            .ServeAsync(connection, Context(TestContext.CancellationToken));

        Assert.IsFalse(connection.UpgradeRequested);
        Assert.AreEqual(
            "#2 extendedResponse operationsError \"a SASL bind is in progress\" [10] 1.3.6.1.4.1.1466.20037",
            LdapResponseTranscript.Of(connection.WrittenBytes)[^1]);
    }

    [TestMethod]
    public async Task ServeAsync_StartTlsInsideASecurityLayer_IsAProtectedOperationsError()
    {
        var sasl = new UnitTestSaslAuthenticationPolicy(_ => [Accepted("alice", new UnitTestSecurityLayer())]);
        var bind = SaslBind(1, "X-UNIT");
        var connection = new InMemoryConnection([bind, UnitTestSecurityLayer.ClientBuffer(StartTls(2))]);

        await Server(new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted), sasl, isTlsUpgradeAvailable: true)
            .ServeAsync(connection, Context(TestContext.CancellationToken));

        var bindResponseLength = LdapResponseTranscript.Encodings(connection.WrittenBytes[..14]).Single().Length;
        Assert.IsFalse(connection.UpgradeRequested);
        Assert.AreEqual(
            "#2 extendedResponse operationsError \"a security layer is installed\" [10] 1.3.6.1.4.1.1466.20037",
            LdapResponseTranscript.Of(UnitTestSecurityLayer.ServerMessages(connection.WrittenBytes, bindResponseLength)).Single());
    }

    [TestMethod]
    public async Task ServeAsync_FailedHandshake_ThrowsForTheEngineToNote()
    {
        var connection = new InMemoryConnection([StartTls(1)], upgradeFails: true);

        await Assert.ThrowsExactlyAsync<TlsHandshakeException>(() => Server(new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted), isTlsUpgradeAvailable: true)
            .ServeAsync(connection, Context(TestContext.CancellationToken)));
    }

    [TestMethod]
    public async Task ServeAsync_RootDse_ListsTheOfferForEachTlsStateAndStartTlsOnlyBeforeTheUpgrade()
    {
        var sasl = new UnitTestSaslAuthenticationPolicy(offer: tls => tls is null ? ["GSS-SPNEGO", "NTLM"] : ["GSS-SPNEGO", "NTLM", "PLAIN"]);
        var connection = new InMemoryConnection([RootDse(1), StartTls(2), RootDse(3)]);

        await Server(new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted), sasl, isTlsUpgradeAvailable: true)
            .ServeAsync(connection, Context(TestContext.CancellationToken));

        CollectionAssert.AreEqual(
            new[]
            {
                "#1 searchResultEntry : namingContexts=dc=example,dc=com,o=other; supportedLDAPVersion=3; supportedSASLMechanisms=GSS-SPNEGO,NTLM; supportedExtension=1.3.6.1.4.1.1466.20037",
                "#1 searchResultDone success",
                "#2 extendedResponse success [10] 1.3.6.1.4.1.1466.20037",
                "#3 searchResultEntry : namingContexts=dc=example,dc=com,o=other; supportedLDAPVersion=3; supportedSASLMechanisms=GSS-SPNEGO,NTLM,PLAIN",
                "#3 searchResultDone success",
            },
            LdapResponseTranscript.Of(connection.WrittenBytes).ToArray());
        Assert.IsTrue(sasl.OfferRequests.All(request => request.Scheme == "ldap"));
    }

    [TestMethod]
    public async Task ServeAsync_SaslBindRefusedForPlainText_IsAllowedOverTlsAfterStartTls()
    {
        var sasl = new UnitTestSaslAuthenticationPolicy(
            start => start.TlsSession is null ? [Refused(SaslLoginOutcome.RefusedPlaintext)] : [Accepted("alice")]);
        var connection = new InMemoryConnection([SaslBind(1, "PLAIN"), StartTls(2), SaslBind(3, "PLAIN"), Message(4, Search(Present("objectClass"), scope: 0))]);

        await Server(new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted), sasl, isTlsUpgradeAvailable: true)
            .ServeAsync(connection, Context(TestContext.CancellationToken));

        CollectionAssert.AreEqual(
            new[]
            {
                "#1 bindResponse confidentialityRequired \"SASL mechanism needs TLS or --allow-plaintext-auth\"",
                "#2 extendedResponse success [10] 1.3.6.1.4.1.1466.20037",
                "#3 bindResponse success",
                "#4 searchResultEntry dc=example,dc=com: objectClass=domain; dc=example",
                "#4 searchResultDone success",
            },
            LdapResponseTranscript.Of(connection.WrittenBytes).ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_Ldaps_OffersForTheTlsConnectionAndNoStartTls()
    {
        var sasl = new UnitTestSaslAuthenticationPolicy(offer: tls => tls is null ? [] : ["PLAIN"]);
        var connection = new InMemoryConnection([RootDse(1)], initialTlsSession: InMemoryConnection.DefaultUpgradeTlsSession);

        await Server(new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted), sasl, isTlsUpgradeAvailable: true)
            .ServeAsync(connection, Context(TestContext.CancellationToken, scheme: "ldaps"));

        Assert.AreEqual(
            "#1 searchResultEntry : namingContexts=dc=example,dc=com,o=other; supportedLDAPVersion=3; supportedSASLMechanisms=PLAIN",
            LdapResponseTranscript.Of(connection.WrittenBytes)[0]);
        Assert.AreEqual(new SaslOfferRequest("ldaps", InMemoryConnection.DefaultUpgradeTlsSession), sasl.OfferRequests.Single());
    }

    private static byte[] StartTls(int messageId) => Message(messageId, writer =>
    {
        using var extended = writer.PushSequence(LdapTags.ExtendedRequest);
        Text(writer, "1.3.6.1.4.1.1466.20037", LdapTags.Context(0));
    });

    private static byte[] RootDse(int messageId) => Message(messageId, Search(Present("objectClass"), baseObject: string.Empty, scope: 0, attributes: "+"));

    private static byte[] SaslBind(int messageId, string mechanism) => Message(messageId, writer =>
    {
        using var bind = writer.PushSequence(LdapTags.BindRequest);
        writer.WriteInteger(3);
        Text(writer, string.Empty);
        using var sasl = writer.PushSequence(LdapTags.Context(3, isConstructed: true));
        Text(writer, mechanism);
    });
}
