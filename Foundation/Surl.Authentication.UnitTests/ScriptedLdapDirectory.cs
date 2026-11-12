
namespace Surl.Authentication;

/// <summary>
/// A directory replayed in memory: it answers with the bytes it is given, in order, records every byte
/// written to it, and can fail or hang where a test asks.
/// </summary>
internal sealed class ScriptedLdapDirectory : ILdapDirectoryConnector
{
    private readonly List<LdapDirectoryEndpoint> endpoints = [];
    private readonly List<byte[]> written = [];

    public List<byte[]> Answers { get; } = [];

    public Exception? ConnectFailure { get; set; }

    public Exception? StartTlsFailure { get; set; }

    public bool Hangs { get; set; }

    public TaskCompletionSource? Gate { get; set; }

    public int WriteFailsAt { get; set; } = -1;

    public int StartTlsCount { get; private set; }

    public int ConnectCount => endpoints.Count;

    public IReadOnlyList<LdapDirectoryEndpoint> Endpoints => endpoints;

    public IReadOnlyList<byte[]> Written => written;

    public async ValueTask<ILdapDirectoryConnection> ConnectAsync(LdapDirectoryEndpoint endpoint, CancellationToken cancellationToken)
    {
        endpoints.Add(endpoint);
        if (Gate is not null)
        {
            await Gate.Task.WaitAsync(cancellationToken);
        }

        return ConnectFailure is null ? new Connection(this) : throw ConnectFailure;
    }

    private sealed class Connection(ScriptedLdapDirectory owner) : ILdapDirectoryConnection
    {
        public Stream Stream { get; } = new ScriptedStream(owner);

        public ValueTask StartTlsAsync(CancellationToken cancellationToken)
        {
            owner.StartTlsCount++;

            return owner.StartTlsFailure is null ? ValueTask.CompletedTask : ValueTask.FromException(owner.StartTlsFailure);
        }

        public ValueTask DisposeAsync() => Stream.DisposeAsync();
    }

    private sealed class ScriptedStream(ScriptedLdapDirectory owner) : Stream
    {
        private readonly MemoryStream answers = new(owner.Answers.SelectMany(answer => answer).ToArray());

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (owner.Hangs)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            return await answers.ReadAsync(buffer, cancellationToken);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (owner.written.Count == owner.WriteFailsAt)
            {
                return ValueTask.FromException(new IOException("scripted"));
            }

            owner.written.Add(buffer.ToArray());

            return ValueTask.CompletedTask;
        }

        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
