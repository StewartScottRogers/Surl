using Surl.Composition;
using Surl.Content;

namespace Surl.Api;

/// <summary>
/// What the tests start servers through: a <see cref="FakeListenerFactory"/>, a data directory that
/// always opens, an in-memory file system for it, <c>SURL_EXTENSIONS=0</c>, and a lock table shared by
/// every start made from one instance, so a second start on a held data directory is refused as
/// <see cref="DataDirectoryLock"/> refuses it, without the disk.
/// </summary>
internal sealed class ApiTestSeams
{
    private readonly HashSet<string> heldDataDirectories = new(StringComparer.Ordinal);

    public ApiTestSeams(FakeListenerFactory? listenerFactory = null) =>
        ListenerFactory = listenerFactory ?? new FakeListenerFactory();

    /// <summary>The factory every start's listeners are started through.</summary>
    public FakeListenerFactory ListenerFactory { get; }

    /// <summary>The host seams.</summary>
    public SurlServerHostSeams Host => new(
        _ => ListenerFactory, _ => true, TakeDataDirectoryLock, FakeEnvironmentVariableReader.ExtensionsTurnedOff)
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

    /// <summary>Options with one listen URL for each of <paramref name="listenUrls"/>.</summary>
    public static SurlServerOptions Options(params string[] listenUrls)
    {
        var options = new SurlServerOptions();
        foreach (var listenUrl in listenUrls)
        {
            options.ListenUrls.Add(listenUrl);
        }

        return options;
    }

    private DataDirectoryLockOutcome TakeDataDirectoryLock(string dataDirectory)
    {
        lock (heldDataDirectories)
        {
            return heldDataDirectories.Add(dataDirectory)
                ? DataDirectoryLockOutcome.Taken(new Release(this, dataDirectory))
                : DataDirectoryLockOutcome.InUse(dataDirectory);
        }
    }

    private sealed class Release(ApiTestSeams owner, string dataDirectory) : IDisposable
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
