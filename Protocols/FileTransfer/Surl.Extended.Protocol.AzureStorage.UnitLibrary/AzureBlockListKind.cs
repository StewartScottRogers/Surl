namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// Which of a blob's block lists a Put Block List entry is looked for in ("Put Block List").
/// </summary>
internal enum AzureBlockListKind
{
    /// <summary>
    /// <c>Committed</c>: the committed block list.
    /// </summary>
    Committed,

    /// <summary>
    /// <c>Uncommitted</c>: the uncommitted blocks.
    /// </summary>
    Uncommitted,

    /// <summary>
    /// <c>Latest</c>: the uncommitted blocks, then the committed block list.
    /// </summary>
    Latest,
}
