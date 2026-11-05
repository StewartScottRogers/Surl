using Surl.Content;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// The appends staged for one Data Lake file and not yet flushed (ADR-0109, decision 5.5): an
/// upload session that started from the file's committed bytes, the ranges each append wrote into
/// it, and when it was last touched.
/// </summary>
/// <param name="session">The upload session the appends are written into.</param>
/// <param name="committedLength">The file's length when the staging began, or after its last flush.</param>
/// <param name="lastTouched">When the staging began.</param>
internal sealed class AzureStagedPath(ContentUploadSession session, long committedLength, DateTimeOffset lastTouched)
{
    private readonly List<AzureStagedRange> ranges = [];

    /// <summary>
    /// The upload session the appends are written into.
    /// </summary>
    public ContentUploadSession Session { get; } = session;

    /// <summary>
    /// The file's committed length, which the first staged range must start at.
    /// </summary>
    public long CommittedLength { get; } = committedLength;

    /// <summary>
    /// When an append last wrote to the staging.
    /// </summary>
    public DateTimeOffset LastTouched { get; private set; } = lastTouched;

    /// <summary>
    /// Records an append's bytes.
    /// </summary>
    /// <param name="range">Where they were written.</param>
    /// <param name="now">When.</param>
    public void Add(AzureStagedRange range, DateTimeOffset now)
    {
        ranges.Add(range);
        LastTouched = now;
    }

    /// <summary>
    /// Whether the staged ranges, in position order, cover the file from its committed length to
    /// <paramref name="position"/> without a gap or an overlap: what a flush to
    /// <paramref name="position"/> needs (ADR-0109, decision 5.5).
    /// </summary>
    /// <param name="position">The flush's position.</param>
    /// <returns>Whether the flush may commit.</returns>
    public bool Reaches(long position)
    {
        var end = CommittedLength;
        foreach (var range in ranges.OrderBy(range => range.Position))
        {
            if (end >= position)
            {
                break;
            }

            if (range.Position != end)
            {
                return false;
            }

            end += range.Length;
        }

        return position >= CommittedLength && position <= end;
    }
}
