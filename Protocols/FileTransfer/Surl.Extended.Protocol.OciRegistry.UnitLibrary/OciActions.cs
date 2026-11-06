namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// What a token may do in one repository, as a Token scope's actions name it (Token "Requesting
/// a Token"; ADR-0115, decision 4.3).
/// </summary>
[Flags]
internal enum OciActions
{
    /// <summary>
    /// Nothing: <c>/v2/</c> itself, which needs a token of any scope.
    /// </summary>
    None = 0,

    /// <summary>
    /// <c>pull</c>: every read.
    /// </summary>
    Pull = 1,

    /// <summary>
    /// <c>push</c>: every upload request and manifest push.
    /// </summary>
    Push = 2,

    /// <summary>
    /// <c>delete</c>: a manifest or blob delete.
    /// </summary>
    Delete = 4,
}
