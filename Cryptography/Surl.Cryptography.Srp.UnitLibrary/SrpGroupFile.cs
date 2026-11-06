namespace Surl.Cryptography.Srp;

/// <summary>
/// Reads a GnuTLS <c>tpasswd.conf</c> file, the groups a <c>tpasswd</c> file's index column names
/// (ADR-0122 decision 4): one <c>index:N:g</c> line per group, <c>N</c> and <c>g</c> in
/// <see cref="GnuTlsSrpBase64" />. Text in, groups out; the caller reads the file.
/// </summary>
public static class SrpGroupFile
{
    private const int FieldCount = 3;

    /// <summary>Parses the file's text into its groups by index.</summary>
    /// <param name="text">The whole file.</param>
    /// <exception cref="SrpFileFormatException">
    /// A line without three fields, an empty or repeated index, a bad number, or a generator that is
    /// not between one and <c>N</c>.
    /// </exception>
    public static IReadOnlyDictionary<string, SrpGroup> Parse(string text)
    {
        var groups = new Dictionary<string, SrpGroup>(StringComparer.Ordinal);
        foreach (var (lineNumber, fields) in SrpFileLines.Split(text, FieldCount))
        {
            if (fields[0].Length == 0 || groups.ContainsKey(fields[0]))
            {
                throw new SrpFileFormatException(lineNumber, $"the index '{fields[0]}' is empty or repeated");
            }

            groups.Add(fields[0], ParseGroup(lineNumber, fields));
        }

        return groups;
    }

    private static SrpGroup ParseGroup(int lineNumber, string[] fields)
    {
        var prime = Srp6a.FromUnsignedBigEndian(SrpFileLines.DecodeField(lineNumber, fields[1], "prime"));
        var generator = Srp6a.FromUnsignedBigEndian(SrpFileLines.DecodeField(lineNumber, fields[2], "generator"));
        if (prime <= generator || generator <= 1)
        {
            throw new SrpFileFormatException(lineNumber, "the generator is not greater than one and less than the prime");
        }

        return new SrpGroup(prime, generator);
    }
}
