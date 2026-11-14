using System.Globalization;
using System.Text;

namespace Surl.Protocol.Ftp;

/// <summary>
/// One upstream test case's <c>&lt;reply&gt;</c> as the FTP server half plays it (ADR-0080
/// decision 2): its parts by name as bytes, its <c>&lt;servercmd&gt;</c> lines, every byte the
/// server half received on its control connections and every upload it was sent.
/// </summary>
/// <remarks>
/// <para>
/// FILEFORMAT.md at tag <c>curl-8_21_0</c> makes the parts content, not replies:
/// <c>&lt;data&gt;</c> is what <c>RETR</c> sends and, for <c>LIST</c> and <c>NLST</c>, the
/// listing; <c>&lt;dataNUM&gt;</c> is sent for a file whose number is over 10000, <c>NUM</c> being
/// its remainder of 10000; <c>&lt;size&gt;</c> answers <c>SIZE</c> and <c>&lt;mdtm&gt;</c>
/// <c>MDTM</c>, <c>-1</c> in either answering that the file does not exist. A file's number is
/// the first run of digits in the name <c>RETR</c> sends; a name with none asks for
/// <c>&lt;data&gt;</c>. Leave out a <c>&lt;data&gt;</c> part that is empty and not
/// <c>sendzero="yes"</c>: the file does not exist then.
/// </para>
/// <para>
/// One instance serves every connection of one case, and is safe to use from several at once.
/// Never compose it into the shipped <c>surl</c>; it is a test seam that
/// <c>Surl.Conformance.UnitTests</c> composes.
/// </para>
/// </remarks>
public sealed class FtpUpstreamTestCaseReplies
{
    private const string DataPart = "data";

    private readonly Dictionary<string, byte[]> parts;
    private readonly FtpUpstreamTestCaseServerCommands commands;
    private readonly Lock gate = new();
    private readonly MemoryStream received = new();
    private readonly MemoryStream uploaded = new();

    /// <summary>
    /// Holds a case's reply for the FTP server half.
    /// </summary>
    /// <param name="parts">The <c>&lt;reply&gt;</c> parts by name (<c>data</c>, <c>data2</c>, <c>size</c>, <c>mdtm</c> ...), each its bytes.</param>
    /// <param name="serverCommands">The <c>&lt;servercmd&gt;</c> lines.</param>
    /// <exception cref="NotSupportedException">A line names a command <see cref="SupportedServerCommands"/> lacks.</exception>
    /// <exception cref="FormatException">A <c>REPLY</c> names no command, or a <c>COUNT</c> or <c>RETRSIZE</c> has no non-negative number.</exception>
    public FtpUpstreamTestCaseReplies(IReadOnlyDictionary<string, byte[]> parts, IEnumerable<string> serverCommands)
    {
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(serverCommands);

        this.parts = new Dictionary<string, byte[]>(parts, StringComparer.Ordinal);
        commands = FtpUpstreamTestCaseServerCommands.Parse(serverCommands);
    }

    /// <summary>
    /// The first words of the <c>&lt;servercmd&gt;</c> lines the FTP server half implements:
    /// <c>Testnum</c>, <c>REPLY</c>, <c>REPLYLF</c>, <c>COUNT</c>, <c>RETRNOSIZE</c>,
    /// <c>RETRSIZE</c>, <c>NOSAVE</c>, <c>STOR</c>, <c>DELAY</c>, <c>SLOWDOWN</c> and
    /// <c>SLOWDOWNDATA</c>, <c>NODATACONN425</c>, <c>NODATACONN421</c>, <c>NODATACONN150</c>,
    /// <c>PASVBADIP</c> and <c>RETRWEIRDO</c>. A case using any other is skipped
    /// <c>servercmd-unsupported:&lt;command&gt;</c>.
    /// </summary>
    public static IReadOnlySet<string> SupportedServerCommands => FtpUpstreamTestCaseServerCommands.Supported;

    /// <summary>
    /// Every byte the server half read from its control connections, in the order they arrived:
    /// what the case's <c>&lt;protocol&gt;</c> is checked against.
    /// </summary>
    public byte[] ReceivedBytes => Copy(received);

    /// <summary>
    /// Every byte uploaded with <c>STOR</c> or <c>APPE</c>, in the order they arrived, unless
    /// <c>NOSAVE</c>: what the case's <c>&lt;upload&gt;</c> is checked against.
    /// </summary>
    public byte[] UploadedBytes => Copy(uploaded);

    /// <summary>Whether <c>SLOWDOWN</c> makes control replies go one byte per 10 ms.</summary>
    internal bool SlowsControlReplies => commands.SlowsControlReplies;

    /// <summary>Whether <c>SLOWDOWNDATA</c> makes data bytes go one per 10 ms.</summary>
    internal bool SlowsDataBytes => commands.SlowsDataBytes;

    /// <summary>What data commands answer when no data connection is made, from the <c>NODATACONN</c> words.</summary>
    internal FtpNoDataConnectionReply NoDataConnectionReply => commands.NoDataConnectionReply;

    /// <summary>Whether <c>PASVBADIP</c> puts <c>1,2,3,4</c> in <c>PASV</c>'s <c>227</c>.</summary>
    internal bool ReportsBadPassiveAddress => commands.ReportsBadPassiveAddress;

    /// <summary>Whether <c>RETRWEIRDO</c> is set.</summary>
    internal bool SendsRetrieveRepliesTogether => commands.SendsRetrieveRepliesTogether;

    /// <summary>The <c>150</c> of a <c>RETRWEIRDO</c> retrieve: the case number and an empty part, as upstream's.</summary>
    internal string WeirdRetrieveOpeningReply(long byteCount) =>
        $"150 Binary data connection for {commands.TestNumber} () ({byteCount.ToString(CultureInfo.InvariantCulture)} bytes).";

    /// <summary>The pause <c>DELAY</c> puts before the answer to <paramref name="command"/>, if any.</summary>
    internal TimeSpan? DelayBefore(string command) => commands.DelayBefore(command);

    /// <summary>The reply that ends an upload in place of <c>226</c>, from <c>STOR [msg]</c>.</summary>
    internal string? StoreReply => commands.StoreReply;

    /// <summary>The listing <c>LIST</c> and <c>NLST</c> send: <c>&lt;data&gt;</c>, or nothing.</summary>
    internal byte[] Listing => parts.GetValueOrDefault(DataPart) ?? [];

    /// <summary>
    /// The scripted answer to <paramref name="command"/> as bytes to send, spending one of its
    /// <c>COUNT</c> uses.
    /// </summary>
    /// <param name="command">The command in upper case, or <c>welcome</c> for the greeting.</param>
    /// <returns>The answer, or <see langword="null"/> when the server answers it itself.</returns>
    internal byte[]? TakeScriptedReply(string command)
    {
        lock (gate)
        {
            return commands.TakeReply(command) is { } reply ? Encoding.Latin1.GetBytes(reply) : null;
        }
    }

    internal void RecordReceived(ReadOnlySpan<byte> bytes) => Record(received, bytes);

    internal void RecordUpload(ReadOnlySpan<byte> bytes)
    {
        if (!commands.DiscardsUploads)
        {
            Record(uploaded, bytes);
        }
    }

    /// <summary>
    /// The file <c>RETR</c> sends for <paramref name="fileName"/>.
    /// </summary>
    /// <param name="fileName">The name as sent.</param>
    /// <returns>Its bytes, or <see langword="null"/> when the case has no such part.</returns>
    internal byte[]? FindFile(string fileName)
    {
        var digits = new string([.. fileName.SkipWhile(character => !char.IsAsciiDigit(character)).TakeWhile(char.IsAsciiDigit)]);
        var number = digits.Length > 0 && long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
        var name = number > 10000 ? DataPart + (number % 10000).ToString(CultureInfo.InvariantCulture) : DataPart;

        return parts.GetValueOrDefault(name);
    }

    /// <summary>
    /// The answer to <c>SIZE</c>: <c>213</c> with <c>&lt;size&gt;</c>, or with the file's length
    /// when the case has no <c>&lt;size&gt;</c>.
    /// </summary>
    /// <param name="fileName">The name as sent.</param>
    /// <returns>The reply, or <see langword="null"/> when the file does not exist.</returns>
    internal string? SizeReply(string fileName)
    {
        if (FindText("size") is { } size)
        {
            return size == "-1" ? null : "213 " + size;
        }

        return FindFile(fileName) is { } file ? "213 " + file.Length.ToString(CultureInfo.InvariantCulture) : null;
    }

    /// <summary>
    /// The answer to <c>MDTM</c>: <c>&lt;mdtm&gt;</c> as written.
    /// </summary>
    /// <returns>The reply, or <see langword="null"/> when the case has none or it is <c>-1</c>.</returns>
    internal string? ModificationTimeReply() =>
        FindText("mdtm") is { } reply && reply != "-1" ? reply : null;

    /// <summary>
    /// The <c>150</c> that opens a <c>RETR</c>, naming the size as <c>RETRNOSIZE</c> and
    /// <c>RETRSIZE</c> say.
    /// </summary>
    /// <param name="fileNameText">The file as the reply names it.</param>
    /// <param name="byteCount">How many bytes follow.</param>
    /// <returns>The reply.</returns>
    internal string RetrieveOpeningReply(string fileNameText, long byteCount)
    {
        var opening = $"150 Opening data connection for {fileNameText}";

        return commands.OmitsRetrieveSize
            ? opening
            : $"{opening} ({(commands.RetrieveSize ?? byteCount).ToString(CultureInfo.InvariantCulture)} bytes)";
    }

    private string? FindText(string partName) =>
        parts.GetValueOrDefault(partName) is { } bytes ? Encoding.Latin1.GetString(bytes).Trim() : null;

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
