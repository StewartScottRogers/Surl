using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// What a PUT Object's body is checked against once it is read: the signed
/// <c>x-goog-content-sha256</c> first (ADR-0110 decision 3.3), then the digests the head sent.
/// </summary>
/// <param name="Sent">The <c>Content-MD5</c> and <c>x-goog-hash</c> digests the head sent.</param>
/// <param name="BodyCheck">The rest of a signature that binds the body; <see langword="null"/> when the head decided.</param>
internal sealed record GcsUploadChecks(GcsSentDigests Sent, IHttpRequestBodyCheck? BodyCheck);
