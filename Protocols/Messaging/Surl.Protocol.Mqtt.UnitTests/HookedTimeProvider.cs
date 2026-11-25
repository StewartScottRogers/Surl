namespace Surl.Protocol.Mqtt;

/// <summary>
/// A clock that stands still and runs an action at one chosen read of the time, so a test can act
/// at a point inside an operation that reads its clock.
/// </summary>
internal sealed class HookedTimeProvider : TimeProvider
{
    private Action? action;
    private int readsLeft;

    /// <summary>Runs <paramref name="hook"/> once, at the <paramref name="read"/>-th read from now.</summary>
    public void RunAtRead(int read, Action hook)
    {
        readsLeft = read;
        action = hook;
    }

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow()
    {
        if (action is { } hook && --readsLeft == 0)
        {
            action = null;
            hook();
        }

        return DateTimeOffset.UnixEpoch;
    }
}
