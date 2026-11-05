namespace Surl.Http3;

/// <summary>
/// What a server sets on its <see cref="Http3ConnectionServer"/> beyond the exchange's limits
/// (ADR-0086, decision 2).
/// </summary>
public sealed class Http3ServerOptions
{
    /// <summary>
    /// The <c>server</c> field the answers the connection writes itself carry - the <c>:status 431</c>
    /// for a field section past <c>--max-message</c> - between <c>:status</c> and
    /// <c>content-length</c>; <see langword="null"/> for none.
    /// </summary>
    public string? ServerFieldValue { get; init; }
}
