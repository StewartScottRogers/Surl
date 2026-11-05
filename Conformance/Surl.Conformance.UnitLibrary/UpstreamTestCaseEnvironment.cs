namespace Surl.Conformance;

/// <summary>
/// What preprocessing a test case depends on: the values of its variables, the features its
/// <c>%if</c> lines test, the files its include instructions read and the clock
/// <c>%days[]</c> reads.
/// </summary>
/// <param name="Variables">
/// Each variable's value by its name without the <c>%</c> (<c>HTTPPORT</c>, <c>LOGDIR</c>,
/// <c>TESTNUMBER</c>); a name must be one <see cref="UpstreamTestCaseVariables.Names"/> lists. A
/// variable not given is left as written, so give a longer name whenever its prefix is given:
/// with <c>CLIENT6IP</c> but not <c>CLIENT6IP-NB</c>, <c>%CLIENT6IP-NB</c> becomes the address
/// followed by <c>-NB</c>.
/// </param>
/// <param name="Features">The features present; <c>%if</c> tests membership, case-sensitively.</param>
/// <param name="Files">Reads the files include instructions name.</param>
/// <param name="Time">The clock <c>%days[]</c> counts from.</param>
public sealed record UpstreamTestCaseEnvironment(
    IReadOnlyDictionary<string, string> Variables,
    IReadOnlySet<string> Features,
    IUpstreamTestCaseFileReader Files,
    TimeProvider Time);
