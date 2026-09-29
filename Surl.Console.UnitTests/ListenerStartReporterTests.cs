using Surl.Output;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

[TestClass]
public sealed class ListenerStartReporterTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task StartConnectionListenerAsync_BeforeTheLastListener_WritesNoStatusLine()
    {
        using var output = new StringWriter();
        var reporter = new ListenerStartReporter(new FakeListenerFactory(), new ListenerStatusLine(output), 2);

        await using var listener = await reporter.StartConnectionListenerAsync(
            new ListenUrl("http", "127.0.0.1", 0), TestContext.CancellationToken);

        Assert.AreEqual(string.Empty, output.ToString());
        Assert.IsNull(reporter.BindFailure);
    }

    [TestMethod]
    public async Task StartConnectionListenerAsync_TheLastListener_WritesOneStatusLinePerListenerInStartOrder()
    {
        using var output = new StringWriter();
        var reporter = new ListenerStartReporter(new FakeListenerFactory(), new ListenerStatusLine(output), 2);

        await using var first = await reporter.StartConnectionListenerAsync(
            new ListenUrl("http", "127.0.0.1", 0), TestContext.CancellationToken);
        await using var second = await reporter.StartConnectionListenerAsync(
            new ListenUrl("http", "localhost", 0), TestContext.CancellationToken);

        Assert.AreEqual(
            $"Listening on http://127.0.0.1:{FakeListenerFactory.BoundPort}/{Environment.NewLine}"
            + $"Listening on http://localhost:{FakeListenerFactory.BoundPort}/{Environment.NewLine}",
            output.ToString());
    }

    [TestMethod]
    public async Task StartConnectionListenerAsync_BindFails_KeepsTheFailureAndRethrowsIt()
    {
        using var output = new StringWriter();
        var listenUrl = new ListenUrl("http", "127.0.0.1", 80);
        var failure = new ListenerBindException(listenUrl, null, ListenerBindFailure.PermissionDenied, null);
        var reporter = new ListenerStartReporter(
            new FakeListenerFactory { BindFailure = failure }, new ListenerStatusLine(output), 1);

        var thrown = await Assert.ThrowsExactlyAsync<ListenerBindException>(
            () => reporter.StartConnectionListenerAsync(listenUrl, TestContext.CancellationToken).AsTask());

        Assert.AreSame(failure, thrown);
        Assert.AreSame(failure, reporter.BindFailure);
        Assert.AreEqual(string.Empty, output.ToString());
    }

    [TestMethod]
    public async Task StartDatagramListenerAsync_TheLastListenerAfterAConnectionListener_WritesBothStatusLinesInStartOrder()
    {
        using var output = new StringWriter();
        var reporter = new ListenerStartReporter(new FakeListenerFactory(), new ListenerStatusLine(output), 2);

        await using var first = await reporter.StartConnectionListenerAsync(
            new ListenUrl("http", "127.0.0.1", 0), TestContext.CancellationToken);
        await using var second = await reporter.StartDatagramListenerAsync(
            new ListenUrl("tftp", "127.0.0.1", 0), TestContext.CancellationToken);

        Assert.AreEqual(
            $"Listening on http://127.0.0.1:{FakeListenerFactory.BoundPort}/{Environment.NewLine}"
            + $"Listening on tftp://127.0.0.1:{FakeListenerFactory.BoundPort}/{Environment.NewLine}",
            output.ToString());
        Assert.IsNull(reporter.BindFailure);
    }

    [TestMethod]
    public async Task StartDatagramListenerAsync_BindFails_KeepsTheFailureAndRethrowsIt()
    {
        using var output = new StringWriter();
        var listenUrl = new ListenUrl("tftp", "127.0.0.1", 69);
        var failure = new ListenerBindException(listenUrl, null, ListenerBindFailure.AddressInUse, null);
        var reporter = new ListenerStartReporter(
            new FakeListenerFactory { BindFailure = failure }, new ListenerStatusLine(output), 1);

        var thrown = await Assert.ThrowsExactlyAsync<ListenerBindException>(
            () => reporter.StartDatagramListenerAsync(listenUrl, TestContext.CancellationToken).AsTask());

        Assert.AreSame(failure, thrown);
        Assert.AreSame(failure, reporter.BindFailure);
        Assert.AreEqual(string.Empty, output.ToString());
    }
}
