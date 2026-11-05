namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// Thrown by <see cref="DnsZoneFolder.LoadAsync"/> when a zone file cannot be loaded (ADR-0092
/// decision 1): it cannot be read, or it cannot be a zone. Nothing is served. The composition
/// root answers it with <c>CouldNotReadFile</c> (37) and
/// <c>surl: (37) Could not read &lt;FilePath&gt;: &lt;Message&gt;</c>.
/// </summary>
public sealed class DnsZoneLoadException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="filePath">The full path of the zone file.</param>
    /// <param name="reason">
    /// Why it cannot be loaded, the exception's message: the read failure's message, or
    /// <c>line &lt;n&gt;: &lt;what&gt;</c>, or what alone when no line holds it.
    /// </param>
    /// <param name="innerException">The exception behind it.</param>
    public DnsZoneLoadException(string filePath, string reason, Exception innerException)
        : base(reason, innerException)
    {
        FilePath = filePath;
    }

    /// <summary>The full path of the zone file.</summary>
    public string FilePath { get; }
}
