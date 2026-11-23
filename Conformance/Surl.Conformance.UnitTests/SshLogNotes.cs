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
    /// Gets the <c>SSH negotiated ...</c> notes of a log snapshot that is already complete, each from
    /// <c>kex</c> to the end of its line, joined by newlines; empty when it holds none. A live log goes
    /// through <see cref="WaitForNegotiatedAlgorithmsAsync"/> instead.
    /// </summary>
    public static string NegotiatedAlgorithmsIn(string settledLog) =>
        string.Join('\n', NegotiatedNote().Matches(settledLog).Select(note => note.Groups["algorithms"].Value.TrimEnd('\r')));

    /// <summary>
    /// Waits, for at most ten seconds, until <paramref name="surl"/>'s log holds an
    /// <c>SSH negotiated ...</c> note and returns the notes joined by newlines. curl can exit just after
    /// NEWKEYS while surl is still writing the note, so one read can find it missing (BL-1375, BL-1607).
    /// Fails with surl's log when the bound passes.
    /// </summary>
    public static async Task<string> WaitForNegotiatedAlgorithmsAsync(SurlOnLoopback surl, CancellationToken cancellationToken)
    {
        var deadline = TimeProvider.System.GetUtcNow() + TimeSpan.FromSeconds(10);
        var notes = NegotiatedAlgorithmsIn(surl.Log);
        while (notes.Length == 0 && TimeProvider.System.GetUtcNow() < deadline)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
            notes = NegotiatedAlgorithmsIn(surl.Log);
        }

        if (notes.Length == 0)
        {
            Assert.Fail($"surl logged no 'SSH negotiated' note within 10 seconds. surl's log:\n{surl.Log}");
        }

        return notes;
    }

    [GeneratedRegex(@"Serving SSH host key (?<type>\S+), --hostpubsha256 (?<sha256>\S+) --hostpubmd5 (?<md5>[0-9a-f]{32})")]
    private static partial Regex ServingNote();

    [GeneratedRegex(@"SSH negotiated (?<algorithms>kex [^\n]*)")]
    private static partial Regex NegotiatedNote();
}
