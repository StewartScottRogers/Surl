using System.Text.RegularExpressions;

namespace Surl.Conformance;

/// <summary>
/// Reads the SSH notes a <c>-v</c> surl writes to its log: the host-key fingerprint note
/// (ADR-0051 decision 8), which a test passes to curl's <c>--hostpubsha256</c>, and the
/// negotiated-algorithms note (decision 10), which records what curl and surl agreed.
/// </summary>
internal static partial class SshLogNotes
{
    /// <summary>
    /// Gets the <c>--hostpubsha256</c> value of the host key of type <paramref name="keyType"/>
    /// surl serves, from its <c>Serving SSH host key</c> note.
    /// </summary>
    public static string HostKeySha256(string log, string keyType)
    {
        foreach (Match note in ServingNote().Matches(log))
        {
            if (note.Groups["type"].Value == keyType)
            {
                return note.Groups["sha256"].Value;
            }
        }

        throw new AssertFailedException($"surl's log has no Serving SSH host key {keyType} note:\n{log}");
    }

    /// <summary>
    /// Gets the <c>--hostpubmd5</c> value of the host key of type <paramref name="keyType"/>
    /// surl serves, from its <c>Serving SSH host key</c> note.
    /// </summary>
    public static string HostKeyMd5(string log, string keyType)
    {
        foreach (Match note in ServingNote().Matches(log))
        {
            if (note.Groups["type"].Value == keyType)
            {
                return note.Groups["md5"].Value;
            }
        }

        throw new AssertFailedException($"surl's log has no Serving SSH host key {keyType} note:\n{log}");
    }

    /// <summary>
    /// Gets every <c>SSH negotiated ...</c> note in the log, from <c>kex</c> to the end of its line.
    /// </summary>
    public static IReadOnlyList<string> NegotiatedAlgorithms(string log) =>
        [.. NegotiatedNote().Matches(log).Select(note => note.Groups["algorithms"].Value.TrimEnd('\r'))];

    [GeneratedRegex(@"Serving SSH host key (?<type>\S+), --hostpubsha256 (?<sha256>\S+) --hostpubmd5 (?<md5>[0-9a-f]{32})")]
    private static partial Regex ServingNote();

    [GeneratedRegex(@"SSH negotiated (?<algorithms>kex [^\n]*)")]
    private static partial Regex NegotiatedNote();
}
