using Surl.Content;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Smb.SmbTestExchange;

namespace Surl.Protocol.Smb;

/// <summary>
/// ADR-0073 decisions 1 to 5 for the negotiate, the session setup and the trees: each answer's
/// bytes built field by field from [MS-CIFS], and each refusal's status.
/// </summary>
[TestClass]
public sealed class SmbProtocolServerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Schemes_AreSmbAndSmbs()
    {
        CollectionAssert.AreEqual(new[] { "smb", "smbs" }, Server().Schemes.ToArray());
    }

    [TestMethod]
    public void Constructor_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new SmbProtocolServer(null!, new SecretPasswordPolicy()));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SmbProtocolServer(StandardContentStore(), null!));
    }

    [TestMethod]
    public async Task ServeAsync_NullArguments_Throw()
    {
        var context = Context(new ManualTimeProvider(), TestContext.CancellationToken);
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Server().ServeAsync(null!, context));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Server().ServeAsync(new InMemoryConnection([]), null!));
    }

    [TestMethod]
    public async Task ServeAsync_WithTheSystemChallengeSource_SendsAnEightByteChallengeThatDiffersPerConnection()
    {
        var server = new SmbProtocolServer(StandardContentStore(), new SecretPasswordPolicy());
        var challenges = new List<string>();
        for (var connectionNumber = 0; connectionNumber < 2; connectionNumber++)
        {
            var connection = new InMemoryConnection([Hex(NegotiateHex)]);
            await server.ServeAsync(connection, Context(new ManualTimeProvider(), TestContext.CancellationToken));
            var written = Convert.ToHexString(connection.WrittenBytes);
            Assert.AreEqual("08", written.Substring(2 * (4 + 32 + 34), 2));
            challenges.Add(written.Substring(2 * (4 + 32 + 37), 16));
        }

        Assert.AreNotEqual(challenges[0], challenges[1]);
    }

    [TestMethod]
    public async Task Negotiate_IsAnsweredWithTheAdrsFields()
    {
        var written = await ServeAsync(TestContext.CancellationToken, [NegotiateHex]);

        Assert.AreEqual(NegotiateResponseHex, written);
    }

    [TestMethod]
    [DataRow(4096L, "00100000")]
    [DataRow(0L, "FFFF0100")]
    [DataRow(131071L, "FFFF0100")]
    [DataRow(131070L, "FEFF0100")]
    public async Task Negotiate_MaxBufferSize_IsMaxMessageCappedAt131071(long maxMessageBytes, string maxBufferSizeHex)
    {
        var limits = ExchangeLimits.Default with { MaxMessageBytes = maxMessageBytes };

        var written = await ServeAsync(TestContext.CancellationToken, [NegotiateHex], limits: limits);

        Assert.AreEqual(maxBufferSizeHex, written.Substring(2 * (4 + 32 + 8), 8));
    }

    [TestMethod]
    public async Task Negotiate_WithoutNtLm012_IsAnsweredWordCountOneFFFFAndClosed()
    {
        var dialects = "\u0002PC NETWORK PROGRAM 1.0\0";
        var request = RequestHex(SmbCommand.Negotiate, 0, 0, "00" + "1800" + Convert.ToHexString(System.Text.Encoding.ASCII.GetBytes(dialects)));

        var connection = new InMemoryConnection([Hex(request + NegotiateHex)]);
        await Server().ServeAsync(connection, Context(new ManualTimeProvider(), TestContext.CancellationToken));

        Assert.AreEqual(ResponseHex(SmbCommand.Negotiate, 0, 0, 0, "01FFFF0000"), Convert.ToHexString(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task Negotiate_ASecondTime_IsServerErrorAndTheSessionGoesOn()
    {
        var written = await ServeAsync(TestContext.CancellationToken, [NegotiateHex, NegotiateHex, SessionSetupHex]);

        Assert.AreEqual(
            NegotiateResponseHex + ErrorHex(SmbCommand.Negotiate, SmbStatus.ServerError, 0, 0) + SessionSetupResponseHex,
            written);
    }

    [TestMethod]
    public async Task SessionSetup_BeforeNegotiate_IsServerError()
    {
        var written = await ServeAsync(TestContext.CancellationToken, [SessionSetupHex]);

        Assert.AreEqual(ErrorHex(SmbCommand.SessionSetupAndX, SmbStatus.ServerError, 0, 0), written);
    }

    [TestMethod]
    public async Task SessionSetup_ASecondTime_IsServerError()
    {
        var written = await ServeAsync(TestContext.CancellationToken, [NegotiateHex, SessionSetupHex, SessionSetupHex]);

        Assert.AreEqual(NegotiateResponseHex + SessionSetupResponseHex + ErrorHex(SmbCommand.SessionSetupAndX, SmbStatus.ServerError, 0, 0), written);
    }

    [TestMethod]
    public async Task SessionSetup_AcceptedUnchecked_IsAnsweredAsGuestWithNoNote()
    {
        var log = new RecordingExchangeLog();

        var written = await ServeAsync(TestContext.CancellationToken, [NegotiateHex, SessionSetupHex], new AnonymousAuthenticationPolicy(), log);

        Assert.AreEqual(NegotiateResponseHex + SessionSetupResponseHex.Replace("03FF00000000000700", "03FF00000001000700", StringComparison.Ordinal), written);
        Assert.IsEmpty(log.Notes);
    }

    [TestMethod]
    public async Task SessionSetup_RefusedWithNothingChecked_NotesNtlmV1NotInAuthAndClosesWithoutReadingOn()
    {
        var log = new RecordingExchangeLog();
        var policy = new FixedVerdictPolicy(new SmbLoginVerdict(SmbLoginOutcome.Refused, null, null));

        var written = await ServeAsync(TestContext.CancellationToken, [NegotiateHex, SessionSetupHex, TreeConnectHex("share")], policy, log);

        Assert.AreEqual(NegotiateResponseHex + ErrorHex(SmbCommand.SessionSetupAndX, SmbStatus.BadPassword, 0, 0), written);
        CollectionAssert.AreEqual(new[] { "SMB session setup refused: ntlmv1 is not in --auth" }, log.Notes.ToArray());
    }

    [TestMethod]
    public async Task SessionSetup_AnOutcomeTheServerDoesNotKnow_IsRefused()
    {
        var log = new RecordingExchangeLog();
        var policy = new FixedVerdictPolicy(new SmbLoginVerdict((SmbLoginOutcome)99, "alice", new CheckedLogin("ntlmv1", "alice", true)));

        var written = await ServeAsync(TestContext.CancellationToken, [NegotiateHex, SessionSetupHex], policy, log);

        Assert.AreEqual(NegotiateResponseHex + ErrorHex(SmbCommand.SessionSetupAndX, SmbStatus.BadPassword, 0, 0), written);
        CollectionAssert.AreEqual(new[] { "Login accepted: ntlmv1 alice" }, log.Notes.ToArray());
    }

    [TestMethod]
    public async Task SessionSetup_OnTls_HandsThePolicyTheTlsSession()
    {
        var policy = new SecretPasswordPolicy();
        var tlsSession = InMemoryConnection.DefaultUpgradeTlsSession;
        var connection = new InMemoryConnection([Hex(NegotiateHex + SessionSetupHex)], initialTlsSession: tlsSession);

        await Server(policy).ServeAsync(connection, Context(new ManualTimeProvider(), TestContext.CancellationToken));

        Assert.AreSame(tlsSession, policy.Logins.Single().TlsSession);
    }

    [TestMethod]
    public async Task TreeConnect_BeforeLogin_IsBadUserId()
    {
        var written = await ServeAsync(TestContext.CancellationToken, [NegotiateHex, TreeConnectHex("share")]);

        Assert.AreEqual(NegotiateResponseHex + ErrorHex(SmbCommand.TreeConnectAndX, SmbStatus.BadUserId, 0), written);
    }

    [TestMethod]
    public async Task TreeConnect_WithAnotherUserId_IsBadUserId()
    {
        var written = await ServeAsync(TestContext.CancellationToken, [NegotiateHex, SessionSetupHex, TreeConnectHex("share", userId: 2)]);

        Assert.AreEqual(NegotiateResponseHex + SessionSetupResponseHex + ErrorHex(SmbCommand.TreeConnectAndX, SmbStatus.BadUserId, 0, 2), written);
    }

    [TestMethod]
    [DataRow("nosuch")]
    [DataRow(".hidden")]
    [DataRow("afile.txt")]
    [DataRow("IPC$")]
    [DataRow("..")]
    [DataRow("")]
    public async Task TreeConnect_ToWhatIsNotATopLevelDirectory_IsInvalidNetworkNameAndNoted(string share)
    {
        var log = new RecordingExchangeLog();

        var written = await ServeAsync(TestContext.CancellationToken, [NegotiateHex, SessionSetupHex, TreeConnectHex(share)], log: log);

        Assert.AreEqual(NegotiateResponseHex + SessionSetupResponseHex + ErrorHex(SmbCommand.TreeConnectAndX, SmbStatus.InvalidNetworkName, 0), written);
        Assert.AreEqual($"SMB tree connect {share}: no such share", log.Notes[^1]);
    }

    [TestMethod]
    [DataRow("IPC$")]
    [DataRow("ipc$")]
    public async Task TreeConnect_ToIpcShare_IsInvalidNetworkNameEvenWhenTheStoreHoldsThatDirectory(string share)
    {
        var fileSystem = StandardFileSystem();
        fileSystem.CreateDirectory(Path.Join(InMemoryContentFileSystem.RootPath, share));
        fileSystem.CreateDirectory(Path.Join(InMemoryContentFileSystem.RootPath, "dollar$"));
        var contentStore = new ContentStore(InMemoryContentFileSystem.RootPath, fileSystem, new ContentExposureOptions());
        var log = new RecordingExchangeLog();

        var written = await ServeAsync(
            TestContext.CancellationToken, [NegotiateHex, SessionSetupHex, TreeConnectHex(share), TreeConnectHex("dollar$")], log: log, contentStore: contentStore);

        Assert.AreEqual(
            NegotiateResponseHex + SessionSetupResponseHex + ErrorHex(SmbCommand.TreeConnectAndX, SmbStatus.InvalidNetworkName, 0) + TreeConnectResponseHex(1),
            written);
        Assert.AreEqual($"SMB tree connect {share}: no such share", log.Notes[^2]);
    }

    [TestMethod]
    public async Task TreeConnect_TheSeventeenthTree_IsServerError()
    {
        var connects = Enumerable.Repeat(TreeConnectHex("share"), 17).ToArray();

        var written = await ServeAsync(TestContext.CancellationToken, [NegotiateHex, SessionSetupHex, .. connects]);

        var expected = NegotiateResponseHex + SessionSetupResponseHex
            + string.Concat(Enumerable.Range(1, 16).Select(treeId => TreeConnectResponseHex((ushort)treeId)))
            + ErrorHex(SmbCommand.TreeConnectAndX, SmbStatus.ServerError, 0);
        Assert.AreEqual(expected, written);
    }

    [TestMethod]
    public async Task TreeDisconnect_FreesTheTreeAndItsTreeIdIsGivenOutAgain()
    {
        var written = await ServeConnectedAsync(
            TestContext.CancellationToken, null, TreeConnectHex("share"), TreeDisconnectHex(1), TreeDisconnectHex(1), TreeConnectHex("share"));

        Assert.AreEqual(
            TreeConnectResponseHex(2)
            + ResponseHex(SmbCommand.TreeDisconnect, 0, 1, SmbSession.UserId)
            + ErrorHex(SmbCommand.TreeDisconnect, SmbStatus.InvalidTreeId)
            + TreeConnectResponseHex(1),
            written);
    }

    [TestMethod]
    public async Task NtCreate_OnAConnectedTree_IsBadFileAndNoted()
    {
        var log = new RecordingExchangeLog();

        var written = await ServeConnectedAsync(TestContext.CancellationToken, log, NtCreateHex(1));

        Assert.AreEqual(ErrorHex(SmbCommand.NtCreateAndX, SmbStatus.BadFile), written);
        Assert.AreEqual(@"SMB open share\dir\file.txt refused: ERRbadfile", log.Notes[^1]);
    }

    [TestMethod]
    public async Task NtCreate_OnATreeIdNotConnected_IsInvalidTreeId()
    {
        var written = await ServeConnectedAsync(TestContext.CancellationToken, null, NtCreateHex(5));

        Assert.AreEqual(ErrorHex(SmbCommand.NtCreateAndX, SmbStatus.InvalidTreeId, 5), written);
    }

    [TestMethod]
    public async Task ReadWriteAndClose_WithNoFileOpen_AreBadFileId()
    {
        var written = await ServeConnectedAsync(TestContext.CancellationToken, null, ReadHex(1), WriteHex(1), CloseHex(1));

        Assert.AreEqual(
            ErrorHex(SmbCommand.ReadAndX, SmbStatus.BadFileId) + ErrorHex(SmbCommand.WriteAndX, SmbStatus.BadFileId) + ErrorHex(SmbCommand.Close, SmbStatus.BadFileId),
            written);
    }

    [TestMethod]
    public async Task ACommandCurlNeverSends_IsUnsupportedCommandAndNoted()
    {
        var log = new RecordingExchangeLog();

        var written = await ServeConnectedAsync(TestContext.CancellationToken, log, RequestHex(0x2B, 1, SmbSession.UserId, "000000"));

        Assert.AreEqual(ErrorHex(0x2B, SmbStatus.UnsupportedCommand), written);
        Assert.AreEqual("SMB command 0x2b refused: not supported", log.Notes[^1]);
    }

    [TestMethod]
    public async Task AMalformedMessage_IsServerErrorAndTheSessionGoesOn()
    {
        var written = await ServeConnectedAsync(
            TestContext.CancellationToken, null, RequestHex(SmbCommand.TreeDisconnect, 1, SmbSession.UserId, "0100000000"), TreeDisconnectHex(1));

        Assert.AreEqual(ErrorHex(SmbCommand.TreeDisconnect, SmbStatus.ServerError) + ResponseHex(SmbCommand.TreeDisconnect, 0, 1, SmbSession.UserId), written);
    }

    [TestMethod]
    [DataRow("0000000400000000")]
    [DataRow("00000020FE534D4200000000000000000000000000000000000000000000000000000000")]
    public async Task AMessageThatIsNotSmb_ClosesTheConnectionWithNothingSent(string frameHex)
    {
        var written = await ServeAsync(TestContext.CancellationToken, [NegotiateHex, frameHex, SessionSetupHex]);

        Assert.AreEqual(NegotiateResponseHex, written);
    }

    [TestMethod]
    public async Task AKeepAlive_IsIgnored()
    {
        var written = await ServeAsync(TestContext.CancellationToken, ["85000000", NegotiateHex, "85000000", SessionSetupHex]);

        Assert.AreEqual(NegotiateResponseHex + SessionSetupResponseHex, written);
    }

    [TestMethod]
    public async Task ASessionRequestFrame_ClosesTheConnection()
    {
        var written = await ServeAsync(TestContext.CancellationToken, [NegotiateHex, "8100000420202020", SessionSetupHex]);

        Assert.AreEqual(NegotiateResponseHex, written);
    }

    [TestMethod]
    public async Task TheClientClosingPartWayThroughAMessage_EndsTheExchangeQuietly()
    {
        var log = new RecordingExchangeLog();

        var written = await ServeAsync(TestContext.CancellationToken, [NegotiateHex, SessionSetupHex[..40]], log: log);

        Assert.AreEqual(NegotiateResponseHex, written);
        Assert.IsEmpty(log.Notes);
    }
}
