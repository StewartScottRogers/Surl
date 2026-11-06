namespace Surl.Extended.Protocol.S3;

/// <summary>
/// What reading a write's body into an upload session came to (<see cref="S3ObjectBodyReader"/>).
/// </summary>
/// <param name="Refusal">Why the body cannot be stored, or <see langword="null"/>.</param>
/// <param name="IsTooLarge">Whether the upload went past the store's upload limit.</param>
/// <param name="Md5">The MD5 of the object's bytes, for the <c>ETag</c> and <c>Content-MD5</c>.</param>
/// <param name="Sha256">The SHA-256 of the object's bytes, for a signed <c>x-amz-content-sha256</c>.</param>
/// <param name="Checksum">The additional checksum asked for, or <see langword="null"/>.</param>
internal sealed record S3ReceivedBody(S3Error? Refusal, bool IsTooLarge, byte[] Md5, byte[] Sha256, S3ReceivedChecksum? Checksum);
