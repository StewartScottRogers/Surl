using System.Formats.Asn1;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;

namespace Surl.Authentication;

/// <summary>
/// <see cref="LdapAccountSource"/> against a directory replayed in memory: ADR-0149 decision 4's
/// exchange, decision 6's refusals, decision 9's reasons and decision 10's bounds.
/// </summary>
[TestClass]
public sealed class LdapAccountSourceTests
{
    private const string Dn = "uid=alice,ou=people,dc=example,dc=com";
    private const string BindSecret = "svc-Secret-9";

    private readonly ManualTimeProvider clock = new();
    private readonly ScriptedLdapDirectory directory = new();
    private readonly List<string> notes = [];

    private static LdapDirectoryUrl Url(string text = "ldap://dir.test/ou=people,dc=example,dc=com")
    {
        Assert.IsTrue(LdapDirectoryUrl.TryParse(text, out var url, out _));

        return url!;
    }

    private LdapAccountSource Source(LdapAccountSourceSettings? settings = null) =>
        new(settings ?? new LdapAccountSourceSettings(Url()), directory, clock, notes.Add);

    private static ReadOnlyMemory<byte> Bytes(string text) => Encoding.UTF8.GetBytes(text);

    // StartTLS (id 1), then the search (id 2) finding one entry, then the user bind (id 3).
    private void Script(int bindCode)
    {
        directory.Answers.Add(LdapTestMessages.ExtendedResponse(1, 0));
        ScriptSearchAndBind(2, bindCode);
    }

    private void ScriptSearchAndBind(int searchId, int bindCode)
    {
        directory.Answers.Add(LdapTestMessages.SearchEntry(searchId, Dn));
        directory.Answers.Add(LdapTestMessages.SearchDone(searchId, 0));
        directory.Answers.Add(LdapTestMessages.BindResponse(searchId + 1, bindCode));
    }

    private ValueTask<LdapLoginResult> CheckAsync(LdapAccountSource source, string user = "alice", string password = "pw") =>
        source.CheckAsync("imap", user, Bytes(password), CancellationToken.None);

    private static (int Id, Asn1Tag Operation, AsnReader Body) Read(byte[] message)
    {
        var outer = new AsnReader(message, AsnEncodingRules.BER).ReadSequence();
        var id = (int)outer.ReadInteger();
        var tag = outer.PeekTag();

        return (id, tag, tag.IsConstructed ? outer.ReadSequence(tag) : outer);
    }

    private static string ReadUserValueOfSearch(byte[] searchMessage)
    {
        var (_, _, search) = Read(searchMessage);
        search.ReadOctetString();
        search.ReadEnumeratedBytes();
        search.ReadEnumeratedBytes();
        search.ReadInteger();
        search.ReadInteger();
        search.ReadBoolean();
        var filter = search.ReadSetOf(new Asn1Tag(TagClass.ContextSpecific, 0, true));
        filter.ReadEncodedValue();
        var equality = filter.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 3, true));
        Assert.AreEqual("uid", Encoding.UTF8.GetString(equality.ReadOctetString()));

        return Encoding.UTF8.GetString(equality.ReadOctetString());
    }

    [TestMethod]
    public async Task RightPassword_IsAccepted_AfterStartTlsSearchBindAndUnbind()
    {
        Script(0);

        var result = await CheckAsync(Source());

        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Reason);
        Assert.AreEqual(0, notes.Count);
        Assert.AreEqual(1, directory.StartTlsCount);
        Assert.AreEqual(1, directory.ConnectCount);
        Assert.AreEqual(new LdapDirectoryEndpoint("dir.test", 389, false), directory.Endpoints[0]);
        CollectionAssert.AreEqual(
            new[] { 23, 3, 0, 2 }, directory.Written.Select(message => Read(message).Operation.TagValue).ToArray());
    }

    [TestMethod]
    public async Task UserBind_IsMadeAsTheEntryFoundWithThePasswordSent()
    {
        Script(0);

        await CheckAsync(Source(), password: "pässword");

        var (id, _, bind) = Read(directory.Written[2]);
        Assert.AreEqual(3, id);
        Assert.AreEqual(3, (int)bind.ReadInteger());
        Assert.AreEqual(Dn, Encoding.UTF8.GetString(bind.ReadOctetString()));
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("pässword"), bind.ReadOctetString(new Asn1Tag(TagClass.ContextSpecific, 0)));
    }

    [TestMethod]
    public async Task Search_AsksForOneUserWithNoAttributesAndTwoEntriesAtMost()
    {
        Script(0);

        await CheckAsync(Source());

        var (id, _, search) = Read(directory.Written[1]);
        Assert.AreEqual(2, id);
        Assert.AreEqual("ou=people,dc=example,dc=com", Encoding.UTF8.GetString(search.ReadOctetString()));
        Assert.AreEqual(2, search.ReadEnumeratedBytes().Span[0]);
        Assert.AreEqual(0, search.ReadEnumeratedBytes().Span[0]);
        Assert.AreEqual(2, (int)search.ReadInteger());
        Assert.AreEqual(5, (int)search.ReadInteger());
        Assert.IsTrue(search.ReadBoolean());
        Assert.AreEqual("alice", ReadUserValueOfSearch(directory.Written[1]));
    }

    [TestMethod]
    public async Task NameWithFilterCharacters_IsMatchedAsAValueNeverAsAFilter()
    {
        directory.Answers.Add(LdapTestMessages.ExtendedResponse(1, 0));
        ScriptSearchAndBind(2, 49);

        await CheckAsync(Source(), user: "al*)(uid=*");

        Assert.AreEqual("al*)(uid=*", ReadUserValueOfSearch(directory.Written[1]));
    }

    [TestMethod]
    public async Task WrongPassword_IsRefusedAndNotedWithoutThePassword()
    {
        Script(49);

        var result = await CheckAsync(Source(), password: "Hunter2!");

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual("wrong password", result.Reason);
        Assert.AreEqual("Login refused: password alice (directory: wrong password)", notes.Single());
        Assert.DoesNotContain("Hunter2!", notes.Single());
    }

    [TestMethod]
    public async Task RefusalWithNoNoteSink_IsStillRefused()
    {
        Script(49);
        var source = new LdapAccountSource(new LdapAccountSourceSettings(Url()), directory, clock);

        var result = await CheckAsync(source);

        Assert.AreEqual("wrong password", result.Reason);
    }

    [TestMethod]
    public async Task AnyOtherBindCode_IsRefusedAndNotedWithItsCode()
    {
        Script(53);

        var result = await CheckAsync(Source());

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual("bind refused, result 53", result.Reason);
    }

    [TestMethod]
    [DataRow("imap", "alice", "pw", DisplayName = "a scheme the directory does not serve")]
    [DataRow("ftp", "alice", "", DisplayName = "an empty password, which a directory may accept unchecked")]
    [DataRow("ftp", "", "pw", DisplayName = "an empty name")]
    [DataRow("ftp", "al\0ice", "pw", DisplayName = "a name holding U+0000")]
    public async Task LoginTheDirectoryIsNotAskedAbout_IsRefusedWithoutContactingIt(string scheme, string user, string password)
    {
        var settings = new LdapAccountSourceSettings(Url()) { Protocols = ["ftp", "pop3"] };

        var result = await Source(settings).CheckAsync(scheme, user, Bytes(password), CancellationToken.None);

        Assert.IsFalse(result.IsAccepted);
        Assert.IsNull(result.Reason);
        Assert.AreEqual(0, directory.ConnectCount);
        Assert.AreEqual(0, notes.Count);
    }

    [TestMethod]
    public async Task NameOf257Bytes_IsRefusedWithoutContactingTheDirectory()
    {
        var result = await CheckAsync(Source(), user: new string('a', LdapAccountSource.MaxUserNameBytes + 1));

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(0, directory.ConnectCount);
    }

    [TestMethod]
    public async Task NameOf256Bytes_IsAskedAbout()
    {
        Script(0);

        var result = await CheckAsync(Source(), user: new string('a', LdapAccountSource.MaxUserNameBytes));

        Assert.IsTrue(result.IsAccepted);
    }

    [TestMethod]
    [DataRow(null, "anything", true, DisplayName = "every scheme")]
    [DataRow("IMAP", "imap", true, DisplayName = "a named scheme, whatever its case")]
    [DataRow("smtp", "imap", false, DisplayName = "a scheme left out")]
    public void Serves_FollowsTheProtocolsKey(string? protocol, string scheme, bool expected)
    {
        var settings = new LdapAccountSourceSettings(Url()) { Protocols = protocol is null ? null : [protocol] };

        Assert.AreEqual(expected, Source(settings).Serves(scheme));
    }

    [TestMethod]
    public async Task Ldaps_StartsTlsAtOnce_AndNeverAsksForStartTls()
    {
        ScriptSearchAndBind(1, 0);
        var settings = new LdapAccountSourceSettings(Url("ldaps://dir.test/dc=example,dc=com"));

        var result = await CheckAsync(Source(settings));

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(new LdapDirectoryEndpoint("dir.test", 636, true), directory.Endpoints[0]);
        Assert.AreEqual(0, directory.StartTlsCount);
        Assert.AreEqual(3, directory.Written.Count);
    }

    [TestMethod]
    public async Task Plaintext_SendsNoStartTls()
    {
        ScriptSearchAndBind(1, 0);
        var settings = new LdapAccountSourceSettings(Url()) { IsPlaintext = true };

        var result = await CheckAsync(Source(settings));

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(0, directory.StartTlsCount);
        Assert.AreEqual(3, directory.Written.Count);
    }

    [TestMethod]
    public async Task ServiceBind_IsSentFirstWithTheBindPassword_AndNeverReachesANote()
    {
        directory.Answers.Add(LdapTestMessages.ExtendedResponse(1, 0));
        directory.Answers.Add(LdapTestMessages.BindResponse(2, 0));
        ScriptSearchAndBind(3, 49);
        var settings = new LdapAccountSourceSettings(Url())
        {
            BindDn = "cn=svc,dc=example,dc=com",
            BindPassword = Bytes(BindSecret),
        };

        var result = await CheckAsync(Source(settings));

        Assert.AreEqual("wrong password", result.Reason);
        var (_, _, bind) = Read(directory.Written[1]);
        bind.ReadInteger();
        Assert.AreEqual("cn=svc,dc=example,dc=com", Encoding.UTF8.GetString(bind.ReadOctetString()));
        Assert.AreEqual(BindSecret, Encoding.UTF8.GetString(bind.ReadOctetString(new Asn1Tag(TagClass.ContextSpecific, 0))));
        Assert.DoesNotContain(BindSecret, notes.Single());
        Assert.DoesNotContain("svc", notes.Single());
    }

    [TestMethod]
    public async Task ServiceBindRefused_IsRefusedAsTheDirectoryUnavailable_AndNeverSearches()
    {
        directory.Answers.Add(LdapTestMessages.ExtendedResponse(1, 0));
        directory.Answers.Add(LdapTestMessages.BindResponse(2, 49));
        var settings = new LdapAccountSourceSettings(Url()) { BindDn = "cn=svc", BindPassword = Bytes(BindSecret) };

        var result = await CheckAsync(Source(settings));

        Assert.AreEqual("service bind refused, result 49", result.Reason);
        Assert.AreEqual(2, directory.Written.Count);
        Assert.DoesNotContain(BindSecret, notes.Single());
    }

    [TestMethod]
    public async Task StartTlsRefused_SendsNothingMore_AndNeverFallsBackToClearText()
    {
        directory.Answers.Add(LdapTestMessages.ExtendedResponse(1, 2));

        var result = await CheckAsync(Source());

        Assert.AreEqual("StartTLS refused, result 2", result.Reason);
        Assert.AreEqual(1, directory.Written.Count);
        Assert.AreEqual(0, directory.StartTlsCount);
    }

    [TestMethod]
    public async Task HandshakeFailure_IsRefusedAsTlsFailed()
    {
        directory.Answers.Add(LdapTestMessages.ExtendedResponse(1, 0));
        directory.StartTlsFailure = new AuthenticationException("certificate for dir.test not trusted");

        var result = await CheckAsync(Source());

        Assert.AreEqual("TLS failed", result.Reason);
        Assert.DoesNotContain("certificate", notes.Single());
    }

    [TestMethod]
    public async Task LdapsHandshakeFailure_IsRefusedAsTlsFailed()
    {
        directory.ConnectFailure = new AuthenticationException("bad");
        var settings = new LdapAccountSourceSettings(Url("ldaps://dir.test/dc=example,dc=com"));

        var result = await CheckAsync(Source(settings));

        Assert.AreEqual("TLS failed", result.Reason);
    }

    [TestMethod]
    public async Task IoFailureOnConnect_IsRefusedAsCouldNotConnect_WithoutEchoingTheException()
    {
        directory.ConnectFailure = new IOException($"refused using {BindSecret}");
        var settings = new LdapAccountSourceSettings(Url()) { BindDn = "cn=svc", BindPassword = Bytes(BindSecret) };

        var result = await CheckAsync(Source(settings));

        Assert.AreEqual("could not connect", result.Reason);
        Assert.DoesNotContain(BindSecret, notes.Single());
    }

    [TestMethod]
    public async Task SocketFailureOnConnect_IsRefusedAsCouldNotConnect()
    {
        directory.ConnectFailure = new SocketException();

        var result = await CheckAsync(Source());

        Assert.AreEqual("could not connect", result.Reason);
    }

    [TestMethod]
    public async Task NoEntry_IsRefused()
    {
        directory.Answers.Add(LdapTestMessages.ExtendedResponse(1, 0));
        directory.Answers.Add(LdapTestMessages.SearchDone(2, 0));

        var result = await CheckAsync(Source());

        Assert.AreEqual("no such entry", result.Reason);
        Assert.AreEqual(2, directory.Written.Count);
    }

    [TestMethod]
    public async Task TwoEntries_AreAmbiguous_AndNeverBound()
    {
        directory.Answers.Add(LdapTestMessages.ExtendedResponse(1, 0));
        directory.Answers.Add(LdapTestMessages.SearchEntry(2, Dn));
        directory.Answers.Add(LdapTestMessages.SearchEntry(2, "uid=alice,ou=other"));

        var result = await CheckAsync(Source());

        Assert.AreEqual("ambiguous entry", result.Reason);
        Assert.AreEqual(2, directory.Written.Count);
    }

    [TestMethod]
    public async Task SizeLimitExceeded_IsAmbiguous()
    {
        directory.Answers.Add(LdapTestMessages.ExtendedResponse(1, 0));
        directory.Answers.Add(LdapTestMessages.SearchEntry(2, Dn));
        directory.Answers.Add(LdapTestMessages.SearchDone(2, 4));

        var result = await CheckAsync(Source());

        Assert.AreEqual("ambiguous entry", result.Reason);
    }

    [TestMethod]
    public async Task ReferralResult_IsNotFollowed()
    {
        directory.Answers.Add(LdapTestMessages.ExtendedResponse(1, 0));
        directory.Answers.Add(LdapTestMessages.SearchDone(2, 10));

        var result = await CheckAsync(Source());

        Assert.AreEqual("referral not followed", result.Reason);
        Assert.AreEqual(1, directory.ConnectCount);
    }

    [TestMethod]
    public async Task ReferenceInsteadOfAnEntry_IsNotFollowed()
    {
        directory.Answers.Add(LdapTestMessages.ExtendedResponse(1, 0));
        directory.Answers.Add(LdapTestMessages.SearchReference(2));
        directory.Answers.Add(LdapTestMessages.SearchDone(2, 0));

        var result = await CheckAsync(Source());

        Assert.AreEqual("referral not followed", result.Reason);
    }

    [TestMethod]
    public async Task ReferenceBesideTheEntry_DoesNotStopTheLogin()
    {
        directory.Answers.Add(LdapTestMessages.ExtendedResponse(1, 0));
        directory.Answers.Add(LdapTestMessages.SearchReference(2));
        directory.Answers.Add(LdapTestMessages.SearchEntry(2, Dn));
        directory.Answers.Add(LdapTestMessages.SearchDone(2, 0));
        directory.Answers.Add(LdapTestMessages.BindResponse(3, 0));

        var result = await CheckAsync(Source());

        Assert.IsTrue(result.IsAccepted);
    }

    [TestMethod]
    public async Task AnyOtherSearchCode_IsRefusedAsTheDirectoryUnavailable()
    {
        directory.Answers.Add(LdapTestMessages.ExtendedResponse(1, 0));
        directory.Answers.Add(LdapTestMessages.SearchDone(2, 32));

        var result = await CheckAsync(Source());

        Assert.AreEqual("search refused, result 32", result.Reason);
    }

    [TestMethod]
    public async Task ConnectionEndingMidExchange_IsRefusedAsMalformed()
    {
        var result = await CheckAsync(Source());

        Assert.AreEqual("malformed answer", result.Reason);
    }

    [TestMethod]
    public async Task WriteFailing_IsRefusedAsMalformed()
    {
        directory.WriteFailsAt = 0;

        var result = await CheckAsync(Source());

        Assert.AreEqual("malformed answer", result.Reason);
    }

    [TestMethod]
    public async Task AnswerWithTheWrongMessageId_IsRefusedAsMalformed()
    {
        directory.Answers.Add(LdapTestMessages.ExtendedResponse(7, 0));

        var result = await CheckAsync(Source());

        Assert.AreEqual("malformed answer", result.Reason);
    }

    [TestMethod]
    public async Task AnswerOfTheWrongKind_IsRefusedAsMalformed()
    {
        directory.Answers.Add(LdapTestMessages.BindResponse(1, 0));

        var result = await CheckAsync(Source());

        Assert.AreEqual("malformed answer", result.Reason);
    }

    [TestMethod]
    public async Task SearchAnswerOfTheWrongKind_IsRefusedAsMalformed()
    {
        directory.Answers.Add(LdapTestMessages.ExtendedResponse(1, 0));
        directory.Answers.Add(LdapTestMessages.BindResponse(2, 0));

        var result = await CheckAsync(Source());

        Assert.AreEqual("malformed answer", result.Reason);
    }

    [TestMethod]
    public async Task SearchAnswerWithTheWrongMessageId_IsRefusedAsMalformed()
    {
        directory.Answers.Add(LdapTestMessages.ExtendedResponse(1, 0));
        directory.Answers.Add(LdapTestMessages.SearchDone(9, 0));

        var result = await CheckAsync(Source());

        Assert.AreEqual("malformed answer", result.Reason);
    }

    [TestMethod]
    public async Task UnbindFailing_AfterTheVerdictIsKnown_ChangesNothing()
    {
        Script(0);
        directory.WriteFailsAt = 3;

        var result = await CheckAsync(Source());

        Assert.IsTrue(result.IsAccepted);
    }

    [TestMethod]
    public async Task DirectoryThatDoesNotAnswer_IsRefusedAsTimedOut_OnTheInjectedClock()
    {
        directory.Hangs = true;
        var check = CheckAsync(Source()).AsTask();

        Assert.IsFalse(check.IsCompleted);
        clock.Advance(TimeSpan.FromSeconds(LdapAccountSourceSettings.DefaultTimeoutSeconds));
        var result = await check;

        Assert.AreEqual("timed out after 5 s", result.Reason);
        Assert.AreEqual("Login refused: password alice (directory: timed out after 5 s)", notes.Single());
    }

    [TestMethod]
    public async Task Timeout_IsTheOneTheSettingsName()
    {
        directory.Hangs = true;
        var check = CheckAsync(Source(new LdapAccountSourceSettings(Url()) { TimeoutSeconds = 2 })).AsTask();

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.IsFalse(check.IsCompleted);
        clock.Advance(TimeSpan.FromSeconds(1));

        Assert.AreEqual("timed out after 2 s", (await check).Reason);
    }

    [TestMethod]
    public async Task CancelledByTheCaller_Throws_AndIsNotNotedAsATimeout()
    {
        directory.Hangs = true;
        using var cancellation = new CancellationTokenSource();
        var check = Source().CheckAsync("imap", "alice", Bytes("pw"), cancellation.Token).AsTask();

        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => check);
        Assert.AreEqual(0, notes.Count);
    }

    [TestMethod]
    public async Task SeventeenthCheck_WaitsForASlot_InsideItsOwnTimeout()
    {
        directory.Hangs = true;
        var source = Source();
        var checks = Enumerable.Range(0, LdapAccountSource.MaxConcurrentChecks + 1)
            .Select(_ => CheckAsync(source).AsTask())
            .ToList();

        Assert.AreEqual(LdapAccountSource.MaxConcurrentChecks, directory.ConnectCount);
        clock.Advance(TimeSpan.FromSeconds(LdapAccountSourceSettings.DefaultTimeoutSeconds));
        var results = await Task.WhenAll(checks);

        Assert.IsTrue(results.All(result => result.Reason == "timed out after 5 s"));
        Assert.AreEqual(LdapAccountSource.MaxConcurrentChecks, directory.ConnectCount);
    }

    [TestMethod]
    public async Task ASlotFreedByAFinishedCheck_IsReused()
    {
        var source = Source();
        for (var round = 0; round < LdapAccountSource.MaxConcurrentChecks + 2; round++)
        {
            directory.Answers.Clear();
            Script(0);

            Assert.IsTrue((await CheckAsync(source)).IsAccepted);
        }
    }

    [TestMethod]
    public void Constructor_RefusesMissingParts()
    {
        var settings = new LdapAccountSourceSettings(Url());

        Assert.Throws<ArgumentNullException>(() => new LdapAccountSource(null!, directory, clock));
        Assert.Throws<ArgumentNullException>(() => new LdapAccountSource(settings, null!, clock));
        Assert.Throws<ArgumentNullException>(() => new LdapAccountSource(settings, directory, null!));
        Assert.Throws<ArgumentNullException>(() => new LdapAccountSourceSettings(null!));
        Assert.Throws<ArgumentNullException>(() => Source().Serves(null!));
    }
}
