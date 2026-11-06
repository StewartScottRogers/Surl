namespace Surl.Content;

/// <summary>
/// The result of <see cref="ObjectMetadataIndex.ReadAsync"/>.
/// </summary>
/// <param name="Properties">What the object answers with; <see langword="null"/> when no file is
/// at the key.</param>
/// <param name="StorageFailure">Why the index file could not be rewritten after a computed ETag
/// was cached, for the server to note with <c>IExchangeLog.Note</c>; <see langword="null"/> when
/// it was, or when nothing was written.</param>
public sealed record ObjectMetadataReading(ObjectProperties? Properties, string? StorageFailure);
