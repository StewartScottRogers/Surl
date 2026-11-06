using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Redis;

/// <summary>A connection the client keeps open: what the server wrote so far, and how to end it.</summary>
internal sealed class OpenRedisConnection(InMemoryConnection connection, Task serving, CancellationTokenSource lifetime, RecordingExchangeLog log)
{
    public string Written => RedisTestExchanges.Written(connection);

    public Task Serving { get; } = serving;

    public RecordingExchangeLog Log { get; } = log;

    /// <summary>Ends the exchange as the engine's idle timeout would, and returns what the server wrote.</summary>
    public async Task<string> CloseAsync()
    {
        await lifetime.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => Serving);
        lifetime.Dispose();
        return Written;
    }
}
