using System.Text.Json;

namespace Surl.Conformance;

/// <summary>
/// Reads the text of <c>UpstreamCurlBuilds.json</c> into the upstream curl builds it pins.
/// </summary>
/// <remarks>
/// Parsed with <see cref="JsonDocument"/> rather than the reflection-based serializer, so the
/// library stays native-AOT compatible. Any shape the file must not have is a
/// <see cref="FormatException"/> naming the problem, because a pin file that parses loosely
/// would let a wrong build through or refuse every build without saying why.
/// </remarks>
public static class UpstreamCurlBuildPins
{
    /// <summary>
    /// The name of the pin file at the repository root.
    /// </summary>
    public const string FileName = "UpstreamCurlBuilds.json";

    private const int Sha256HexLength = 64;

    /// <summary>
    /// Parses the text of <c>UpstreamCurlBuilds.json</c>.
    /// </summary>
    /// <param name="json">The file's text.</param>
    /// <returns>Every entry of the <c>builds</c> array, in file order.</returns>
    /// <exception cref="FormatException">
    /// The text is not JSON, has no <c>builds</c> array, or an entry lacks a required field or
    /// has one of the wrong shape.
    /// </exception>
    public static IReadOnlyList<PinnedUpstreamCurlBuild> Parse(string json)
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

        var pins = new List<PinnedUpstreamCurlBuild>();

        foreach (var entry in builds.EnumerateArray())
        {
            pins.Add(ParseBuild(entry, pins.Count));
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

    private static PinnedUpstreamCurlBuild ParseBuild(JsonElement entry, int index)
    {
        if (entry.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException($"Entry {index} of builds in {FileName} is not an object.");
        }

        return new PinnedUpstreamCurlBuild(
            RequiredString(entry, index, "platform"),
            RequiredString(entry, index, "defaultPath"),
            ParseSha256(RequiredString(entry, index, "sha256"), index),
            OptionalString(entry, index, "version") ?? string.Empty,
            ParseProtocols(OptionalString(entry, index, "protocols")),
            ParseRole(OptionalString(entry, index, "role"), index));
    }

    private static string RequiredString(JsonElement entry, int index, string name)
    {
        var value = OptionalString(entry, index, name);

        return string.IsNullOrWhiteSpace(value)
            ? throw new FormatException($"Entry {index} of builds in {FileName} has no {name}.")
            : value;
    }

    private static string? OptionalString(JsonElement entry, int index, string name)
    {
        if (!entry.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new FormatException($"The {name} of entry {index} of builds in {FileName} is not a string.");
        }

        return value.GetString();
    }

    private static string ParseSha256(string sha256, int index)
    {
        if (sha256.Length != Sha256HexLength || !sha256.All(char.IsAsciiHexDigit))
        {
            throw new FormatException($"The sha256 of entry {index} of builds in {FileName} is not {Sha256HexLength} hexadecimal digits.");
        }

        return sha256;
    }

    private static string[] ParseProtocols(string? protocols) =>
        (protocols ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static UpstreamCurlBuildRole ParseRole(string? role, int index) => role switch
    {
        null or "reference" => UpstreamCurlBuildRole.Reference,
        "supplementary" => UpstreamCurlBuildRole.Supplementary,
        _ => throw new FormatException($"The role of entry {index} of builds in {FileName} is '{role}', not reference or supplementary."),
    };
}
