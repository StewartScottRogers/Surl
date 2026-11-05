namespace Surl.Conformance;

/// <summary>
/// What one Phase 7 run's own <c>surl</c> shows of the run (ADR-0089 decision 4).
/// </summary>
/// <param name="Port">The port that <c>surl</c> listened on, replaced by <c>&lt;port&gt;</c> before comparing.</param>
/// <param name="Notes">The <c>-v</c> notes <c>surl</c> wrote during the run.</param>
/// <param name="StoredFiles">The files the case compares, as <c>surl</c> stored them, by name; a file it did not store is absent.</param>
public sealed record CurlPortSurlSide(int Port, string Notes, IReadOnlyDictionary<string, byte[]> StoredFiles)
{
    /// <summary>
    /// Gets the bytes the client sent to <c>surl</c>, one entry per plaintext TCP connection in order,
    /// or <see langword="null"/> when they were not recorded. BL-579's loopback relay records them;
    /// until both runs carry them they are not compared.
    /// </summary>
    public IReadOnlyList<byte[]>? ClientRequestBytes { get; init; }
}
