using Surl.Content;

namespace Surl.Protocol.Ws;

/// <summary>
/// What an upgraded request path names in the content store: a file, or a directory when
/// listings are on (ADR-0071 decision 1, check 10).
/// </summary>
/// <param name="Path">The request path, without its query.</param>
/// <param name="Mapping">Where the content store mapped <paramref name="Path"/>.</param>
/// <param name="Status">The entry's kind and, for a file, its length, as looked up for check 10.</param>
internal sealed record WebSocketServedEntry(string Path, ContentPathMapping Mapping, ContentEntryStatus Status);
