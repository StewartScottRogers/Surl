using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json;

namespace Surl.McpServing;

/// <summary>
/// One node of a tool's input schema, read once when the tool is added, and the check of a value against it
/// (ADR-0103 decision 1). It supports exactly this subset of JSON Schema and refuses a schema using any other
/// keyword, so a check is never silently skipped: <c>type</c>, <c>properties</c>, <c>required</c>,
/// <c>additionalProperties</c> (a boolean), <c>items</c>, <c>enum</c> (strings), <c>minimum</c>, <c>maximum</c>,
/// <c>minItems</c>, <c>maxItems</c>, <c>minLength</c>, <c>maxLength</c>, and <c>description</c>, <c>default</c>
/// and <c>title</c>, which are documentation only.
/// </summary>
internal sealed class McpToolInputSchema
{
    private static readonly FrozenSet<string> Types =
        FrozenSet.ToFrozenSet(["object", "string", "integer", "number", "boolean", "array"], StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, Action<McpToolInputSchema, JsonElement, string>> KeywordReaders =
        new Dictionary<string, Action<McpToolInputSchema, JsonElement, string>>(StringComparer.Ordinal)
        {
            ["type"] = (schema, value, path) => schema._type = ReadType(value, path),
            ["properties"] = (schema, value, path) => schema.ReadProperties(value, path),
            ["required"] = (schema, value, path) => schema._required = ReadStrings(value, path),
            ["additionalProperties"] = (schema, value, path) => schema._additionalPropertiesAllowed = ReadBoolean(value, path),
            ["items"] = (schema, value, path) => schema._items = Read(value, path),
            ["enum"] = (schema, value, path) => schema._enum = ReadStrings(value, path),
            ["minimum"] = (schema, value, path) => schema._minimum = ReadNumber(value, path),
            ["maximum"] = (schema, value, path) => schema._maximum = ReadNumber(value, path),
            ["minItems"] = (schema, value, path) => schema._minItems = ReadCount(value, path),
            ["maxItems"] = (schema, value, path) => schema._maxItems = ReadCount(value, path),
            ["minLength"] = (schema, value, path) => schema._minLength = ReadCount(value, path),
            ["maxLength"] = (schema, value, path) => schema._maxLength = ReadCount(value, path),
            ["description"] = (_, value, path) => RequireKind(value, JsonValueKind.String, path, "a string"),
            ["title"] = (_, value, path) => RequireKind(value, JsonValueKind.String, path, "a string"),
            ["default"] = (_, _, _) => { },
        }.ToFrozenDictionary(StringComparer.Ordinal);

    private readonly Dictionary<string, McpToolInputSchema> _properties = new(StringComparer.Ordinal);

    private string? _type;

    private string[] _required = [];

    private bool _additionalPropertiesAllowed = true;

    private McpToolInputSchema? _items;

    private string[]? _enum;

    private double? _minimum;

    private double? _maximum;

    private int? _minItems;

    private int? _maxItems;

    private int? _minLength;

    private int? _maxLength;

    private McpToolInputSchema()
    {
    }

    /// <summary>Reads a tool's input schema, which must be a JSON object whose <c>type</c> is <c>"object"</c>.</summary>
    /// <param name="schemaText">The schema as JSON text.</param>
    /// <param name="schemaJson">The schema as parsed JSON, written as the tool's <c>inputSchema</c>.</param>
    /// <returns>The schema's root node.</returns>
    /// <exception cref="ArgumentException">The text is not JSON, not an object schema, or uses a keyword or value the core does not check.</exception>
    internal static McpToolInputSchema ReadToolSchema(string schemaText, out JsonElement schemaJson)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(schemaText);
            schemaJson = document.RootElement.Clone();
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("The input schema is not JSON.", exception);
        }

        McpToolInputSchema schema = Read(schemaJson, "inputSchema");
        if (schema._type != "object")
        {
            throw new ArgumentException("inputSchema: the type must be \"object\".");
        }

        return schema;
    }

    /// <summary>Checks a value against this node.</summary>
    /// <param name="value">The value.</param>
    /// <param name="path">Where the value sits, e.g. <c>arguments.listen_urls[0]</c>.</param>
    /// <returns>The first failure as <c>path: what was expected</c>, or <see langword="null" /> when the value passes.</returns>
    internal string? Check(JsonElement value, string path) => CheckTypeAndEnum(value, path) ?? CheckByKind(value, path);

    private string? CheckTypeAndEnum(JsonElement value, string path)
    {
        if (_type is { } type && !IsOfType(value, type))
        {
            return $"{path}: expected {Article(type)} {type}";
        }

        return _enum is { } allowed && !IsOneOf(value, allowed) ? $"{path}: expected one of {string.Join(", ", allowed)}" : null;
    }

    private string? CheckByKind(JsonElement value, string path) =>
        value.ValueKind switch
        {
            JsonValueKind.Object => CheckObject(value, path),
            JsonValueKind.Array => CheckArray(value, path),
            JsonValueKind.String => CheckString(value, path),
            JsonValueKind.Number => CheckNumber(value, path),
            _ => null,
        };

    private static McpToolInputSchema Read(JsonElement element, string path)
    {
        RequireKind(element, JsonValueKind.Object, path, "a schema object");
        McpToolInputSchema schema = new();
        foreach (JsonProperty keyword in element.EnumerateObject())
        {
            if (!KeywordReaders.TryGetValue(keyword.Name, out Action<McpToolInputSchema, JsonElement, string>? read))
            {
                throw new ArgumentException($"{path}: the keyword {keyword.Name} is not one the core checks.");
            }

            read(schema, keyword.Value, $"{path}.{keyword.Name}");
        }

        return schema;
    }

    private void ReadProperties(JsonElement value, string path)
    {
        RequireKind(value, JsonValueKind.Object, path, "an object");
        foreach (JsonProperty property in value.EnumerateObject())
        {
            _properties[property.Name] = Read(property.Value, $"{path}.{property.Name}");
        }
    }

    private static string ReadType(JsonElement value, string path)
    {
        RequireKind(value, JsonValueKind.String, path, "a string");
        string type = value.GetString()!;
        return Types.Contains(type) ? type : throw new ArgumentException($"{path}: the type {type} is not one the core checks.");
    }

    private static string[] ReadStrings(JsonElement value, string path)
    {
        RequireKind(value, JsonValueKind.Array, path, "an array of strings");
        foreach (JsonElement entry in value.EnumerateArray())
        {
            RequireKind(entry, JsonValueKind.String, path, "an array of strings");
        }

        return [.. value.EnumerateArray().Select(entry => entry.GetString()!)];
    }

    private static bool ReadBoolean(JsonElement value, string path) => value.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => throw new ArgumentException($"{path}: must be a boolean."),
    };

    private static double ReadNumber(JsonElement value, string path)
    {
        RequireKind(value, JsonValueKind.Number, path, "a number");
        return ParseNumber(value);
    }

    // A number too large for a double reads as infinite, so it fails every finite bound and is no integer.
    private static double ParseNumber(JsonElement number) =>
        double.Parse(number.GetRawText(), NumberStyles.Float, CultureInfo.InvariantCulture);

    private static int ReadCount(JsonElement value, string path) =>
        value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int count) && count >= 0
            ? count
            : throw new ArgumentException($"{path}: must be a non-negative integer.");

    private static void RequireKind(JsonElement value, JsonValueKind kind, string path, string what)
    {
        if (value.ValueKind != kind)
        {
            throw new ArgumentException($"{path}: must be {what}.");
        }
    }

    private static bool IsOfType(JsonElement value, string type) => type switch
    {
        "object" => value.ValueKind == JsonValueKind.Object,
        "array" => value.ValueKind == JsonValueKind.Array,
        "string" => value.ValueKind == JsonValueKind.String,
        "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "number" => value.ValueKind == JsonValueKind.Number,
        _ => IsInteger(value),
    };

    // JSON Schema counts a number with no fractional part as an integer, 1.0 included.
    private static bool IsInteger(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number)
        {
            return false;
        }

        double number = ParseNumber(value);
        return double.IsFinite(number) && Math.Floor(number) == number;
    }

    private static string Article(string type) => type[0] is 'a' or 'o' or 'i' ? "an" : "a";

    private static bool IsOneOf(JsonElement value, string[] allowed) =>
        value.ValueKind == JsonValueKind.String && allowed.Contains(value.GetString(), StringComparer.Ordinal);

    private string? CheckObject(JsonElement value, string path)
    {
        foreach (string name in _required)
        {
            if (!value.TryGetProperty(name, out _))
            {
                return $"{path}.{name}: required";
            }
        }

        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (CheckProperty(property, $"{path}.{property.Name}") is { } failure)
            {
                return failure;
            }
        }

        return null;
    }

    private string? CheckProperty(JsonProperty property, string path)
    {
        if (_properties.TryGetValue(property.Name, out McpToolInputSchema? schema))
        {
            return schema.Check(property.Value, path);
        }

        return _additionalPropertiesAllowed ? null : $"{path}: not allowed";
    }

    private string? CheckArray(JsonElement value, string path)
    {
        int length = value.GetArrayLength();
        if (CheckCount(length, _minItems, _maxItems, "items") is { } countFailure)
        {
            return $"{path}: {countFailure}";
        }

        if (_items is null)
        {
            return null;
        }

        int index = 0;
        foreach (JsonElement entry in value.EnumerateArray())
        {
            if (_items.Check(entry, $"{path}[{index}]") is { } failure)
            {
                return failure;
            }

            index++;
        }

        return null;
    }

    // JSON Schema measures a string's length in Unicode code points.
    private string? CheckString(JsonElement value, string path) =>
        CheckCount(value.GetString()!.EnumerateRunes().Count(), _minLength, _maxLength, "characters") is { } failure
            ? $"{path}: {failure}"
            : null;

    private static string? CheckCount(int count, int? minimum, int? maximum, string unit)
    {
        if (count < minimum)
        {
            return $"expected at least {minimum} {unit}";
        }

        return count > maximum ? $"expected at most {maximum} {unit}" : null;
    }

    private string? CheckNumber(JsonElement value, string path)
    {
        double number = ParseNumber(value);
        if (number < _minimum)
        {
            return $"{path}: expected at least {_minimum.Value.ToString(CultureInfo.InvariantCulture)}";
        }

        return number > _maximum ? $"{path}: expected at most {_maximum.Value.ToString(CultureInfo.InvariantCulture)}" : null;
    }
}
