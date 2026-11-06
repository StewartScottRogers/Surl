using Surl.HttpMessage;

namespace Surl.Protocol.Http;

/// <summary>
/// One upstream test case's <c>&lt;reply&gt;</c> as the HTTP server half plays it (ADR-0080
/// decision 2): its parts by name as bytes, its <c>&lt;servercmd&gt;</c> lines, the state the
/// case carries from one request to the next, and every byte the server half received.
/// </summary>
/// <remarks>
/// <para>
/// A request is answered with <c>&lt;dataNUM&gt;</c>, or <c>&lt;connectNUM&gt;</c> for
/// <c>CONNECT</c>, as FILEFORMAT.md at tag <c>curl-8_21_0</c> chooses <c>NUM</c>; <c>NUM</c> 0
/// is the unnumbered part. After a part containing <c>swsbounce</c>, the case's next request
/// gets the part numbered one more, whatever it asks for. Parts are sent verbatim, so build
/// them with the case's newline attributes already applied.
/// </para>
/// <para>
/// The state is the case's, not a connection's: one instance serves every connection of one
/// case, and is safe to use from several at once. Never compose it into the shipped
/// <c>surl</c>; it is a test seam that <c>Surl.Conformance.UnitTests</c> composes.
/// </para>
/// </remarks>
public sealed class HttpUpstreamTestCaseReplies
{
    private const string BounceKeyword = "swsbounce";

    private const string CloseKeyword = "swsclose";

    private readonly Dictionary<string, byte[]> parts;
    private readonly Lock gate = new();
    private readonly MemoryStream received = new();
    private int? bouncedPartNumber;
    private int negotiateRequestCount;

    /// <summary>
    /// Holds a case's reply for the HTTP server half.
    /// </summary>
    /// <param name="parts">The <c>&lt;reply&gt;</c> parts by name (<c>data</c>, <c>data2</c>, <c>connect</c> ...), each the bytes to send.</param>
    /// <param name="serverCommands">The <c>&lt;servercmd&gt;</c> lines.</param>
    /// <exception cref="NotSupportedException">A line names a command <see cref="SupportedServerCommands"/> lacks.</exception>
    /// <exception cref="FormatException">A <c>writedelay:</c> or <c>skip:</c> line has no non-negative number.</exception>
    public HttpUpstreamTestCaseReplies(IReadOnlyDictionary<string, byte[]> parts, IEnumerable<string> serverCommands)
    {
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(serverCommands);

        this.parts = new Dictionary<string, byte[]>(parts, StringComparer.Ordinal);
        Commands = HttpUpstreamTestCaseServerCommands.Parse(serverCommands);
    }

    /// <summary>
    /// The first words of the <c>&lt;servercmd&gt;</c> lines the HTTP server half implements:
    /// <c>Testnum</c>, <c>auth_required</c>, <c>idle</c>, <c>no-expect</c>, <c>skip:</c>,
    /// <c>swsclose</c> and <c>writedelay:</c>. A case using any other is skipped
    /// <c>servercmd-unsupported:&lt;command&gt;</c>.
    /// </summary>
    public static IReadOnlySet<string> SupportedServerCommands => HttpUpstreamTestCaseServerCommands.Supported;

    /// <summary>
    /// Every byte the server half read from its connections, in the order they arrived: what
    /// the case's <c>&lt;protocol&gt;</c> is checked against.
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

    internal HttpUpstreamTestCaseServerCommands Commands { get; }

    internal void RecordReceived(ReadOnlySpan<byte> bytes)
    {
        lock (gate)
        {
            received.Write(bytes);
        }
    }

    internal HttpUpstreamTestCaseReply ChooseReply(HttpRequestHead head)
    {
        lock (gate)
        {
            var asked = HttpUpstreamTestCasePartNumber.Of(head, ref negotiateRequestCount);
            var number = bouncedPartNumber ?? asked;
            var prefix = head.Method == "CONNECT" ? "connect" : "data";
            var name = number == 0 ? prefix : prefix + number;
            var bytes = parts.GetValueOrDefault(name);
            bouncedPartNumber = Contains(bytes, BounceKeyword) ? number + 1 : null;

            return new HttpUpstreamTestCaseReply(name, bytes, Commands.ClosesAfterReply || Contains(bytes, CloseKeyword));
        }
    }

    private static bool Contains(byte[]? bytes, string keyword) =>
        bytes is not null && bytes.AsSpan().IndexOf(System.Text.Encoding.ASCII.GetBytes(keyword)) >= 0;
}
