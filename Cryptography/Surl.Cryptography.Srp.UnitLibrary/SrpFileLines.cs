namespace Surl.Cryptography.Srp;

/// <summary>
/// Splits a GnuTLS SRP file into its colon-separated lines, numbering each from one, skipping blank
/// lines and dropping a carriage return before the line feed.
/// </summary>
internal static class SrpFileLines
{
    internal static IEnumerable<(int LineNumber, string[] Fields)> Split(string text, int fieldCount)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = text.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields = line.Split(':');
            if (fields.Length != fieldCount)
            {
                throw new SrpFileFormatException(index + 1, $"expected {fieldCount} fields separated by ':' but found {fields.Length}");
            }

            yield return (index + 1, fields);
        }
    }

    internal static byte[] DecodeField(int lineNumber, string field, string name) =>
        GnuTlsSrpBase64.Decode(field)
            ?? throw new SrpFileFormatException(lineNumber, $"the {name} is not a GnuTLS SRP base64 number");
}
