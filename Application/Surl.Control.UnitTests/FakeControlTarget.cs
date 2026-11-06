using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using Surl.Protocol.Abstractions;

namespace Surl.Control;

/// <summary>
/// An <see cref="IControlTarget"/> that records each call as one line, answers as a test sets it up, and hands out
/// the events a test writes to <see cref="Events"/>.
/// </summary>
internal sealed class FakeControlTarget : IControlTarget
{
    private int snapshotCount;

    /// <summary>Each call, as one line.</summary>
    public List<string> Calls { get; } = [];

    /// <summary>The events <see cref="ReadEventsAsync"/> hands out.</summary>
    public Channel<SurlEvent> Events { get; } = Channel.CreateUnbounded<SurlEvent>();

    /// <summary>What every member but <see cref="ReadEventsAsync"/> throws, or <see langword="null"/>.</summary>
    public Exception? Failure { get; set; }

    /// <summary>What <see cref="ListFaults"/> answers.</summary>
    public List<ControlFault> Faults { get; } = [];

    /// <summary>What the removing members answer.</summary>
    public bool Found { get; set; } = true;

    /// <summary>The snapshots taken, in order.</summary>
    public List<IControlSnapshot> Snapshots { get; } = [];

    /// <summary>The last message published.</summary>
    public ControlMessage? LastMessage { get; private set; }

    public async IAsyncEnumerable<SurlEvent> ReadEventsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var surlEvent in Events.Reader.ReadAllAsync(cancellationToken))
        {
            yield return surlEvent;
        }
    }

    public string AddFault(string item)
    {
        Record($"AddFault {item}");

        return "f7";
    }

    public IReadOnlyList<ControlFault> ListFaults()
    {
        Record("ListFaults");

        return Faults;
    }

    public bool RemoveFault(string faultId)
    {
        Record($"RemoveFault {faultId}");

        return Found;
    }

    public Task SeedMailAsync(string recipient, ReadOnlyMemory<byte> message, CancellationToken cancellationToken)
    {
        Record($"SeedMail {recipient} {Encoding.UTF8.GetString(message.Span)}");

        return Task.CompletedTask;
    }

    public Task WriteFileAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        Record($"WriteFile {path} {Encoding.UTF8.GetString(content.Span)}");

        return Task.CompletedTask;
    }

    public Task<bool> DeleteFileAsync(string path, CancellationToken cancellationToken)
    {
        Record($"DeleteFile {path}");

        return Task.FromResult(Found);
    }

    public Task PublishMessageAsync(ControlMessage message, CancellationToken cancellationToken)
    {
        Record($"PublishMessage {message.Topic ?? "-"} {message.Queue ?? "-"} {Encoding.UTF8.GetString(message.Payload.Span)} {message.Retain}");
        LastMessage = message;

        return Task.CompletedTask;
    }

    public Task AddAccountAsync(string user, string password, CancellationToken cancellationToken)
    {
        Record($"AddAccount {user} {password}");

        return Task.CompletedTask;
    }

    public Task<bool> RemoveAccountAsync(string user, CancellationToken cancellationToken)
    {
        Record($"RemoveAccount {user}");

        return Task.FromResult(Found);
    }

    public Task ResetAsync(CancellationToken cancellationToken)
    {
        Record("Reset");

        return Task.CompletedTask;
    }

    public Task<IControlSnapshot> SnapshotAsync(CancellationToken cancellationToken)
    {
        Record("Snapshot");
        IControlSnapshot snapshot = new FakeSnapshot(++snapshotCount);
        Snapshots.Add(snapshot);

        return Task.FromResult(snapshot);
    }

    public Task RestoreAsync(IControlSnapshot snapshot, CancellationToken cancellationToken)
    {
        Record($"Restore {((FakeSnapshot)snapshot).Number}");

        return Task.CompletedTask;
    }

    private void Record(string call)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        Calls.Add(call);
    }

    /// <summary>A snapshot the fake took, numbered from 1.</summary>
    /// <param name="Number">Its number.</param>
    public sealed record FakeSnapshot(int Number) : IControlSnapshot;
}
