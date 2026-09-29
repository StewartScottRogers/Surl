using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// Waits on an <see cref="InMemoryConnection"/> for what the engine does to it after an
/// exchange or a refusal ends.
/// </summary>
internal static class InMemoryConnectionWaits
{
    // The engine disposes a connection once its exchange ends; with no server call to await,
    // polling the flag is the only signal a test has.
    public static async Task WaitUntilDisposedAsync(InMemoryConnection connection)
    {
        var deadline = DateTime.UtcNow + Patience.Timeout;

        while (!connection.Disposed && DateTime.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(1));
        }

        Assert.IsTrue(connection.Disposed);
    }
}
