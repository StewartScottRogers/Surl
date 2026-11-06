namespace Surl.Dtls;

/// <summary>
/// Why a received record was discarded. Every one is discarded silently, with no alert
/// (RFC 6347 section 4.1.2.7, ADR-0087 decision 8); the reason is for the verbose log.
/// </summary>
public enum DtlsRecordDropReason
{
    /// <summary>The datagram ends inside a record's header or before the length its header gives; the rest of the datagram is dropped with it.</summary>
    Truncated,

    /// <summary>The record belongs to an epoch other than the one being read.</summary>
    WrongEpoch,

    /// <summary>The record's sequence number was already accepted, or is older than the replay window.</summary>
    Replayed,

    /// <summary>The record's fragment is too short for its protection, or its tag does not verify.</summary>
    FailedAuthentication,

    /// <summary>The record's plaintext is longer than 2^14 bytes (RFC 5246 section 6.2.1).</summary>
    TooLong,
}
