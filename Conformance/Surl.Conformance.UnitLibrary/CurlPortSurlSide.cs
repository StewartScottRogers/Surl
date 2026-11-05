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
    /// Gets the bytes the client sent to <c>surl</c>, one entry per plaintext TCP connection in the
    /// order the recording relay accepted them (<see cref="ClientByteTranscript.ToConnections"/>), or
    /// <see langword="null"/> when they were not recorded. They are compared only when both runs
    /// carry them.
    /// </summary>
    public IReadOnlyList<byte[]>? ClientRequestBytes { get; init; }

    /// <summary>
    /// Gets the port of the recording relay the client was pointed at in place of <c>surl</c>, or
    /// <see langword="null"/> when the client talked to <c>surl</c> directly.
    /// </summary>
    public int? RelayPort { get; init; }

    /// <summary>
    /// Gets the port the client connected to - the relay's when there is one, otherwise
    /// <c>surl</c>'s - which is the port the client's output and request bytes name.
    /// </summary>
    public int ClientFacingPort => RelayPort ?? Port;
}
