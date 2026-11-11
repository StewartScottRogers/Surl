using System.Security.Cryptography;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Quic;

/// <summary>
/// Drives a <see cref="QuicDatagramFlowServer"/> over an <see cref="InMemoryDatagramFlow"/> with the
/// test client, starting from the Initials pinned upstream curl sent (ADR-0003), on a clock the test
/// moves.
/// </summary>
[TestClass]
public sealed partial class QuicDatagramFlowServerTests
{
    /// <summary>curl's ALPN list, <c>h3</c> then <c>h3-29</c>, as its ClientHello carries it.</summary>
    private const string RecordedAlpnList = "0268330568332d3239";

    /// <summary>The same nine bytes offering <c>doq</c> then <c>hq-i</c>.</summary>
    private const string DoqAlpnList = "03646f710468712d69";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_HandshakeAndOneStream_TheApplicationReceivesTheConnectionAndExchangesTheStreamsBytes()
    {
        using var key = RSA.Create(2048);
        using var client = new UnitTestFlowClient();
        var received = new List<byte>();
        var protocol = new UnitTestApplicationProtocol(async (connection, token) =>
        {
            var stream = (await connection.AcceptStreamAsync(token))!;
            await ReadToEndAsync(connection, stream, received, token);
            await connection.WriteAsync(stream, "pong"u8.ToArray(), fin: true, token);
        });

        var serving = client.Server(UnitTestCertificates.Options(key), protocol).ServeAsync(client.Flow, TestContext.CancellationToken);
        await client.CompleteHandshakeAsync(TestContext.CancellationToken);
        client.SendOneRtt(new QuicStreamFrame(0, 0, "ping"u8.ToArray(), Fin: true));
        await client.FinishAsync(serving, TestContext.CancellationToken);

        Assert.AreEqual(1, protocol.Calls);
        Assert.AreEqual("h3", protocol.Connection!.ApplicationProtocol);
        Assert.AreEqual((Surl.Tls13.TlsCipherSuite)client.Client.Tls.CipherSuite, protocol.Connection.CipherSuite);
        Assert.AreEqual((ulong)262144, protocol.Connection.PeerTransportParameters.InitialMaxStreamsBidi);
        Assert.AreEqual("ping", Encoding.ASCII.GetString([.. received]));
        var (data, fin) = client.StreamSent(0);
        Assert.AreEqual("pong", Encoding.ASCII.GetString(data));
        Assert.IsTrue(fin);
        var close = client.OneRttFrames<QuicConnectionCloseFrame>().Single();
        Assert.IsTrue(close.IsApplicationError);
        Assert.AreEqual(0UL, close.ErrorCode);
        Assert.IsTrue(protocol.Connection.Ended.IsCancellationRequested);
    }

    [TestMethod]
    public async Task ServeAsync_OptionsNameDoqAndTheClientOffersIt_AgreesDoq()
    {
        using var key = RSA.Create(2048);
        using var client = new UnitTestFlowClient(UnitTestClientHelloEdits.Replace(RecordedAlpnList, DoqAlpnList));
        var protocol = new UnitTestApplicationProtocol((_, _) => Task.CompletedTask);
        var options = DoqOptions(key);

        var serving = client.Server(options, protocol).ServeAsync(client.Flow, TestContext.CancellationToken);
        await client.CompleteHandshakeAsync(TestContext.CancellationToken);
        await client.FinishAsync(serving, TestContext.CancellationToken);

        Assert.AreEqual("doq", protocol.Connection!.ApplicationProtocol);
        Assert.AreEqual("doq", client.Client.Tls.ApplicationProtocol);
    }

    [TestMethod]
    public async Task ServeAsync_OptionsNameDoqAndTheClientOffersOnlyH3_EndsWithNoApplicationProtocolAndNeverStartsTheApplication()
    {
        using var key = RSA.Create(2048);
        using var client = new UnitTestFlowClient();
        var protocol = new UnitTestApplicationProtocol((_, _) => Task.CompletedTask);

        var serving = client.Server(DoqOptions(key), protocol).ServeAsync(client.Flow, TestContext.CancellationToken);
        await client.FinishAsync(serving, TestContext.CancellationToken);

        // The client's later Initials may reach the closing state, which answers with the same close again.
        var closes = client.Client.FramesReceived[QuicPacketType.Initial].OfType<QuicConnectionCloseFrame>().ToList();
        Assert.IsNotEmpty(closes);
        Assert.IsTrue(closes.All(close => close.ErrorCode == 0x0178UL));
        Assert.AreEqual(0, protocol.Calls);
    }

    [TestMethod]
    public async Task ServeAsync_HandshakeTimeoutPassesOnTheClock_ClosesWithNoErrorAndNeverStartsTheApplication()
    {
        using var key = RSA.Create(2048);
        using var client = new UnitTestFlowClient();
        var protocol = new UnitTestApplicationProtocol((_, _) => Task.CompletedTask);
        var options = new QuicServerConnectionOptions
        {
            CertificateChain = [UnitTestCertificates.SelfSigned(key)],
            CertificateKey = key,
            HandshakeTimeout = TimeSpan.FromSeconds(5),
        };

        var serving = client.Server(options, protocol).ServeAsync(client.Flow, TestContext.CancellationToken);
        await client.UntilAsync(() => client.Client.ClientFinished is not null && client.Time.PendingTimerCount > 0, TestContext.CancellationToken);
        client.Time.Advance(TimeSpan.FromSeconds(5));
        await client.FinishAsync(serving, TestContext.CancellationToken);

        var close = client.Client.FramesReceived[QuicPacketType.Handshake].OfType<QuicConnectionCloseFrame>().Single();
        Assert.IsFalse(close.IsApplicationError);
        Assert.AreEqual(0UL, close.ErrorCode);
        Assert.AreEqual(0, protocol.Calls);
    }

    [TestMethod]
    public async Task ServeAsync_ApplicationClosesWithItsCode_SendsItThenRefusesWritesReadsWhatCameAndEnds()
    {
        using var key = RSA.Create(2048);
        using var client = new UnitTestFlowClient();
        Exception? writeAfterClose = null;
        var readsAfterClose = new List<int>();
        QuicStream? acceptAfterClose = null;
        var protocol = new UnitTestApplicationProtocol(async (connection, token) =>
        {
            var stream = (await connection.AcceptStreamAsync(token))!;
            await connection.CloseAsync(0x100, token);
            await connection.CloseAsync(0x101, token);
            writeAfterClose = await CatchAsync(() => connection.WriteAsync(stream, "x"u8.ToArray(), fin: false, CancellationToken.None).AsTask());
            readsAfterClose.Add(await connection.ReadAsync(stream, new byte[4], CancellationToken.None));
            readsAfterClose.Add(await connection.ReadAsync(stream, new byte[4], CancellationToken.None));
            acceptAfterClose = await connection.AcceptStreamAsync(CancellationToken.None);
        });

        var serving = client.Server(UnitTestCertificates.Options(key), protocol).ServeAsync(client.Flow, TestContext.CancellationToken);
        await client.CompleteHandshakeAsync(TestContext.CancellationToken);
        client.SendOneRtt(new QuicStreamFrame(0, 0, "ab"u8.ToArray(), Fin: false));
        await client.FinishAsync(serving, TestContext.CancellationToken);

        var close = client.OneRttFrames<QuicConnectionCloseFrame>().Single();
        Assert.IsTrue(close.IsApplicationError);
        Assert.AreEqual(0x100UL, close.ErrorCode);
        Assert.IsInstanceOfType<OperationCanceledException>(writeAfterClose);
        CollectionAssert.AreEqual(new[] { 2, 0 }, readsAfterClose);
        Assert.IsNull(acceptAfterClose);
    }

    [TestMethod]
    public async Task ServeAsync_ApplicationThrows_ClosesWithApplicationErrorZeroAndThrowsItsException()
    {
        using var key = RSA.Create(2048);
        using var client = new UnitTestFlowClient();
        var protocol = new UnitTestApplicationProtocol((_, _) => throw new InvalidDataException("the application failed"));

        var serving = client.Server(UnitTestCertificates.Options(key), protocol).ServeAsync(client.Flow, TestContext.CancellationToken);
        await client.CompleteHandshakeAsync(TestContext.CancellationToken);

        var thrown = await Assert.ThrowsExactlyAsync<InvalidDataException>(() => client.FinishAsync(serving, TestContext.CancellationToken));
        Assert.AreEqual("the application failed", thrown.Message);
        await client.UntilAsync(() => client.OneRttFrames<QuicConnectionCloseFrame>().Any(), TestContext.CancellationToken);
        Assert.AreEqual(0UL, client.OneRttFrames<QuicConnectionCloseFrame>().Single().ErrorCode);
    }

    [TestMethod]
    public async Task ServeAsync_ClientClosesTheConnection_CancelsTheApplicationAndEndsWithoutError()
    {
        using var key = RSA.Create(2048);
        using var client = new UnitTestFlowClient();
        var protocol = new UnitTestApplicationProtocol((_, token) => Task.Delay(Timeout.InfiniteTimeSpan, token));

        var serving = client.Server(UnitTestCertificates.Options(key), protocol).ServeAsync(client.Flow, TestContext.CancellationToken);
        await client.CompleteHandshakeAsync(TestContext.CancellationToken);
        await client.UntilAsync(() => protocol.Calls == 1, TestContext.CancellationToken);
        client.SendOneRtt(new QuicConnectionCloseFrame(true, 0, 0, ReadOnlyMemory<byte>.Empty));
        await client.FinishAsync(serving, TestContext.CancellationToken);

        Assert.IsTrue(protocol.Connection!.Ended.IsCancellationRequested);
        Assert.IsFalse(client.OneRttFrames<QuicConnectionCloseFrame>().Any());
    }

    [TestMethod]
    public async Task ServeAsync_ClientSendsInTheClosingState_AnswersWithTheConnectionCloseAgainAndEndsOnceTheClockPassesTheStatesEnd()
    {
        using var key = RSA.Create(2048);
        using var client = new UnitTestFlowClient();
        var protocol = new UnitTestApplicationProtocol((connection, token) => connection.CloseAsync(0x100, token).AsTask());

        var serving = client.Server(UnitTestCertificates.Options(key), protocol).ServeAsync(client.Flow, TestContext.CancellationToken);
        await client.CompleteHandshakeAsync(TestContext.CancellationToken);
        await client.UntilAsync(() => client.OneRttFrames<QuicConnectionCloseFrame>().Any(), TestContext.CancellationToken);
        var sentBeforeTheLostClose = client.Flow.SentDatagrams.Count;
        client.SendOneRtt(new QuicPingFrame());
        await client.UntilAsync(() => client.Flow.SentDatagrams.Count > sentBeforeTheLostClose, TestContext.CancellationToken);
        var completedBeforeTheStateEnded = serving.IsCompleted;
        client.Time.Advance(TimeSpan.FromMinutes(1));
        await serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        var sent = client.Flow.SentDatagrams;
        CollectionAssert.AreEqual(sent[sentBeforeTheLostClose - 1], sent[sentBeforeTheLostClose]);
        Assert.HasCount(sentBeforeTheLostClose + 1, sent);
        Assert.IsFalse(completedBeforeTheStateEnded);
        Assert.AreEqual(0x100UL, client.OneRttFrames<QuicConnectionCloseFrame>().First().ErrorCode);
    }

    [TestMethod]
    public async Task ServeAsync_ClientClosesTheConnection_CancelsEndedBeforeTheDrainingStateEnds()
    {
        using var key = RSA.Create(2048);
        using var client = new UnitTestFlowClient();
        var protocol = new UnitTestApplicationProtocol((connection, _) => Task.Delay(Timeout.InfiniteTimeSpan, connection.Ended));

        var serving = client.Server(UnitTestCertificates.Options(key), protocol).ServeAsync(client.Flow, TestContext.CancellationToken);
        await client.CompleteHandshakeAsync(TestContext.CancellationToken);
        await client.UntilAsync(() => protocol.Calls == 1, TestContext.CancellationToken);
        client.SendOneRtt(new QuicConnectionCloseFrame(true, 0, 0, ReadOnlyMemory<byte>.Empty));
        await client.UntilAsync(() => protocol.Connection!.Ended.IsCancellationRequested, TestContext.CancellationToken);
        var completedWhenEnded = serving.IsCompleted;
        client.Time.Advance(TimeSpan.FromMinutes(1));
        await serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        Assert.IsFalse(completedWhenEnded);
        Assert.IsFalse(client.OneRttFrames<QuicConnectionCloseFrame>().Any());
    }

    [TestMethod]
    public async Task ServeAsync_CancelledInTheClosingState_ThrowsOperationCanceledAtOnce()
    {
        using var key = RSA.Create(2048);
        using var client = new UnitTestFlowClient();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var protocol = new UnitTestApplicationProtocol((connection, token) => connection.CloseAsync(0x100, token).AsTask());

        var serving = client.Server(UnitTestCertificates.Options(key), protocol).ServeAsync(client.Flow, cancellation.Token);
        await client.CompleteHandshakeAsync(TestContext.CancellationToken);
        await client.UntilAsync(() => client.OneRttFrames<QuicConnectionCloseFrame>().Any(), TestContext.CancellationToken);
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ServeAsync_CancelledDuringTheHandshake_ThrowsOperationCanceled()
    {
        using var key = RSA.Create(2048);
        using var client = new UnitTestFlowClient();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var protocol = new UnitTestApplicationProtocol((_, _) => Task.CompletedTask);

        var serving = client.Server(UnitTestCertificates.Options(key), protocol).ServeAsync(client.Flow, cancellation.Token);
        await client.UntilAsync(() => client.Client.ClientFinished is not null, TestContext.CancellationToken);
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => client.FinishAsync(serving, TestContext.CancellationToken));
        Assert.AreEqual(0, protocol.Calls);
    }

    [TestMethod]
    public async Task ServeAsync_FirstDatagramStartsNoConnection_WaitsWithNoDeadlineAndStartsOnTheClientsInitial()
    {
        using var key = RSA.Create(2048);
        using var client = new UnitTestFlowClient(strayFirstDatagram: [0x00]);
        var protocol = new UnitTestApplicationProtocol((_, _) => Task.CompletedTask);

        var serving = client.Server(UnitTestCertificates.Options(key), protocol).ServeAsync(client.Flow, TestContext.CancellationToken);
        await client.CompleteHandshakeAsync(TestContext.CancellationToken);
        await client.FinishAsync(serving, TestContext.CancellationToken);

        Assert.AreEqual(1, protocol.Calls);
        Assert.AreEqual("h3", protocol.Connection!.ApplicationProtocol);
        Assert.AreEqual((Surl.Tls13.TlsCipherSuite)client.Client.Tls.CipherSuite, protocol.Connection.CipherSuite);
    }

    [TestMethod]
    public async Task ServeAsync_CancelledWhileTheApplicationRuns_EndsTheApplicationsCallsAndThrowsOperationCanceled()
    {
        using var key = RSA.Create(2048);
        using var client = new UnitTestFlowClient();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var running = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Exception? openAfterEnd = null;
        var protocol = new UnitTestApplicationProtocol(async (connection, _) =>
        {
            running.SetResult();

            // Ended, not the application's token: the token is cancelled the moment serving is,
            // a beat before the loop ends the application's side, so a call made on the token's
            // cancellation could still go through.
            await CatchAsync(() => Task.Delay(Timeout.InfiniteTimeSpan, connection.Ended));
            openAfterEnd = await CatchAsync(() => connection.OpenUnidirectionalStreamAsync(CancellationToken.None).AsTask());
        });

        var serving = client.Server(UnitTestCertificates.Options(key), protocol).ServeAsync(client.Flow, cancellation.Token);
        await client.CompleteHandshakeAsync(TestContext.CancellationToken);
        await running.Task.WaitAsync(TestContext.CancellationToken);
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => client.FinishAsync(serving, TestContext.CancellationToken));
        Assert.IsInstanceOfType<OperationCanceledException>(openAfterEnd);
        Assert.IsFalse(client.OneRttFrames<QuicConnectionCloseFrame>().Any());
    }

    [TestMethod]
    public async Task ServeAsync_NullFlow_ThrowsArgumentNull()
    {
        using var key = RSA.Create(2048);
        var server = new QuicDatagramFlowServer(UnitTestCertificates.Options(key), new UnitTestApplicationProtocol((_, _) => Task.CompletedTask), TimeProvider.System, RandomNumberGenerator.Create());

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(null!, TestContext.CancellationToken));
    }

    [TestMethod]
    public void Constructor_NullArgument_ThrowsArgumentNull()
    {
        using var key = RSA.Create(2048);
        var options = UnitTestCertificates.Options(key);
        var protocol = new UnitTestApplicationProtocol((_, _) => Task.CompletedTask);
        using var random = RandomNumberGenerator.Create();

        Assert.ThrowsExactly<ArgumentNullException>(() => new QuicDatagramFlowServer(null!, protocol, TimeProvider.System, random));
        Assert.ThrowsExactly<ArgumentNullException>(() => new QuicDatagramFlowServer(options, null!, TimeProvider.System, random));
        Assert.ThrowsExactly<ArgumentNullException>(() => new QuicDatagramFlowServer(options, protocol, null!, random));
        Assert.ThrowsExactly<ArgumentNullException>(() => new QuicDatagramFlowServer(options, protocol, TimeProvider.System, null!));
    }

    private static QuicServerConnectionOptions DoqOptions(RSA key) => new()
    {
        CertificateChain = [UnitTestCertificates.SelfSigned(key)],
        CertificateKey = key,
        ApplicationProtocols = ["doq"],
    };

    private static async Task ReadToEndAsync(QuicApplicationConnection connection, QuicStream stream, List<byte> received, CancellationToken token)
    {
        var buffer = new byte[16];
        for (var count = await connection.ReadAsync(stream, buffer, token); count > 0; count = await connection.ReadAsync(stream, buffer, token))
        {
            received.AddRange(buffer[..count]);
        }
    }

    private static async Task<Exception?> CatchAsync(Func<Task> act)
    {
        try
        {
            await act();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }
}
