namespace Surl.Conformance;

/// <summary>
/// What a Phase 7 case needs beyond a <see cref="CurlPortMeasurementCase"/> to run the way the test it
/// mirrors runs, given identically to the upstream run and the port run (ADR-0089 decisions 2 and 3).
/// Every member is optional; the default runs the case as <see cref="PinnedCurlPortBesideUpstreamCurl"/>
/// always has.
/// </summary>
internal sealed record CurlPortRunSetup
{
    /// <summary>
    /// Gets the setup that changes nothing.
    /// </summary>
    public static CurlPortRunSetup None { get; } = new();

    /// <summary>
    /// Gets the SHA-256 of the supplementary upstream build the mirrored test runs, when it runs one
    /// (<c>PinnedUpstreamCurl.RunSupplementaryBuildAsync</c>), or <see langword="null"/> to pair the case
    /// by its <see cref="CurlPortMeasurementCase.UpstreamProtocol"/>.
    /// </summary>
    public string? UpstreamBuildSha256 { get; init; }

    /// <summary>
    /// Gets the environment variables the mirrored test sets for curl, beside the empty <c>HOME</c>,
    /// <c>CURL_HOME</c> and <c>USERPROFILE</c> every run gets. A variable named here wins.
    /// </summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets the bytes both clients read on standard input, as the mirrored test feeds them
    /// (a TELNET session's keystrokes). Empty by default.
    /// </summary>
    public ReadOnlyMemory<byte> StandardInput { get; init; }

    /// <summary>
    /// Gets how each run's own <c>surl</c> starts, when it is not
    /// <see cref="SurlOnLoopback.StartAsync(string, IReadOnlyDictionary{string, byte[]}, IReadOnlyList{string}, IReadOnlyList{string}, CancellationToken)"/>
    /// with the case's scheme, files and options (several listeners over one data directory, for example).
    /// </summary>
    public Func<CancellationToken, Task<SurlOnLoopback>>? StartSurlAsync { get; init; }

    /// <summary>
    /// Gets a loopback relay the mirrored test puts between curl and <c>surl</c>, started per run in
    /// front of that run's <c>surl</c> listener URL and returning the URL curl connects to instead
    /// (such as <see cref="DigestChallengeRelay"/>). The recording relay, when there is one, sits in
    /// front of it.
    /// </summary>
    public Func<Uri, CancellationToken, (IAsyncDisposable Relay, Uri BaseUrl)>? StartFrontRelay { get; init; }

    /// <summary>
    /// Gets curl's arguments for one run, from that run's <c>surl</c> and the base URL curl connects to,
    /// for a case whose arguments name more than the base URL (a port inside <c>--resolve</c>, a file
    /// written per run). It may also do the mirrored test's own steps before curl runs, such as a
    /// delivery to read back. <see langword="null"/> uses <see cref="CurlPortMeasurementCase.ArgumentsFor"/>.
    /// </summary>
    public Func<SurlOnLoopback, Uri, CancellationToken, Task<IReadOnlyList<string>>>? ArgumentsForRunAsync { get; init; }

    /// <summary>
    /// Gets how the files <c>surl</c> stored are read back once that run's <c>surl</c> has stopped, by
    /// name, for a case whose stored content is not in its served directory (a mail store). The names
    /// compared are still <see cref="CurlPortMeasurementCase.ComparedStoredFiles"/>.
    /// </summary>
    public Func<CancellationToken, Task<IReadOnlyDictionary<string, byte[]>>>? ReadStoredFilesAfterStopAsync { get; init; }

    /// <summary>
    /// Gets which of <c>surl</c>'s <c>-v</c> notes are compared, for a case whose mirrored test runs
    /// <c>surl -v</c> on a scheme whose packets carry the server's own randoms (an SSH key exchange
    /// cookie and padding), which no client chooses. <see langword="null"/> compares every note.
    /// </summary>
    public Func<string, string>? ComparedNotes { get; init; }

    /// <summary>
    /// Gets <paramref name="notes"/> without the packet dumps <c>surl -v</c> writes, the lines that begin
    /// <c>#&lt;connection&gt; &gt; </c> or <c>#&lt;connection&gt; &lt; </c>: on SSH they hold the server's random
    /// cookie, padding and encrypted bytes, which ADR-0089 decision 4 leaves uncompared. surl's
    /// <c>*</c> notes (negotiated algorithms, logins, refusals) stay.
    /// </summary>
    public static string WithoutPacketDumps(string notes) =>
        string.Join('\n', notes.Split('\n').Where(line => !System.Text.RegularExpressions.Regex.IsMatch(line, @"^#\d+ [<>] ")));

    /// <summary>
    /// Gets <paramref name="notes"/> without the note of how a TLS connection ended when the client gave up
    /// on it (<c>TLS handshake failed: …</c>, or an <c>IOException</c> from a connection the client reset):
    /// which of the two <c>surl</c> sees depends on the client's TLS library and on timing, not on curl, and
    /// the same client gives either from run to run (BL-580, measured 2026-11-05). ADR-0089 decision 4
    /// leaves TLS to its library; the exit code and the asserted stderr lines still judge the refusal.
    /// </summary>
    public static string WithoutTlsEndings(string notes) =>
        WithoutTlsEndingsAndExchangeOpenings(notes, dropExchangeOpenings: false);

    /// <summary>
    /// Gets <paramref name="notes"/> as <see cref="WithoutTlsEndings"/> does, and also without the
    /// <c>Exchange N opened</c> lines, for a case where the client chooses whether to reuse a connection
    /// for its second request: the Curl port sometimes opens one connection and sometimes two for the same
    /// command line over TLS, where pinned upstream curl opens two (BL-1089, measured 2026-11-12, two
    /// runs in six). The exit code and the bytes written still judge the case.
    /// </summary>
    public static string WithoutTlsEndingsAndExchangeOpenings(string notes) =>
        WithoutTlsEndingsAndExchangeOpenings(notes, dropExchangeOpenings: true);

    private static string WithoutTlsEndingsAndExchangeOpenings(string notes, bool dropExchangeOpenings) =>
        string.Join('\n', notes.Split('\n').Where(line =>
            !line.Contains("* TLS handshake failed: ", StringComparison.Ordinal)
            && !System.Text.RegularExpressions.Regex.IsMatch(line, @"\* Exchange \d+ ended because the protocol server threw IOException: ")
            && !(dropExchangeOpenings && System.Text.RegularExpressions.Regex.IsMatch(line, @"\* Exchange \d+ opened"))));
}
