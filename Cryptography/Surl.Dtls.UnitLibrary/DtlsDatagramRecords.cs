namespace Surl.Dtls;

/// <summary>
/// What one received datagram held: the records accepted, in order, and why each other record
/// was dropped.
/// </summary>
/// <param name="Records">The records accepted.</param>
/// <param name="Drops">One reason per record dropped, in order.</param>
public sealed record DtlsDatagramRecords(IReadOnlyList<DtlsRecord> Records, IReadOnlyList<DtlsRecordDropReason> Drops);
