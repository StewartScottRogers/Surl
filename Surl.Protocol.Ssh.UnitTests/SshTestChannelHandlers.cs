using System.Collections.Concurrent;

namespace Surl.Protocol.Ssh;

/// <summary>
/// Handlers for the channel tests: each accepted request runs <see cref="Run"/>, and every
/// request asked about is recorded, an SCP command as itself and the subsystem as <c>sftp</c>.
/// </summary>
internal sealed class SshTestChannelHandlers : ISshChannelHandlers
{
    public Func<ISshChannelDataStream, CancellationToken, Task<uint>> Run { get; init; } = (_, _) => Task.FromResult(0u);

    public bool ServesScp { get; init; } = true;

    public bool ServesSftp { get; init; } = true;

    public ConcurrentQueue<object> Requests { get; } = new();

    public ISshChannelHandler? ForScp(SshScpCommand command)
    {
        Requests.Enqueue(command);

        return ServesScp ? new Handler(Run) : null;
    }

    public ISshChannelHandler? ForSftp()
    {
        Requests.Enqueue("sftp");

        return ServesSftp ? new Handler(Run) : null;
    }

    private sealed class Handler(Func<ISshChannelDataStream, CancellationToken, Task<uint>> run) : ISshChannelHandler
    {
        public Task<uint> RunAsync(ISshChannelDataStream channel, CancellationToken cancellationToken) => run(channel, cancellationToken);
    }
}
