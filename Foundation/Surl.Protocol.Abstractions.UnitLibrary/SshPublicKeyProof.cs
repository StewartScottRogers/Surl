namespace Surl.Protocol.Abstractions;

/// <summary>
/// What an <see cref="SshPublicKeyLogin"/> proves about its key (ADR-0051, section 7).
/// </summary>
public enum SshPublicKeyProof
{
    /// <summary>
    /// The query form, without a signature: is the key acceptable? Never delayed, never noted.
    /// </summary>
    None,

    /// <summary>
    /// Signed, and the server verified the signature.
    /// </summary>
    ValidSignature,

    /// <summary>
    /// Signed, and the signature did not verify: refused, after the delay, with a note.
    /// </summary>
    InvalidSignature,
}
