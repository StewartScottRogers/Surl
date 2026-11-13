using Surl.Content;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// What one server keeps between requests and connections: its open upload sessions, the lock
/// that lets one request at a time rewrite an <c>index.json</c>, where upload session UUIDs
/// come from, and the tokens it has issued. Nothing here is static, so registries side by side
/// share nothing.
/// </summary>
/// <param name="Sessions">The open upload sessions.</param>
/// <param name="IndexLock">Held while a request reads, changes and rewrites an <c>index.json</c>.</param>
/// <param name="NewSessionId">Makes each upload session's version 4 UUID.</param>
/// <param name="Tokens">The tokens the token endpoint issued.</param>
/// <param name="MetadataIndex">
/// Where each blob file's verified digest is cached against its stamp (ADR-0115, decision 2.1);
/// <see langword="null"/> checks every file on every read.
/// </param>
internal sealed record OciServerState(OciUploadSessions Sessions, SemaphoreSlim IndexLock, Func<Guid> NewSessionId, OciTokenBook Tokens, ObjectMetadataIndex? MetadataIndex);
