using System.Text;
using Surl.HttpMessage;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// One upstream test case's <c>&lt;reply&gt;</c> as the HTTP proxy server half plays it (ADR-0080
/// decision 2): its parts by name as bytes, the state the case carries from one request to the
/// next, and every byte the proxy read, on each of curl's connections, before that connection's tunnel opened.
/// </summary>
/// <remarks>
/// <para>
/// A request is answered with <c>&lt;dataNUM&gt;</c>, or <c>&lt;connectNUM&gt;</c> for
/// <c>CONNECT</c>, as FILEFORMAT.md at tag <c>curl-8_21_0</c> chooses <c>NUM</c>; <c>NUM</c> 0
/// is the unnumbered part. After a part containing <c>swsbounce</c>, the case's next request
/// gets the part numbered one more. Parts are sent verbatim, so build them with the case's
/// newline attributes already applied.
/// </para>
/// <para>
/// None of the cases that name <c>http-proxy</c> or <c>https-proxy</c> at the tag has a
/// <c>&lt;servercmd&gt;</c> but <c>Testnum</c>, so that is the only command accepted. The state is
/// the case's, not a connection's, and is safe to use from several connections at once. Never compose
/// it into the shipped <c>surl</c>; it is a test seam that <c>Surl.Conformance.UnitTests</c> composes.
/// </para>
/// </remarks>
public sealed class HttpProxyUpstreamTestCaseReplies
{
    private const string BounceKeyword = "swsbounce";

    private const string CloseKeyword = "swsclose";

    private readonly Dictionary<string, byte[]> parts;
    private readonly Lock gate = new();
    private readonly MemoryStream received = new();
    private int? bouncedPartNumber;
    private int negotiateRequestCount;

    /// <summary>
    /// Holds a case's reply for the proxy server half.
    /// </summary>
    /// <param name="parts">The <c>&lt;reply&gt;</c> parts by name (<c>data</c>, <c>connect</c>, <c>connect1001</c> ...), each the bytes to send.</param>
    /// <param name="serverCommands">The <c>&lt;servercmd&gt;</c> lines.</param>
    /// <exception cref="NotSupportedException">A line names a command <see cref="SupportedServerCommands"/> lacks.</exception>
    public HttpProxyUpstreamTestCaseReplies(IReadOnlyDictionary<string, byte[]> parts, IEnumerable<string> serverCommands)
    {
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(serverCommands);

        this.parts = new Dictionary<string, byte[]>(parts, StringComparer.Ordinal);
        foreach (var command in serverCommands.Select(line => line.Trim()).Where(line => line.Length > 0 && !line.StartsWith('#')))
        {
            var word = command.Split([' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries)[0];
            if (!SupportedServerCommands.Contains(word))
            {
                throw new NotSupportedException($"The HTTP proxy server half does not implement the server command '{word}' (servercmd-unsupported:{word}).");
            }
        }
    }

    /// <summary>
    /// The first words of the <c>&lt;servercmd&gt;</c> lines the proxy server half accepts: only
    /// <c>Testnum</c>, the line runtests writes first, which names the case. A case using any other
    /// is skipped <c>servercmd-unsupported:&lt;command&gt;</c>.
    /// </summary>
    public static IReadOnlySet<string> SupportedServerCommands { get; } = new HashSet<string>(["Testnum"], StringComparer.Ordinal);

    /// <summary>
    /// Every byte the proxy read from curl's connections, in the order they arrived, each
    /// connection's up to the moment its tunnel opened: what the case's <c>&lt;proxy&gt;</c> is checked against.
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

    internal void RecordReceived(ReadOnlySpan<byte> bytes)
    {
        lock (gate)
        {
            received.Write(bytes);
        }
    }

    internal HttpProxyUpstreamTestCaseReply ChooseReply(HttpRequestHead head)
    {
        lock (gate)
        {
            var asked = HttpProxyUpstreamTestCasePartNumber.Of(head, ref negotiateRequestCount);
            var number = bouncedPartNumber ?? asked;
            var prefix = head.Method == "CONNECT" ? "connect" : "data";
            var name = number == 0 ? prefix : prefix + number;
            var bytes = parts.GetValueOrDefault(name);
            bouncedPartNumber = Contains(bytes, BounceKeyword) ? number + 1 : null;

            return new HttpProxyUpstreamTestCaseReply(name, bytes, Contains(bytes, CloseKeyword));
        }
    }

    private static bool Contains(byte[]? bytes, string keyword) =>
        bytes is not null && bytes.AsSpan().IndexOf(Encoding.ASCII.GetBytes(keyword)) >= 0;
}
