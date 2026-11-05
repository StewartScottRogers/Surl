namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// What one server keeps between requests and connections: its open upload sessions, the lock
/// that lets one request at a time rewrite an <c>index.json</c>, and where upload session UUIDs
/// come from. Nothing here is static, so registries side by side share nothing.
/// </summary>
/// <param name="Sessions">The open upload sessions.</param>
/// <param name="IndexLock">Held while a request reads, changes and rewrites an <c>index.json</c>.</param>
/// <param name="NewSessionId">Makes each upload session's version 4 UUID.</param>
internal sealed record OciServerState(OciUploadSessions Sessions, SemaphoreSlim IndexLock, Func<Guid> NewSessionId);
