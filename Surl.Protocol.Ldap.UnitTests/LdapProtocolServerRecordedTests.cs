using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ldap.LdapServerExchange;

namespace Surl.Protocol.Ldap;

/// <summary>
/// Replays what the pinned Windows reference build sent in each recorded case
/// (<c>Fixtures/README.md</c>) and checks what the server answers, as ADR-0072 decisions 2 and 3
/// say. The directory is <see cref="LdapDirectoryFixture.PeopleEntries"/>.
/// </summary>
[TestClass]
public sealed class LdapProtocolServerRecordedTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_RecordedBindAndBaseSearch_IsBoundThenAnsweredTheBaseEntry()
    {
        var policy = new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted);
        var log = new RecordingExchangeLog();

        var connection = await RunAsync(policy, TestContext.CancellationToken, log, RecordedFixture.ReadRequestMessages("simple-bind-base-search"));

        CollectionAssert.AreEqual(
            new[]
            {
                "#1 bindResponse success",
                "#2 searchResultEntry dc=example,dc=com: objectClass=domain; dc=example",
                "#2 searchResultDone success",
            },
            LdapResponseTranscript.Of(connection.WrittenBytes).ToArray());
        CollectionAssert.AreEqual(
            new[] { "Login accepted: simple alice", "LDAP search dc=example,dc=com scope base: 1 entries, success" },
            log.Notes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_RecordedBind_AnswersTheExactBindResponseAndChecksTheLoginOverPlainLdap()
    {
        var policy = new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted);

        var connection = await RunAsync(policy, TestContext.CancellationToken, null, RecordedFixture.ReadRequestMessages("simple-bind-base-search")[0]);

        // messageID 1, BindResponse { success, "", "" }.
        CollectionAssert.AreEqual(Convert.FromHexString("300C02010161070A010004000400"), connection.WrittenBytes);
        var login = policy.Logins.Single();
        Assert.AreEqual("ldap", login.Scheme);
        Assert.AreEqual("alice", login.UserName);
        Assert.AreEqual("secret", Encoding.UTF8.GetString(login.Password!.Value.Span));
        Assert.IsNull(login.TlsSession);
    }

    [TestMethod]
    public async Task ServeAsync_RecordedOneLevelSearch_AnswersEachChildWithTheNamedAttributes()
    {
        var connection = await RunAsync(
            new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted), TestContext.CancellationToken, null, RecordedFixture.ReadRequestMessages("one-level-search"));

        CollectionAssert.AreEqual(
            new[]
            {
                "#1 bindResponse success",
                "#2 searchResultEntry cn=alice,dc=example,dc=com: cn=alice; mail=alice@example.com; cn;lang-fr=alicia",
                "#2 searchResultEntry cn=bob,dc=example,dc=com: cn=bob; mail=bob@other.example",
                "#2 searchResultEntry ou=staff,dc=example,dc=com: ",
                "#2 searchResultDone success",
            },
            LdapResponseTranscript.Of(connection.WrittenBytes).ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_RecordedFilteredSubtreeSearch_AnswersOnlyTheEntriesTheFilterMatches()
    {
        var log = new RecordingExchangeLog();

        var connection = await RunAsync(
            new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted), TestContext.CancellationToken, log, RecordedFixture.ReadRequestMessages("subtree-filtered-search"));

        CollectionAssert.AreEqual(
            new[]
            {
                "#1 bindResponse success",
                "#2 searchResultEntry cn=alice,dc=example,dc=com: cn=alice; cn;lang-fr=alicia",
                "#2 searchResultDone success",
            },
            LdapResponseTranscript.Of(connection.WrittenBytes).ToArray());
        Assert.AreEqual("LDAP search dc=example,dc=com scope sub: 1 entries, success", log.Notes[^1]);
    }

    [TestMethod]
    public async Task ServeAsync_RecordedSearchThatMatchesNothing_AnswersDoneAlone()
    {
        var connection = await RunAsync(
            new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted), TestContext.CancellationToken, null, RecordedFixture.ReadRequestMessages("empty-result-search"));

        CollectionAssert.AreEqual(
            new[] { "#1 bindResponse success", "#2 searchResultDone success" },
            LdapResponseTranscript.Of(connection.WrittenBytes).ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_RecordedSearchOfABaseNotInTheDirectory_AnswersNoSuchObject()
    {
        var connection = await RunAsync(
            new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted), TestContext.CancellationToken, null, RecordedFixture.ReadRequestMessages("no-such-object-search"));

        CollectionAssert.AreEqual(
            new[] { "#1 bindResponse success", "#2 searchResultDone noSuchObject" },
            LdapResponseTranscript.Of(connection.WrittenBytes).ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_RecordedWrongPasswordAndItsVersion2Retry_AreBothInvalidCredentialsThenUnbindCloses()
    {
        var policy = new UnitTestAuthenticationPolicy(PasswordLoginVerdict.RefusedCredentials);
        var log = new RecordingExchangeLog();

        var connection = await RunAsync(policy, TestContext.CancellationToken, log, RecordedFixture.ReadRequestMessages("wrong-password-version-2-retry"));

        CollectionAssert.AreEqual(
            new[] { "#1 bindResponse invalidCredentials", "#2 bindResponse invalidCredentials" },
            LdapResponseTranscript.Of(connection.WrittenBytes).ToArray());
        CollectionAssert.AreEqual(new[] { "Login refused: simple alice", "Login refused: simple alice" }, log.Notes.ToArray());
        Assert.HasCount(2, policy.Logins);
        Assert.IsFalse(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task ServeAsync_RecordedBindWithNoAccountsConfigured_IsInvalidCredentialsLikeAWrongPassword()
    {
        // With no accounts the policy refuses every password as credentials (ADR-0032, section 8):
        // the answer says nothing that differs from a wrong password.
        var connection = await RunAsync(
            new UnitTestAuthenticationPolicy(PasswordLoginVerdict.RefusedCredentials), TestContext.CancellationToken, null, RecordedFixture.ReadRequestMessages("simple-bind-base-search"));

        CollectionAssert.AreEqual(
            new[] { "#1 bindResponse invalidCredentials", "#2 searchResultDone insufficientAccessRights \"bind first\"" },
            LdapResponseTranscript.Of(connection.WrittenBytes).ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_RecordedPlainTextBindWithoutAllowPlaintextAuth_IsConfidentialityRequiredForBothVersions()
    {
        var log = new RecordingExchangeLog();

        var connection = await RunAsync(
            new UnitTestAuthenticationPolicy(PasswordLoginVerdict.RefusedPlaintext), TestContext.CancellationToken, log, RecordedFixture.ReadRequestMessages("plaintext-bind-refused"));

        CollectionAssert.AreEqual(
            new[]
            {
                "#1 bindResponse confidentialityRequired \"simple bind needs TLS or --allow-plaintext-auth\"",
                "#2 bindResponse confidentialityRequired \"simple bind needs TLS or --allow-plaintext-auth\"",
            },
            LdapResponseTranscript.Of(connection.WrittenBytes).ToArray());
        Assert.AreEqual("LDAP bind refused: confidentialityRequired: simple bind needs TLS or --allow-plaintext-auth", log.Notes[0]);
    }

    [TestMethod]
    public async Task ServeAsync_RecordedPlainTextBindWithAllowPlaintextAuth_IsCheckedAndAccepted()
    {
        // --allow-plaintext-auth is the policy's: it checks the password instead of refusing it.
        var connection = await RunAsync(
            new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted), TestContext.CancellationToken, null, RecordedFixture.ReadRequestMessages("plaintext-bind-refused"));

        CollectionAssert.AreEqual(
            new[] { "#1 bindResponse success", "#2 bindResponse success" },
            LdapResponseTranscript.Of(connection.WrittenBytes).ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_RecordedBindUnderAllowAnonymous_IsAcceptedUncheckedWithNoLoginNote()
    {
        var log = new RecordingExchangeLog();

        var connection = await RunAsync(
            new UnitTestAuthenticationPolicy(PasswordLoginVerdict.AcceptedUnchecked), TestContext.CancellationToken, log, RecordedFixture.ReadRequestMessages("simple-bind-base-search"));

        Assert.AreEqual("#2 searchResultDone success", LdapResponseTranscript.Of(connection.WrittenBytes)[^1]);
        CollectionAssert.AreEqual(new[] { "LDAP search dc=example,dc=com scope base: 1 entries, success" }, log.Notes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_RecordedSearchWithNoBindWithoutAllowAnonymous_IsInsufficientAccessRights()
    {
        var search = RecordedFixture.ReadRequestMessages("simple-bind-base-search")[1];
        var policy = new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted, anonymousVerdict: PasswordLoginVerdict.RefusedAnonymous);
        var log = new RecordingExchangeLog();

        var connection = await RunAsync(policy, TestContext.CancellationToken, log, search);

        CollectionAssert.AreEqual(
            new[] { "#2 searchResultDone insufficientAccessRights \"bind first\"" },
            LdapResponseTranscript.Of(connection.WrittenBytes).ToArray());
        Assert.AreEqual("LDAP search dc=example,dc=com scope base: 0 entries, insufficientAccessRights", log.Notes.Single());
        Assert.IsNull(policy.Logins.Single().UserName);
    }

    [TestMethod]
    public async Task ServeAsync_RecordedSearchOfAMissingBaseWithNoBind_SaysNothingAboutTheDirectory()
    {
        var search = RecordedFixture.ReadRequestMessages("no-such-object-search")[1];

        var connection = await RunAsync(new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted), TestContext.CancellationToken, null, search);

        CollectionAssert.AreEqual(
            new[] { "#2 searchResultDone insufficientAccessRights \"bind first\"" },
            LdapResponseTranscript.Of(connection.WrittenBytes).ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_RecordedSearchWithNoBindUnderAllowAnonymous_IsAnswered()
    {
        var search = RecordedFixture.ReadRequestMessages("simple-bind-base-search")[1];
        var policy = new UnitTestAuthenticationPolicy(PasswordLoginVerdict.AcceptedUnchecked, anonymousVerdict: PasswordLoginVerdict.AcceptedUnchecked);

        var connection = await RunAsync(policy, TestContext.CancellationToken, null, search);

        CollectionAssert.AreEqual(
            new[] { "#2 searchResultEntry dc=example,dc=com: objectClass=domain; dc=example", "#2 searchResultDone success" },
            LdapResponseTranscript.Of(connection.WrittenBytes).ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_RecordedRefusedBindThenSearch_LeavesTheConnectionAnonymous()
    {
        var messages = RecordedFixture.ReadRequestMessages("wrong-password-version-2-retry");
        var search = RecordedFixture.ReadRequestMessages("simple-bind-base-search")[1];
        var policy = new UnitTestAuthenticationPolicy(PasswordLoginVerdict.RefusedCredentials);

        var connection = await RunAsync(policy, TestContext.CancellationToken, null, messages[0], search);

        Assert.AreEqual("#2 searchResultDone insufficientAccessRights \"bind first\"", LdapResponseTranscript.Of(connection.WrittenBytes)[^1]);
    }

    [TestMethod]
    public async Task ServeAsync_EveryRecordedCase_NotesNoPassword()
    {
        string[] cases =
        [
            "simple-bind-base-search", "one-level-search", "subtree-filtered-search", "empty-result-search",
            "no-such-object-search", "wrong-password-version-2-retry", "plaintext-bind-refused",
        ];
        PasswordLoginVerdict[] verdicts = [.. Enum.GetValues<PasswordLoginVerdict>()];

        foreach (var caseName in cases)
        {
            foreach (var verdict in verdicts)
            {
                var log = new RecordingExchangeLog();
                await RunAsync(new UnitTestAuthenticationPolicy(verdict), TestContext.CancellationToken, log, RecordedFixture.ReadRequestMessages(caseName));

                Assert.IsFalse(log.Notes.Any(note => note.Contains("secret", StringComparison.Ordinal) || note.Contains("wrong", StringComparison.Ordinal)), $"{caseName} {verdict}");
            }
        }
    }
}
