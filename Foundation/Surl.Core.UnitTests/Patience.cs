namespace Surl.Core;

/// <summary>
/// How long a test waits for something that should already be happening before it fails,
/// so a broken engine fails the test instead of hanging it.
/// </summary>
internal static class Patience
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
}
