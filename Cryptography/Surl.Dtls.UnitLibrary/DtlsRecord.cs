namespace Surl.Dtls;

/// <summary>
/// A record the reader accepted: its header as received and its plaintext.
/// </summary>
/// <param name="Header">The record's header as received; its length is the fragment's on the wire.</param>
/// <param name="Plaintext">The record's plaintext, decrypted when its epoch is protected.</param>
public sealed record DtlsRecord(DtlsRecordHeader Header, byte[] Plaintext);
