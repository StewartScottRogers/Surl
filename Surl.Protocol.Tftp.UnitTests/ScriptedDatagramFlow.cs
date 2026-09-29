using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Tftp;

/// <summary>
/// A hand-written in-memory <see cref="IDatagramFlow"/>: its first datagram, then a script of
/// the client's datagrams and silences, played one step per receive. A silence advances the
/// <see cref="ManualTimeProvider"/>; if that cuts the receive off, the receive throws, and
/// otherwise the next step is played. Past the end of the script the client is silent for an
/// hour at a time. Every datagram sent is recorded with the local port it left from.
/// </summary>
internal sealed class ScriptedDatagramFlow(byte[] firstDatagram, ManualTimeProvider clock, IEnumerable<ScriptedDatagramFlow.Step> script) : IDatagramFlow
{
    public const int ListenPort = 18069;
    public const int TransferPort = 50001;

    private readonly Queue<Step> steps = new(script);

    public EndPoint LocalEndPoint { get; private set; } = new IPEndPoint(IPAddress.Loopback, ListenPort);

    public EndPoint RemoteEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 50000);

    public ReadOnlyMemory<byte> FirstDatagram { get; } = firstDatagram;

    public List<(int FromPort, byte[] Bytes)> Sent { get; } = [];

    public IEnumerable<byte[]> SentBytes => Sent.Select(sent => sent.Bytes);

    public int RemainingSteps => steps.Count;

    public static Step Datagram(byte[] bytes) => new(bytes, TimeSpan.Zero);

    public static Step Silence(TimeSpan duration) => new(null, duration);

    public ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var step = steps.Count > 0 ? steps.Dequeue() : Silence(TimeSpan.FromHours(1));
            if (step.Bytes is { } bytes)
            {
                return ValueTask.FromResult<ReadOnlyMemory<byte>>(bytes);
            }

            clock.Advance(step.Duration);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Sent.Add((((IPEndPoint)LocalEndPoint).Port, datagram.ToArray()));
        return ValueTask.CompletedTask;
    }

    public ValueTask MoveToNewLocalPortAsync(CancellationToken cancellationToken)
    {
        LocalEndPoint = new IPEndPoint(IPAddress.Loopback, TransferPort);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// One step of the script: a datagram from the client, or a silence of a duration.
    /// </summary>
    internal sealed record Step(byte[]? Bytes, TimeSpan Duration);
}
