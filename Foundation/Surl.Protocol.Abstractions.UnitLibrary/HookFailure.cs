namespace Surl.Protocol.Abstractions;

/// <summary>
/// How a hook failed, in a <see cref="HookFailedEvent"/>.
/// </summary>
public enum HookFailure
{
    /// <summary>
    /// The hook threw an exception.
    /// </summary>
    Threw,

    /// <summary>
    /// The hook ran past its time limit.
    /// </summary>
    TimedOut,
}
