using System.Text;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// The Data Lake <c>x-ms-properties</c> field ("Path - Update", "Path - Get Properties"; ADR-0109,
/// decision 1): a comma-separated list of <c>name=value</c> pairs, each value the base64 of its
/// UTF-8 bytes. The properties are the file's user metadata - the same entries the Blob endpoint
/// sends as <c>x-ms-meta-*</c> - kept in the object metadata index.
/// </summary>
internal static class AzurePropertiesField
{
    /// <summary>
    /// The field's name.
    /// </summary>
    public const string Name = "x-ms-properties";

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Reads a field into the properties it names.
    /// </summary>
    /// <param name="value">The field's value; empty for no properties.</param>
    /// <returns>The properties by name, or <see langword="null"/> when a pair has no <c>=</c>, its
    /// name is no C# identifier or is given twice, or its value is not base64 of UTF-8.</returns>
    public static Dictionary<string, string>? Parse(string value)
    {
        var properties = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var equals = pair.IndexOf('=');
            var decoded = equals > 0 ? DecodeValue(pair[(equals + 1)..]) : null;
            if (decoded is null || !AzurePutBlob.IsMetadataName(pair[..equals]) || !properties.TryAdd(pair[..equals], decoded))
            {
                return null;
            }
        }

        return properties;
    }

    /// <summary>
    /// Writes properties as the field's value.
    /// </summary>
    /// <param name="properties">The properties, in the order they are written.</param>
    /// <returns>The value, or <see langword="null"/> when there are none, so the field is not sent.</returns>
    public static string? Format(IReadOnlyList<KeyValuePair<string, string>> properties) =>
        properties.Count == 0
            ? null
            : string.Join(',', properties.Select(property => $"{property.Key}={Convert.ToBase64String(Encoding.UTF8.GetBytes(property.Value))}"));

    private static string? DecodeValue(string base64)
    {
        var bytes = new byte[base64.Length];
        if (!Convert.TryFromBase64String(base64, bytes, out var written))
        {
            return null;
        }

        try
        {
            return StrictUtf8.GetString(bytes, 0, written);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }
}
