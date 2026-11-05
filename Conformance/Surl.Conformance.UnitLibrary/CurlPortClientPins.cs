using System.Text.Json;

namespace Surl.Conformance;

/// <summary>
/// Reads the text of <c>CurlPortBuilds.json</c> into the Curl port clients it pins
/// (ADR-0083 decision 2).
/// </summary>
/// <remarks>
/// Kept apart from <see cref="UpstreamCurlBuildPins"/> on purpose: the two files never share a
/// type, so a port pin can never reach an upstream curl test, and the reverse. Parsed with
/// <see cref="JsonDocument"/> so the library stays native-AOT compatible; any shape the file
/// must not have is a <see cref="FormatException"/> naming the problem.
/// </remarks>
public static class CurlPortClientPins
{
    /// <summary>
    /// The name of the pin file at the repository root.
    /// </summary>
    public const string FileName = "CurlPortBuilds.json";

    private const int Sha256HexLength = 64;

    /// <summary>
    /// Parses the text of <c>CurlPortBuilds.json</c>.
    /// </summary>
    /// <param name="json">The file's text.</param>
    /// <returns>Every entry of the <c>builds</c> array, in file order.</returns>
    /// <exception cref="FormatException">
    /// The text is not JSON, has no <c>builds</c> array, or an entry lacks one of the six fields
    /// ADR-0083 decision 2 names or has one of the wrong shape.
    /// </exception>
    public static IReadOnlyList<PinnedCurlPortClient> Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        using var document = ParseDocument(json);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("builds", out var builds)
            || builds.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException($"{FileName} has no builds array.");
        }

        var pins = new List<PinnedCurlPortClient>();

        foreach (var entry in builds.EnumerateArray())
        {
            pins.Add(ParseClient(entry, pins.Count));
        }

        return pins;
    }

    private static JsonDocument ParseDocument(string json)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new FormatException($"{FileName} is not valid JSON: {exception.Message}", exception);
        }
    }

    private static PinnedCurlPortClient ParseClient(JsonElement entry, int index)
    {
        if (entry.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException($"Entry {index} of builds in {FileName} is not an object.");
        }

        return new PinnedCurlPortClient(
            RequiredString(entry, index, "platform"),
            RequiredString(entry, index, "path"),
            ParseSha256(RequiredString(entry, index, "sha256"), index),
            RequiredString(entry, index, "commit"),
            RequiredString(entry, index, "publishCommand"),
            RequiredString(entry, index, "published"));
    }

    private static string RequiredString(JsonElement entry, int index, string name)
    {
        if (!entry.TryGetProperty(name, out var value))
        {
            throw new FormatException($"Entry {index} of builds in {FileName} has no {name}.");
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new FormatException($"The {name} of entry {index} of builds in {FileName} is not a string.");
        }

        var text = value.GetString()!;

        return string.IsNullOrWhiteSpace(text)
            ? throw new FormatException($"Entry {index} of builds in {FileName} has no {name}.")
            : text;
    }

    private static string ParseSha256(string sha256, int index) =>
        sha256.Length == Sha256HexLength && sha256.All(char.IsAsciiHexDigit)
            ? sha256
            : throw new FormatException($"The sha256 of entry {index} of builds in {FileName} is not {Sha256HexLength} hexadecimal digits.");
}
