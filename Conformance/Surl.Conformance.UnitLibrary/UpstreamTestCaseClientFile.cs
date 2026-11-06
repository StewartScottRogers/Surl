namespace Surl.Conformance;

/// <summary>
/// A file a test case's <c>&lt;file&gt;</c> to <c>&lt;file4&gt;</c> part has runtests create
/// before curl runs.
/// </summary>
/// <param name="Path">The path the part's <c>name</c> attribute gives, its variables substituted.</param>
/// <param name="Contents">The bytes, the part's <c>nonewline</c> and <c>crlf</c> applied.</param>
public sealed record UpstreamTestCaseClientFile(string Path, byte[] Contents);
