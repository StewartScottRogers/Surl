using System.Globalization;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// The query of <c>storage.objects.get</c> and <c>storage.objects.delete</c> (ADR-0148, decisions 2 and 5):
/// <c>alt</c>, <c>generation</c> and the four conditions.
/// </summary>
/// <param name="Conditions">The generation and metageneration conditions.</param>
/// <param name="Generation">The <c>generation</c> asked for, or <see langword="null"/> for the current one.</param>
/// <param name="IsMedia">Whether the answer is the object's bytes: <c>alt=media</c>, or the download path.</param>
internal sealed record GcsJsonObjectQuery(GcsJsonConditions Conditions, long? Generation, bool IsMedia)
{
    /// <summary>
    /// Reads the query.
    /// </summary>
    /// <param name="parameters">The query's parameters.</param>
    /// <param name="isDownload">Whether the path is under <c>/download/storage/v1/</c>, where <c>alt</c> is <c>media</c> or absent.</param>
    /// <returns>The query, or <see langword="null"/> when <c>alt</c> is not one the path takes, or <c>generation</c> or a
    /// condition is not a whole number.</returns>
    public static GcsJsonObjectQuery? Read(IReadOnlyList<KeyValuePair<string, string>> parameters, bool isDownload)
    {
        var alt = GcsQueryParameters.Value(parameters, "alt");
        var isMedia = alt == "media" || (isDownload && alt is null);
        var isAltValid = isMedia || (alt is null or "json" && !isDownload);
        var generationText = GcsQueryParameters.Value(parameters, "generation");
        long? generation = null;
        if (generationText is not null)
        {
            if (!long.TryParse(generationText, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            {
                return null;
            }

            generation = number;
        }

        return isAltValid && GcsJsonConditions.Read(parameters) is { } conditions
            ? new GcsJsonObjectQuery(conditions, generation, isMedia)
            : null;
    }
}
