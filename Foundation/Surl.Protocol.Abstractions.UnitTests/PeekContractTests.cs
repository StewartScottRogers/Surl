using System.Net;

namespace Surl.Protocol.Abstractions;

/// <summary>
/// <see cref="IConnection.PeekByteAsync"/>'s default implementation (ADR-0113, decision 6;
/// BL-734): a connection that does not implement the peek says so.
/// </summary>
[TestClass]
public sealed class PeekContractTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task PeekByteAsync_ConnectionWithoutAPeek_ThrowsNotSupportedExceptionNamingIt()
    {
        IConnection connection = new PeeklessConnection();

        var failure = await Assert.ThrowsExactlyAsync<NotSupportedException>(
            async () => await connection.PeekByteAsync(TimeSpan.Zero, TestContext.CancellationToken));

        StringAssert.Contains(failure.Message, nameof(PeeklessConnection));
    }

    private sealed class PeeklessConnection : IConnection
    {
        public EndPoint LocalEndPoint => throw new NotSupportedException();

        public EndPoint RemoteEndPoint => throw new NotSupportedException();

        public TlsSession? TlsSession => null;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public void Abort() => throw new NotSupportedException();

        public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
