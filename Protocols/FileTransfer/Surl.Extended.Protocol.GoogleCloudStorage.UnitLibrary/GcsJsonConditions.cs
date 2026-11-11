using System.Globalization;
using Surl.Content;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// The conditions of a JSON API read or delete (ADR-0148, decision 5; "Objects: get"): the four generation and
/// metageneration query parameters, then the HTTP <c>If-*</c> fields, judged against the object's current
/// generation and metageneration.
/// </summary>
/// <param name="GenerationMatch">The <c>ifGenerationMatch</c> value, or <see langword="null"/>.</param>
/// <param name="GenerationNotMatch">The <c>ifGenerationNotMatch</c> value, or <see langword="null"/>.</param>
/// <param name="MetagenerationMatch">The <c>ifMetagenerationMatch</c> value, or <see langword="null"/>.</param>
/// <param name="MetagenerationNotMatch">The <c>ifMetagenerationNotMatch</c> value, or <see langword="null"/>.</param>
internal sealed record GcsJsonConditions(long? GenerationMatch, long? GenerationNotMatch, long? MetagenerationMatch, long? MetagenerationNotMatch)
{
    /// <summary>
    /// Reads the four parameters.
    /// </summary>
    /// <param name="parameters">The query's parameters.</param>
    /// <returns>The conditions, or <see langword="null"/> when a value given is not a whole number.</returns>
    public static GcsJsonConditions? Read(IReadOnlyList<KeyValuePair<string, string>> parameters)
    {
        if (TryRead(parameters, "ifGenerationMatch", out var generationMatch)
            && TryRead(parameters, "ifGenerationNotMatch", out var generationNotMatch)
            && TryRead(parameters, "ifMetagenerationMatch", out var metagenerationMatch)
            && TryRead(parameters, "ifMetagenerationNotMatch", out var metagenerationNotMatch))
        {
            return new GcsJsonConditions(generationMatch, generationNotMatch, metagenerationMatch, metagenerationNotMatch);
        }

        return null;
    }

    /// <summary>
    /// Judges the conditions against an existing object: a failing <c>if*Match</c> or <c>If-Match</c> or
    /// <c>If-Unmodified-Since</c> is <see cref="GcsConditionOutcome.PreconditionFailed"/>; then a matching
    /// <c>if*NotMatch</c>, <c>If-None-Match</c> or an unmodified <c>If-Modified-Since</c> is
    /// <see cref="GcsConditionOutcome.NotModified"/>.
    /// </summary>
    /// <param name="fields">The request's fields.</param>
    /// <param name="current">The object's current properties.</param>
    /// <returns>The outcome.</returns>
    public GcsConditionOutcome Evaluate(IReadOnlyList<KeyValuePair<string, string>> fields, ObjectProperties current)
    {
        if ((GenerationMatch is { } generation && generation != current.Generation)
            || (MetagenerationMatch is { } metageneration && metageneration != current.Metageneration))
        {
            return GcsConditionOutcome.PreconditionFailed;
        }

        var fromFields = GcsConditions.Evaluate(fields, current.ETag, current.LastWriteTimeUtc);
        if (fromFields == GcsConditionOutcome.PreconditionFailed)
        {
            return fromFields;
        }

        return GenerationNotMatch == current.Generation || MetagenerationNotMatch == current.Metageneration
            ? GcsConditionOutcome.NotModified
            : fromFields;
    }

    /// <summary>
    /// Judges the conditions of a write that makes the object, which may not exist yet: with no object, only "none" conditions hold -
    /// no <c>ifGenerationMatch</c> but 0 and no <c>ifMetagenerationMatch</c>; with one, <see cref="Evaluate"/> must serve.
    /// </summary>
    /// <param name="fields">The request's fields.</param>
    /// <param name="current">The object there now, or <see langword="null"/> when none.</param>
    /// <returns>Whether the write may go ahead.</returns>
    public bool AreMetByWrite(IReadOnlyList<KeyValuePair<string, string>> fields, ObjectProperties? current) =>
        current is null
            ? GenerationMatch is null or 0 && MetagenerationMatch is null
            : Evaluate(fields, current) == GcsConditionOutcome.Serve;

    // A parameter not sent reads as null; one sent must be a whole number.
    private static bool TryRead(IReadOnlyList<KeyValuePair<string, string>> parameters, string name, out long? value)
    {
        value = null;
        if (GcsQueryParameters.Value(parameters, name) is not { } text)
        {
            return true;
        }

        if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            return false;
        }

        value = number;

        return true;
    }
}
