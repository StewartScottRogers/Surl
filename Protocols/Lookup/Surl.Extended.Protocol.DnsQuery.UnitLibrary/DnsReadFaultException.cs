namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// Carries a <see cref="DnsReadFault"/> from wherever reading found it to
/// <see cref="DnsMessageReader.Read"/>, which turns it into a <see cref="DnsReadOutcome"/>. It
/// never leaves the library.
/// </summary>
internal sealed class DnsReadFaultException(DnsReadFault fault) : Exception(fault.ToString())
{
    /// <summary>The fault.</summary>
    public DnsReadFault Fault { get; } = fault;
}
