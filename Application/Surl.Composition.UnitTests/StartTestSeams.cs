using Surl.Cli;
using Surl.Content;
using Surl.Output;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// What the start tests hand <see cref="SurlServerComposition.ComposeAsync"/>: a
/// <see cref="FakeListenerFactory"/>, a data directory that always opens, an in-memory file system
/// for it, and a lock table shared by every start made from one instance, so a second start on a
/// held data directory is refused as <see cref="DataDirectoryLock"/> refuses it, without the disk.
/// </summary>
internal sealed class StartTestSeams
{
    private readonly HashSet<string> heldDataDirectories = new(StringComparer.Ordinal);

    public StartTestSeams(FakeListenerFactory? listenerFactory = null) =>
        ListenerFactory = listenerFactory ?? new FakeListenerFactory();

    /// <summary>The factory every start's listeners are started through.</summary>
    public FakeListenerFactory ListenerFactory { get; }

    /// <summary>The seams, with extensions off and every optional seam its default but the file systems.</summary>
    public SurlServerCompositionSeams Seams => new(
        _ => ListenerFactory, _ => true, TakeDataDirectoryLock, TimeProvider.System, AreExtensionsTurnedOn: false)
    {
        DataDirectoryFileSystem = new InMemoryContentFileSystem(TimeProvider.System),
    };

    /// <summary>Whether a start holds <paramref name="dataDirectory"/>'s lock now.</summary>
    public bool IsHeld(string dataDirectory)
    {
        lock (heldDataDirectories)
        {
            return heldDataDirectories.Contains(dataDirectory);
        }
    }

    /// <summary>Parses <paramref name="args"/>, failing the test when the command line is refused.</summary>
    public static SurlCommandLine Parse(params string[] args) =>
        CommandLineParser.Parse(args).CommandLine ?? throw new AssertFailedException("The command line was refused.");

    /// <summary>Outputs that write the log and the status lines to <paramref name="log"/> and record the bound URLs.</summary>
    public static SurlServingOutputs Outputs(TextWriter log, Action<IReadOnlyList<ListenUrl>>? allListenersBound = null) =>
        new(log, log, new LevelledExchangeLogFactory(log, LogLevel.Info, false, TimeProvider.System), allListenersBound ?? (_ => { }));

    private DataDirectoryLockOutcome TakeDataDirectoryLock(string dataDirectory)
    {
        lock (heldDataDirectories)
        {
            return heldDataDirectories.Add(dataDirectory)
                ? DataDirectoryLockOutcome.Taken(new Release(this, dataDirectory))
                : DataDirectoryLockOutcome.InUse(dataDirectory);
        }
    }

    private sealed class Release(StartTestSeams owner, string dataDirectory) : IDisposable
    {
        public void Dispose()
        {
            lock (owner.heldDataDirectories)
            {
                owner.heldDataDirectories.Remove(dataDirectory);
            }
        }
    }
}
