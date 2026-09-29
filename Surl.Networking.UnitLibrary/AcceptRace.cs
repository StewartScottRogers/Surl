using System.Net.Sockets;

namespace Surl.Networking;

/// <summary>
/// Accepts from several sources at once and hands out whichever accepts first: one listen
/// URL can bind several addresses (ADR-0004, section 6), and a client may connect to any of
/// them. An accept still waiting when a call ends, or cut off by its token, stays in place
/// for the next call, so no accepted connection is lost. The accepts themselves are the
/// caller's, so this runs without a socket in the fast tests.
/// </summary>
/// <typeparam name="TAccepted">What an accept produces; an accepted <see cref="Socket"/> in production.</typeparam>
/// <remarks>Meant for one caller at a time: <see cref="AcceptNextAsync"/> is not safe for concurrent calls.</remarks>
internal sealed class AcceptRace<TAccepted>
{
    private readonly Func<int, CancellationToken, Task<TAccepted>> acceptFrom;
    private readonly Action<TAccepted> releaseUnclaimed;
    private readonly Task<TAccepted>?[] pendingAccepts;
    private readonly CancellationTokenSource lifetime = new();
    private readonly Lock gate = new();
    private Task? stopping;

    /// <summary>
    /// Creates a race over <paramref name="sourceCount"/> sources.
    /// </summary>
    /// <param name="sourceCount">How many sources there are, at least one.</param>
    /// <param name="acceptFrom">
    /// Starts one accept on the source with the given index; the token is cancelled by <see cref="StopAsync"/>.
    /// </param>
    /// <param name="releaseUnclaimed">Releases something accepted after <see cref="StopAsync"/>, which nobody will claim.</param>
    public AcceptRace(int sourceCount, Func<int, CancellationToken, Task<TAccepted>> acceptFrom, Action<TAccepted> releaseUnclaimed)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sourceCount, 1);
        ArgumentNullException.ThrowIfNull(acceptFrom);
        ArgumentNullException.ThrowIfNull(releaseUnclaimed);

        this.acceptFrom = acceptFrom;
        this.releaseUnclaimed = releaseUnclaimed;
        pendingAccepts = new Task<TAccepted>?[sourceCount];
    }

    /// <summary>
    /// Waits until any source accepts, and returns what it accepted.
    /// </summary>
    /// <param name="cancellationToken">Cuts the wait off; the accepts already started carry on for the next call.</param>
    /// <returns>What the first source to accept produced.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the wait off.</exception>
    /// <exception cref="ObjectDisposedException"><see cref="StopAsync"/> was called.</exception>
    /// <exception cref="IOException">The accept failed with a socket error; the next call starts another.</exception>
    public async Task<TAccepted> AcceptNextAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(stopping is not null, this);

        StartMissingAccepts();
        var accept = await WaitForFirstAcceptAsync(cancellationToken);

        lock (gate)
        {
            // Once a stop has begun, whatever this accept produced is the stop's to release.
            ObjectDisposedException.ThrowIf(stopping is not null, this);
            pendingAccepts[Array.IndexOf(pendingAccepts, accept)] = null;
        }

        return await ClaimAsync(accept);
    }

    /// <summary>
    /// Stops accepting: cancels every accept still waiting, and releases anything one accepts
    /// before it stops. Calling it twice is harmless.
    /// </summary>
    /// <returns>A task that completes once every accept still waiting has finished.</returns>
    public Task StopAsync()
    {
        lock (gate)
        {
            if (stopping is null)
            {
                lifetime.Cancel();
                stopping = Task.WhenAll(pendingAccepts.OfType<Task<TAccepted>>().Select(ReleaseWhenAcceptedAsync));
            }

            return stopping;
        }
    }

    private static async Task<TAccepted> ClaimAsync(Task<TAccepted> accept)
    {
        try
        {
            return await accept;
        }
        catch (SocketException exception)
        {
            throw new IOException(exception.Message, exception);
        }
    }

    private void StartMissingAccepts()
    {
        for (var index = 0; index < pendingAccepts.Length; index++)
        {
            pendingAccepts[index] ??= acceptFrom(index, lifetime.Token);
        }
    }

    private async Task<Task<TAccepted>> WaitForFirstAcceptAsync(CancellationToken cancellationToken)
    {
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstAccept = Task.WhenAny(pendingAccepts.OfType<Task<TAccepted>>());

        using (cancellationToken.Register(() => cancelled.TrySetResult()))
        {
            await Task.WhenAny(firstAccept, cancelled.Task);
        }

        cancellationToken.ThrowIfCancellationRequested();

        return await firstAccept;
    }

    private async Task ReleaseWhenAcceptedAsync(Task<TAccepted> accept)
    {
        TAccepted accepted;

        try
        {
            accepted = await accept;
        }
        catch (Exception)
        {
            // The accept failed or was cancelled by the stop: nothing was accepted, so
            // there is nothing to release.
            return;
        }

        releaseUnclaimed(accepted);
    }
}
