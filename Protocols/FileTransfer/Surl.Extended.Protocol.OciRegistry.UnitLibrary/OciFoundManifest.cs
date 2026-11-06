namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// A manifest found in a repository, its bytes checked against its digest.
/// </summary>
/// <param name="Descriptor">The descriptor that lists it, whose media type is its <c>Content-Type</c>.</param>
/// <param name="Digest">Its digest, sent as <c>Docker-Content-Digest</c>.</param>
/// <param name="Bytes">Its bytes, exactly as stored.</param>
internal sealed record OciFoundManifest(OciDescriptor Descriptor, OciDigest Digest, byte[] Bytes);
