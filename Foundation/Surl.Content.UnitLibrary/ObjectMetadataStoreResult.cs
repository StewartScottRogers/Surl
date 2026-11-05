namespace Surl.Content;

/// <summary>
/// The result of <see cref="ObjectMetadataIndex.StoreAsync"/>.
/// </summary>
/// <param name="Outcome">Whether the metadata is stored, and why not when it is not.</param>
/// <param name="Properties">What the object now answers with; <see langword="null"/> unless
/// <see cref="Outcome"/> is <see cref="ObjectMetadataStoreOutcome.Stored"/>.</param>
/// <param name="StorageFailure">Why the index file could not be rewritten, for the server to note
/// with <c>IExchangeLog.Note</c>; <see langword="null"/> when it was, or when nothing was written.
/// The change is kept in memory either way, and the next change rewrites the whole index.</param>
public sealed record ObjectMetadataStoreResult(
    ObjectMetadataStoreOutcome Outcome,
    ObjectProperties? Properties,
    string? StorageFailure);
