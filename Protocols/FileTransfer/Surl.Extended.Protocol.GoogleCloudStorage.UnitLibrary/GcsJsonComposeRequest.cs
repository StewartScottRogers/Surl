using System.Globalization;
using System.Text.Json;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// The body of <c>storage.objects.compose</c> (ADR-0148, decision 7): <c>sourceObjects</c>, each with a <c>name</c> and optionally a
/// <c>generation</c> and <c>objectPreconditions.ifGenerationMatch</c>, and the <c>destination</c>'s writable metadata.
/// </summary>
/// <param name="Sources">The sources, in order.</param>
/// <param name="Destination">The destination's fields, or <see langword="null"/> when the body names none.</param>
internal sealed record GcsJsonComposeRequest(IReadOnlyList<GcsJsonComposeRequest.Source> Sources, GcsJsonObjectFields? Destination)
{
    /// <summary>
    /// The most sources one compose takes.
    /// </summary>
    public const int MaxSources = 32;

    /// <summary>
    /// One source object.
    /// </summary>
    /// <param name="Name">The source's name in the bucket.</param>
    /// <param name="Generation">The generation it must be, or <see langword="null"/> for the current one.</param>
    /// <param name="IfGenerationMatch">The generation it must be for the compose to go ahead, or <see langword="null"/>.</param>
    internal sealed record Source(string Name, long? Generation, long? IfGenerationMatch);

    /// <summary>
    /// Reads a compose body.
    /// </summary>
    /// <param name="json">The body's UTF-8 bytes.</param>
    /// <returns>The request and no refusal; or the refusal: <c>required</c> for no sources, <c>invalid</c> for more than
    /// <see cref="MaxSources"/>, a body that is not JSON of this shape, or a source with no name.</returns>
    public static (GcsJsonComposeRequest? Request, GcsJsonError? Refusal) Read(byte[] json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);

            return ReadRoot(document.RootElement);
        }
        catch (JsonException)
        {
            return (null, GcsJsonError.Invalid);
        }
    }

    private static (GcsJsonComposeRequest? Request, GcsJsonError? Refusal) ReadRoot(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return (null, GcsJsonError.Invalid);
        }

        var hasList = root.TryGetProperty("sourceObjects", out var list) && list.ValueKind != JsonValueKind.Null;
        if (!hasList || (list.ValueKind == JsonValueKind.Array && list.GetArrayLength() == 0))
        {
            return (null, GcsJsonError.Required);
        }

        var (destination, isDestinationValid) = ReadDestination(root);
        if (list.ValueKind != JsonValueKind.Array || list.GetArrayLength() > MaxSources || !isDestinationValid)
        {
            return (null, GcsJsonError.Invalid);
        }

        var sources = list.EnumerateArray().Select(ReadSource).ToList();

        return sources.Contains(null) ? (null, GcsJsonError.Invalid) : (new GcsJsonComposeRequest(sources!, destination), null);
    }

    // No destination, or a null one, names no fields; one that is no object of the writable members is invalid.
    private static (GcsJsonObjectFields? Fields, bool IsValid) ReadDestination(JsonElement root)
    {
        if (!root.TryGetProperty("destination", out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return (null, true);
        }

        var fields = GcsJsonObjectFields.From(element);

        return (fields, fields is not null);
    }

    private static Source? ReadSource(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(name.GetString())
            || !TryNumber(element, "generation", out var generation))
        {
            return null;
        }

        long? match = null;
        if (element.TryGetProperty("objectPreconditions", out var preconditions) && preconditions.ValueKind == JsonValueKind.Object)
        {
            return TryNumber(preconditions, "ifGenerationMatch", out match) ? new Source(name.GetString()!, generation, match) : null;
        }

        return new Source(name.GetString()!, generation, match);
    }

    // A member that is absent or null is none; one present is a whole number, as a JSON number or, as 64-bit values are sent, a string.
    private static bool TryNumber(JsonElement element, string member, out long? value)
    {
        value = null;
        if (!element.TryGetProperty(member, out var found) || found.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        var text = found.ValueKind is JsonValueKind.String or JsonValueKind.Number ? found.ToString() : null;
        if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            return false;
        }

        value = number;

        return true;
    }
}
