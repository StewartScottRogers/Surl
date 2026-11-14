using Surl.Protocol.Abstractions;

namespace Surl.LineProtocol;

/// <summary>
/// BL-1137: upstream's <c>SLOWDOWN</c> writes one byte at a time, 10 ms apart; an unpaced
/// connection writes at once.
/// </summary>
[TestClass]
public sealed class BytePacedConnectionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new BytePacedConnection(null!, new ManualTimeProvider()));
        Assert.ThrowsExactly<ArgumentNullException>(() => new BytePacedConnection(new InMemoryConnection([]), null!));
    }

    [TestMethod]
    public async Task WriteAsync_Paced_WritesOneByteThenWaitsTenMillisecondsForEachByte()
    {
        var inner = new InMemoryConnection([]);
        var clock = new ManualTimeProvider();
        await using var paced = new BytePacedConnection(inner, clock);

        var write = paced.WriteAsync("abc"u8.ToArray(), TestContext.CancellationToken).AsTask();
        await WaitForTimerAsync(clock, 1);
        Assert.AreEqual("a", System.Text.Encoding.ASCII.GetString(inner.WrittenBytes));
        Assert.IsFalse(write.IsCompleted);

        clock.Advance(TimeSpan.FromMilliseconds(9));
        Assert.AreEqual("a", System.Text.Encoding.ASCII.GetString(inner.WrittenBytes));

        clock.Advance(TimeSpan.FromMilliseconds(1));
        await WaitForTimerAsync(clock, 1);
        Assert.AreEqual("ab", System.Text.Encoding.ASCII.GetString(inner.WrittenBytes));

        clock.Advance(BytePacedConnection.ByteInterval);
        await WaitForTimerAsync(clock, 1);
        Assert.AreEqual("abc", System.Text.Encoding.ASCII.GetString(inner.WrittenBytes));

        clock.Advance(BytePacedConnection.ByteInterval);
        await write;
    }

    [TestMethod]
    public async Task WriteAsync_Unpaced_WritesAtOnce()
    {
        var inner = new InMemoryConnection([]);

        await inner.WriteAsync("abc"u8.ToArray(), TestContext.CancellationToken);

        Assert.AreEqual("abc", System.Text.Encoding.ASCII.GetString(inner.WrittenBytes));
    }

    [TestMethod]
    public async Task WriteAsync_Empty_WritesNothingAndDoesNotWait()
    {
        var inner = new InMemoryConnection([]);
        await using var paced = new BytePacedConnection(inner, new ManualTimeProvider());

        await paced.WriteAsync(ReadOnlyMemory<byte>.Empty, TestContext.CancellationToken);

        Assert.IsEmpty(inner.WrittenBytes);
    }

    [TestMethod]
    public async Task EverythingButWrite_GoesStraightToTheWrappedConnection()
    {
        var inner = new InMemoryConnection(["xy"u8.ToArray()]);
        var paced = new BytePacedConnection(inner, new ManualTimeProvider());

        Assert.AreSame(inner.LocalEndPoint, paced.LocalEndPoint);
        Assert.AreSame(inner.RemoteEndPoint, paced.RemoteEndPoint);
        Assert.IsNull(paced.TlsSession);
        Assert.AreEqual((byte)'x', await paced.PeekByteAsync(TimeSpan.Zero, TestContext.CancellationToken));
        var buffer = new byte[2];
        Assert.AreEqual(2, await paced.ReadAsync(buffer, TestContext.CancellationToken));
        Assert.AreSame(InMemoryConnection.DefaultUpgradeTlsSession, await paced.UpgradeToTlsAsync(TestContext.CancellationToken));
        Assert.AreSame(InMemoryConnection.DefaultUpgradeTlsSession, paced.TlsSession);
        await paced.CompleteWritesAsync(TestContext.CancellationToken);
        Assert.IsTrue(inner.WritesCompleted);
        paced.Abort();
        Assert.IsTrue(inner.Aborted);
        await paced.DisposeAsync();
        Assert.IsTrue(inner.Disposed);
    }

    // Waits until the paced write is parked on its delay, so the test never races the writer.
    private async Task WaitForTimerAsync(ManualTimeProvider clock, int count)
    {
        while (clock.PendingTimerCount < count)
        {
            await Task.Yield();
            TestContext.CancellationToken.ThrowIfCancellationRequested();
        }
    }
}
