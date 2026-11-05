namespace Surl.Protocol.Gopher;

/// <summary>
/// One upstream test case's <c>&lt;reply&gt;</c> as the Gopher server half plays it (ADR-0080
/// decision 2): the <c>&lt;data&gt;</c> it sends and every byte it received.
/// </summary>
/// <remarks>
/// <para>
/// FILEFORMAT.md at tag <c>curl-8_21_0</c> makes <c>&lt;data&gt;</c> the content a selector
/// fetches: the server half sends it, whatever the selector, then closes the connection, the
/// close ending the reply as RFC 1436 says. No <c>&lt;servercmd&gt;</c> applies to Gopher, so a
/// case with one is skipped <c>servercmd-unsupported:&lt;command&gt;</c>.
/// </para>
/// <para>
/// One instance serves every connection of one case, and is safe to use from several at once.
/// Never compose it into the shipped <c>surl</c>; it is a test seam that
/// <c>Surl.Conformance.UnitTests</c> composes.
/// </para>
/// </remarks>
/// <param name="data">The case's <c>&lt;data&gt;</c>.</param>
public sealed class GopherUpstreamTestCaseReplies(byte[] data)
{
    private readonly byte[] data = data ?? throw new ArgumentNullException(nameof(data));
    private readonly Lock gate = new();
    private readonly MemoryStream received = new();

    /// <summary>
    /// The first words of the <c>&lt;servercmd&gt;</c> lines the Gopher server half implements:
    /// none.
    /// </summary>
    public static IReadOnlySet<string> SupportedServerCommands { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Every byte the server half read, in the order it arrived: what the case's
    /// <c>&lt;protocol&gt;</c> is checked against.
    /// </summary>
    public byte[] ReceivedBytes
    {
        get
        {
            lock (gate)
            {
                return received.ToArray();
            }
        }
    }

    /// <summary>The bytes every selector is answered with.</summary>
    internal ReadOnlyMemory<byte> Data => data;

    internal void RecordReceived(ReadOnlySpan<byte> bytes)
    {
        lock (gate)
        {
            received.Write(bytes);
        }
    }
}
