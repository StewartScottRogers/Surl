using System.Globalization;
using Surl.Content;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Checks <c>x-goog-if-generation-match</c> and <c>x-goog-if-metageneration-match</c> ("HTTP
/// headers and common request headers"; ADR-0110, decision 1) on every object operation: a value
/// that is not a number is <c>400 InvalidArgument</c>, and one the object does not have is
/// <c>412 PreconditionFailed</c>. A generation of <c>0</c> holds only when no object exists.
/// </summary>
internal static class GcsGenerationConditions
{
    private const string GenerationMatch = "x-goog-if-generation-match";
    private const string MetagenerationMatch = "x-goog-if-metageneration-match";

    /// <summary>
    /// Whether the request carries either condition, so the object's generation must be read.
    /// </summary>
    /// <param name="fields">The request's fields.</param>
    /// <returns><see langword="true"/> when either field is sent.</returns>
    public static bool AreGiven(IReadOnlyList<KeyValuePair<string, string>> fields) =>
        GcsRequestFields.Value(fields, GenerationMatch) is not null || GcsRequestFields.Value(fields, MetagenerationMatch) is not null;

    /// <summary>
    /// Checks both conditions against the object.
    /// </summary>
    /// <param name="fields">The request's fields.</param>
    /// <param name="current">The object, or <see langword="null"/> when none exists.</param>
    /// <returns><see langword="null"/> when both hold or are not sent, else the error.</returns>
    public static GcsError? Check(IReadOnlyList<KeyValuePair<string, string>> fields, ObjectProperties? current)
    {
        var generationText = GcsRequestFields.Value(fields, GenerationMatch);
        var metagenerationText = GcsRequestFields.Value(fields, MetagenerationMatch);
        if (!TryRead(generationText, out var generation) || !TryRead(metagenerationText, out var metageneration))
        {
            return GcsError.InvalidArgument;
        }

        var holds = (generation is null || generation == (current?.Generation ?? 0))
            && (metageneration is null || (current is not null && metageneration == GcsGeneration.Metageneration));

        return holds ? null : GcsError.PreconditionFailed;
    }

    // A field not sent reads as null; one sent must be a whole number.
    private static bool TryRead(string? text, out long? value)
    {
        value = null;
        if (text is null)
        {
            return true;
        }

        if (!long.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            return false;
        }

        value = number;
        return true;
    }
}
