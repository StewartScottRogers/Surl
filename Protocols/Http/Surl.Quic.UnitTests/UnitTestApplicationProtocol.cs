namespace Surl.Quic;

/// <summary>An application protocol that runs the test's script and records the connection it was handed.</summary>
internal sealed class UnitTestApplicationProtocol(Func<QuicApplicationConnection, CancellationToken, Task> script) : IQuicApplicationProtocol
{
    private int calls;

    public int Calls => calls;

    public QuicApplicationConnection? Connection { get; private set; }

    public Task ServeAsync(QuicApplicationConnection connection, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref calls);
        Connection = connection;
        return script(connection, cancellationToken);
    }
}
