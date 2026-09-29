using System.Net.Sockets;

namespace Surl.Networking;

[TestClass]
public sealed class AcceptRaceTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task AcceptNextAsync_TwoSources_ReturnsWhicheverAcceptsFirst()
    {
        var sources = new FakeSources(2);
        var race = sources.CreateRace();

        var next = race.AcceptNextAsync(TestContext.CancellationToken);
        sources.Accept(1, "from 1");

        Assert.AreEqual("from 1", await next);
    }

    [TestMethod]
    public async Task AcceptNextAsync_AnotherSourceStillWaiting_KeepsItsAcceptForTheNextCall()
    {
        var sources = new FakeSources(2);
        var race = sources.CreateRace();

        var first = race.AcceptNextAsync(TestContext.CancellationToken);
        sources.Accept(1, "from 1");
        await first;
        var second = race.AcceptNextAsync(TestContext.CancellationToken);
        sources.Accept(0, "from 0");

        Assert.AreEqual("from 0", await second);
        CollectionAssert.AreEqual(new[] { 1, 2 }, sources.StartCounts);
    }

    [TestMethod]
    public async Task AcceptNextAsync_AlreadyCancelled_ThrowsWithoutStartingAnAccept()
    {
        var sources = new FakeSources(1);
        var race = sources.CreateRace();
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => race.AcceptNextAsync(source.Token));

        CollectionAssert.AreEqual(new[] { 0 }, sources.StartCounts);
    }

    [TestMethod]
    public async Task AcceptNextAsync_CancelledWhileWaiting_ThrowsAndHandsTheLaterAcceptToTheNextCall()
    {
        var sources = new FakeSources(1);
        var race = sources.CreateRace();
        using var source = new CancellationTokenSource();

        var cancelled = race.AcceptNextAsync(source.Token);
        source.Cancel();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => cancelled);
        sources.Accept(0, "late");

        Assert.AreEqual("late", await race.AcceptNextAsync(TestContext.CancellationToken));
        CollectionAssert.AreEqual(new[] { 1 }, sources.StartCounts);
    }

    [TestMethod]
    public async Task AcceptNextAsync_AcceptFailsWithAListenerFatalSocketError_ThrowsIOExceptionAndStartsAnotherAcceptNextTime()
    {
        var sources = new FakeSources(1);
        var race = sources.CreateRace();
        var failure = new SocketException((int)SocketError.TooManyOpenSockets);

        var failed = race.AcceptNextAsync(TestContext.CancellationToken);
        sources.Fail(0, failure);
        var exception = await Assert.ThrowsExactlyAsync<IOException>(() => failed);
        var next = race.AcceptNextAsync(TestContext.CancellationToken);
        sources.Accept(0, "retried");

        Assert.AreSame(failure, exception.InnerException);
        Assert.AreEqual("retried", await next);
        CollectionAssert.AreEqual(new[] { 2 }, sources.StartCounts);
    }

    [TestMethod]
    [DataRow(SocketError.ConnectionReset)]
    [DataRow(SocketError.ConnectionAborted)]
    public async Task AcceptNextAsync_AcceptFailsForOneClient_AcceptsAgainAndReturnsTheNextClient(SocketError socketError)
    {
        var sources = new FakeSources(1);
        var race = sources.CreateRace();

        var next = race.AcceptNextAsync(TestContext.CancellationToken);
        sources.Fail(0, new SocketException((int)socketError));
        await sources.WaitForStartAsync(0, 2, TestContext.CancellationToken);
        sources.Accept(0, "next client");

        Assert.AreEqual("next client", await next);
        CollectionAssert.AreEqual(new[] { 2 }, sources.StartCounts);
    }

    [TestMethod]
    public async Task AcceptNextAsync_AcceptedSocketLost_AcceptsAgainAndReturnsTheNextClient()
    {
        var sources = new FakeSources(1);
        var race = sources.CreateRace();

        var next = race.AcceptNextAsync(TestContext.CancellationToken);
        sources.Fail(0, new AcceptedSocketLostException(new SocketException((int)SocketError.InvalidArgument)));
        await sources.WaitForStartAsync(0, 2, TestContext.CancellationToken);
        sources.Accept(0, "next client");

        Assert.AreEqual("next client", await next);
    }

    [TestMethod]
    public async Task AcceptNextAsync_OneClientFailsThenTheListenerFails_ThrowsIOExceptionForTheListener()
    {
        var sources = new FakeSources(1);
        var race = sources.CreateRace();
        var listenerFailure = new SocketException((int)SocketError.NotSocket);

        var next = race.AcceptNextAsync(TestContext.CancellationToken);
        sources.Fail(0, new SocketException((int)SocketError.ConnectionReset));
        await sources.WaitForStartAsync(0, 2, TestContext.CancellationToken);
        sources.Fail(0, listenerFailure);

        var exception = await Assert.ThrowsExactlyAsync<IOException>(() => next);
        Assert.AreSame(listenerFailure, exception.InnerException);
    }

    [TestMethod]
    public async Task AcceptNextAsync_OneClientFailsWhileAnotherSourceAccepts_ReturnsTheOtherSourcesClient()
    {
        var sources = new FakeSources(2);
        var race = sources.CreateRace();

        var next = race.AcceptNextAsync(TestContext.CancellationToken);
        sources.Fail(0, new SocketException((int)SocketError.ConnectionAborted));
        await sources.WaitForStartAsync(0, 2, TestContext.CancellationToken);
        sources.Accept(1, "from 1");

        Assert.AreEqual("from 1", await next);
        CollectionAssert.AreEqual(new[] { 2, 1 }, sources.StartCounts);
    }

    [TestMethod]
    public async Task AcceptNextAsync_AcceptFailsWithAnotherException_ThrowsIt()
    {
        var sources = new FakeSources(1);
        var race = sources.CreateRace();

        var failed = race.AcceptNextAsync(TestContext.CancellationToken);
        sources.Fail(0, new InvalidOperationException());

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => failed);
    }

    [TestMethod]
    public void StopAsync_CancelsTheTokenEveryAcceptWasStartedWith()
    {
        var sources = new FakeSources(2);
        var race = sources.CreateRace();
        _ = race.AcceptNextAsync(TestContext.CancellationToken);

        _ = race.StopAsync();

        Assert.IsTrue(sources.Tokens.All(token => token.IsCancellationRequested));
    }

    [TestMethod]
    public async Task StopAsync_AcceptedButUnclaimed_ReleasesIt()
    {
        var sources = new FakeSources(2);
        var race = sources.CreateRace();
        var first = race.AcceptNextAsync(TestContext.CancellationToken);
        sources.Accept(0, "claimed");
        await first;
        sources.Accept(1, "unclaimed");

        await race.StopAsync();

        CollectionAssert.AreEqual(new[] { "unclaimed" }, sources.Released);
    }

    [TestMethod]
    public async Task StopAsync_AcceptCompletesAfterStop_ReleasesItAndTheWaitingCallThrows()
    {
        var sources = new FakeSources(1);
        var race = sources.CreateRace();
        var pending = race.AcceptNextAsync(TestContext.CancellationToken);

        var stopping = race.StopAsync();
        sources.Accept(0, "too late");

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => pending);
        await stopping;
        CollectionAssert.AreEqual(new[] { "too late" }, sources.Released);
    }

    [TestMethod]
    public async Task StopAsync_PendingAcceptFailsOrIsCancelled_ReleasesNothingAndDoesNotThrow()
    {
        var sources = new FakeSources(2) { CancelWhenTokenIs = true };
        var race = sources.CreateRace();
        _ = race.AcceptNextAsync(TestContext.CancellationToken);
        sources.Fail(1, new SocketException((int)SocketError.OperationAborted));

        await race.StopAsync();

        Assert.IsEmpty(sources.Released);
    }

    [TestMethod]
    public async Task StopAsync_NoAcceptEverStarted_ReleasesNothing()
    {
        var sources = new FakeSources(1);
        var race = sources.CreateRace();

        await race.StopAsync();

        Assert.IsEmpty(sources.Released);
        CollectionAssert.AreEqual(new[] { 0 }, sources.StartCounts);
    }

    [TestMethod]
    public async Task StopAsync_Twice_IsHarmless()
    {
        var race = new FakeSources(1).CreateRace();

        await race.StopAsync();
        await race.StopAsync();
    }

    [TestMethod]
    public async Task AcceptNextAsync_AfterStop_ThrowsObjectDisposedException()
    {
        var race = new FakeSources(1).CreateRace();
        await race.StopAsync();

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(
            () => race.AcceptNextAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public void Constructor_NoSource_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new AcceptRace<string>(0, (_, _) => Task.FromResult(string.Empty), _ => { }));
    }

    [TestMethod]
    public void Constructor_NullAccept_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new AcceptRace<string>(1, null!, _ => { }));
    }

    [TestMethod]
    public void Constructor_NullRelease_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => new AcceptRace<string>(1, (_, _) => Task.FromResult(string.Empty), null!));
    }

    private sealed class FakeSources(int count)
    {
        private readonly TaskCompletionSource<string>?[] current = new TaskCompletionSource<string>?[count];
        private readonly Lock gate = new();
        private TaskCompletionSource startedAnother = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int[] StartCounts { get; } = new int[count];

        public List<CancellationToken> Tokens { get; } = [];

        public List<string> Released { get; } = [];

        public bool CancelWhenTokenIs { get; init; }

        public AcceptRace<string> CreateRace() => new(count, Start, Released.Add);

        public void Accept(int index, string accepted) => current[index]!.SetResult(accepted);

        public void Fail(int index, Exception exception) => current[index]!.SetException(exception);

        // Waits until the source with the given index has had an accept started on it
        // startCount times; the race restarts an accept on its own continuation.
        public async Task WaitForStartAsync(int index, int startCount, CancellationToken cancellationToken)
        {
            while (true)
            {
                Task started;
                lock (gate)
                {
                    if (StartCounts[index] >= startCount)
                    {
                        return;
                    }

                    started = startedAnother.Task;
                }

                await started.WaitAsync(cancellationToken);
            }
        }

        private Task<string> Start(int index, CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            current[index] = completion;
            Tokens.Add(cancellationToken);
            lock (gate)
            {
                StartCounts[index]++;
                startedAnother.SetResult();
                startedAnother = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            if (CancelWhenTokenIs)
            {
                cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
            }

            return completion.Task;
        }
    }
}
