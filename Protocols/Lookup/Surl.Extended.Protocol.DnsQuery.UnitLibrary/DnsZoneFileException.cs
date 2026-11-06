namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// Thrown when a zone file cannot be a zone (ADR-0092 decision 1): <see cref="Fault"/> says what
/// is wrong, and <see cref="LineNumber"/> the first physical line of the entry it is in, when it
/// is in one. The message is <c>line &lt;n&gt;: &lt;fault&gt;</c>, or the fault alone when no
/// line holds it (a second zone for one apex, no SOA, the zones' bounds).
/// </summary>
public sealed class DnsZoneFileException : Exception
{
    /// <summary>Creates the exception for <paramref name="fault"/>, found on <paramref name="lineNumber"/>.</summary>
    /// <param name="lineNumber">The first physical line of the faulty entry, from 1, or <see langword="null"/>.</param>
    /// <param name="fault">What is wrong, one of the texts the zone reader pins.</param>
    public DnsZoneFileException(int? lineNumber, string fault)
        : base(lineNumber is { } line ? $"line {line}: {fault}" : fault)
    {
        LineNumber = lineNumber;
        Fault = fault;
    }

    /// <summary>The first physical line of the faulty entry, or <see langword="null"/> when no line holds the fault.</summary>
    public int? LineNumber { get; }

    /// <summary>What is wrong, without the line.</summary>
    public string Fault { get; }
}
