using System.Text;
using System.Threading.Channels;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// What a <see cref="FakeRefusalWritingProtocolServer"/> was asked to refuse.
/// </summary>
internal sealed record WrittenRefusal(IConnection Connection, ConnectionRefusal Refusal, CancellationToken CancellationToken);

/// <summary>
/// A connection protocol server that also writes refusals: it serves exchanges as the
/// <see cref="FakeConnectionProtocolServer"/> it wraps, records every refusal it is asked to
/// write, and writes <c>refused:&lt;refusal&gt;</c> unless the test scripted other behaviour.
/// </summary>
internal sealed class FakeRefusalWritingProtocolServer(
    FakeConnectionProtocolServer inner,
    Func<IConnection, ConnectionRefusal, CancellationToken, Task>? writeRefusal = null)
    : IConnectionProtocolServer, IConnectionRefusalWriter
{
    private readonly Channel<WrittenRefusal> refusals = Channel.CreateUnbounded<WrittenRefusal>();

    public IReadOnlyList<string> Schemes => inner.Schemes;

    public FakeConnectionProtocolServer Inner => inner;

    /// <summary>
    /// Waits for the next refusal the server was asked to write.
    /// </summary>
    public Task<WrittenRefusal> NextRefusalAsync() =>
        refusals.Reader.ReadAsync().AsTask().WaitAsync(Patience.Timeout);

    public bool TryTakeRefusal(out WrittenRefusal? refusal) => refusals.Reader.TryRead(out refusal);

    public Task ServeAsync(IConnection connection, ExchangeContext context) => inner.ServeAsync(connection, context);

    public async ValueTask WriteRefusalAsync(IConnection connection, ConnectionRefusal refusal, CancellationToken cancellationToken)
    {
        refusals.Writer.TryWrite(new WrittenRefusal(connection, refusal, cancellationToken));

        if (writeRefusal is null)
        {
            await connection.WriteAsync(Encoding.ASCII.GetBytes($"refused:{refusal}"), cancellationToken);

            return;
        }

        await writeRefusal(connection, refusal, cancellationToken);
    }
}
