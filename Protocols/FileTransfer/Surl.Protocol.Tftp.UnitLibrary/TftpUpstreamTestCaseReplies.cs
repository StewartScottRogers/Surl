using System.Globalization;
using System.Text;

namespace Surl.Protocol.Tftp;

/// <summary>
/// One upstream test case's <c>&lt;reply&gt;</c> as the TFTP server half plays it (ADR-0080
/// decision 2): its parts by name as bytes, its <c>&lt;servercmd&gt;</c> lines, the request
/// lines the server half logged and every upload it was sent.
/// </summary>
/// <remarks>
/// <para>
/// FILEFORMAT.md at tag <c>curl-8_21_0</c> makes the parts content: <c>&lt;data&gt;</c> is the
/// file a read request fetches, and <c>&lt;dataNUM&gt;</c> the file of a name whose number is
/// over 10000, <c>NUM</c> being its remainder of 10000. A file's number is the last run of
/// digits in the requested name; a name with none names no file of the case, so a read of it
/// finds nothing and a write to it is refused. Leave out a <c>&lt;data&gt;</c> part that is empty
/// and not <c>sendzero="yes"</c>: the file does not exist then.
/// </para>
/// <para>
/// <see cref="ReceivedBytes"/> is the log a case's <c>&lt;protocol&gt;</c> is checked against, one
/// <c>name = value</c> line each, as runtests' TFTP server writes it: <c>opcode</c>, <c>mode</c>,
/// every option in the order the request sent it, then <c>filename</c>.
/// </para>
/// <para>
/// One instance serves every flow of one case, and is safe to use from several at once. Never
/// compose it into the shipped <c>surl</c>; it is a test seam that <c>Surl.Conformance.UnitTests</c>
/// composes.
/// </para>
/// </remarks>
public sealed class TftpUpstreamTestCaseReplies
{
    private const string DataPart = "data";
    private const string WriteDelayCommand = "writedelay:";

    private readonly Dictionary<string, byte[]> parts;
    private readonly Lock gate = new();
    private readonly MemoryStream received = new();
    private readonly MemoryStream uploaded = new();

    /// <summary>
    /// Holds a case's reply for the TFTP server half.
    /// </summary>
    /// <param name="parts">The <c>&lt;reply&gt;</c> parts by name (<c>data</c>, <c>data3</c> ...), each its bytes.</param>
    /// <param name="serverCommands">The <c>&lt;servercmd&gt;</c> lines.</param>
    /// <exception cref="NotSupportedException">A line names a command <see cref="SupportedServerCommands"/> lacks.</exception>
    /// <exception cref="FormatException">A <c>writedelay:</c> has no non-negative number of milliseconds.</exception>
    public TftpUpstreamTestCaseReplies(IReadOnlyDictionary<string, byte[]> parts, IEnumerable<string> serverCommands)
    {
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(serverCommands);

        this.parts = new Dictionary<string, byte[]>(parts, StringComparer.Ordinal);
        foreach (var line in serverCommands.Select(line => line.Trim()).Where(line => line.Length > 0 && !line.StartsWith('#')))
        {
            Apply(line);
        }
    }

    /// <summary>
    /// The first words of the <c>&lt;servercmd&gt;</c> lines the TFTP server half implements:
    /// <c>Testnum</c>, which names the case the server half already knows, and
    /// <c>writedelay:</c>, the pause before each DATA packet. A case using any other is skipped
    /// <c>servercmd-unsupported:&lt;command&gt;</c>.
    /// </summary>
    public static IReadOnlySet<string> SupportedServerCommands { get; } =
        new HashSet<string>(["Testnum", WriteDelayCommand], StringComparer.Ordinal);

    /// <summary>
    /// The request log, one line per request field as <see cref="TftpUpstreamTestCaseReplies"/>
    /// describes, in the order the requests arrived: what the case's <c>&lt;protocol&gt;</c> is
    /// checked against.
    /// </summary>
    public byte[] ReceivedBytes => Copy(received);

    /// <summary>
    /// Every byte uploaded with a write request, in the order the uploads ended: what the case's
    /// <c>&lt;upload&gt;</c> is checked against.
    /// </summary>
    public byte[] UploadedBytes => Copy(uploaded);

    /// <summary>The pause before each DATA packet, from <c>writedelay: [msecs]</c>; zero without one.</summary>
    internal TimeSpan WriteDelay { get; private set; }

    /// <summary>
    /// Whether <paramref name="fileName"/> carries a case number, so names a file of the case.
    /// </summary>
    /// <param name="fileName">The name as the request sent it.</param>
    /// <returns><see langword="true"/> when it holds a digit.</returns>
    internal static bool NamesACaseFile(string fileName) => fileName.Any(char.IsAsciiDigit);

    /// <summary>
    /// The file a read request for <paramref name="fileName"/> fetches.
    /// </summary>
    /// <param name="fileName">The name as the request sent it.</param>
    /// <returns>Its bytes, or <see langword="null"/> when the case has no such part.</returns>
    internal byte[]? FindFile(string fileName)
    {
        var end = fileName.AsSpan().LastIndexOfAnyInRange('0', '9') + 1;
        if (end == 0)
        {
            return null;
        }

        var start = fileName.AsSpan(0, end).LastIndexOfAnyExceptInRange('0', '9') + 1;
        var number = long.TryParse(fileName.AsSpan(start, end - start), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
        var name = number > 10000 ? DataPart + (number % 10000).ToString(CultureInfo.InvariantCulture) : DataPart;

        return parts.GetValueOrDefault(name);
    }

    /// <summary>Logs one request's fields, as <see cref="ReceivedBytes"/> describes.</summary>
    /// <param name="request">The parsed request.</param>
    internal void RecordRequest(TftpRequest request)
    {
        var text = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"opcode = {(request.IsWrite ? TftpPacket.WriteRequest : TftpPacket.ReadRequest)}\n")
            .Append(CultureInfo.InvariantCulture, $"mode = {request.Mode}\n");
        foreach (var option in request.Options)
        {
            text.Append(CultureInfo.InvariantCulture, $"{option.Name} = {option.Value}\n");
        }

        text.Append(CultureInfo.InvariantCulture, $"filename = {Encoding.Latin1.GetString(request.FileName)}\n");
        Record(received, Encoding.Latin1.GetBytes(text.ToString()));
    }

    /// <summary>Keeps one finished upload.</summary>
    /// <param name="bytes">The uploaded file.</param>
    internal void RecordUpload(ReadOnlySpan<byte> bytes) => Record(uploaded, bytes);

    private void Apply(string line)
    {
        var command = line.Split(' ', '\t')[0];
        if (!SupportedServerCommands.Contains(command))
        {
            throw new NotSupportedException($"The TFTP server half does not implement the server command \"{command}\".");
        }

        if (command == WriteDelayCommand)
        {
            var argument = line[command.Length..].Trim();
            WriteDelay = int.TryParse(argument, NumberStyles.None, CultureInfo.InvariantCulture, out var milliseconds)
                ? TimeSpan.FromMilliseconds(milliseconds)
                : throw new FormatException($"\"{line}\" names no number of milliseconds.");
        }
    }

    private void Record(MemoryStream stream, ReadOnlySpan<byte> bytes)
    {
        lock (gate)
        {
            stream.Write(bytes);
        }
    }

    private byte[] Copy(MemoryStream stream)
    {
        lock (gate)
        {
            return stream.ToArray();
        }
    }
}
