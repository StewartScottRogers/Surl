namespace Surl.Conformance;

/// <summary>
/// Gives the test process enough thread-pool threads at once for every parallel test to be
/// waiting on a curl or surl process while the callbacks that read those processes' output
/// still run (BL-604): without it a pool at its starting size delays the callback that reads
/// surl's <c>Listening on</c> line until the 30-second start timeout has already expired.
/// </summary>
[TestClass]
public static class ThreadPoolSetup
{
    private const int MinimumThreads = 512;

    /// <summary>Raises the thread pool's minimum thread counts before the first test runs.</summary>
    /// <param name="context">The MSTest assembly context, unused.</param>
    [AssemblyInitialize]
    public static void RaiseMinimumThreads(TestContext context)
    {
        ThreadPool.GetMinThreads(out var workers, out var completionPorts);
        ThreadPool.SetMinThreads(Math.Max(workers, MinimumThreads), Math.Max(completionPorts, MinimumThreads));
    }
}
