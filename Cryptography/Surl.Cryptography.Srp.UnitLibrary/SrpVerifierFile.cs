namespace Surl.Cryptography.Srp;

/// <summary>
/// Reads a GnuTLS <c>tpasswd</c> file (ADR-0122 decision 4): one <c>user:verifier:salt:index</c> line
/// per account, the verifier and salt in <see cref="GnuTlsSrpBase64" />, the index naming a group of
/// the <c>tpasswd.conf</c> file <see cref="SrpGroupFile" /> read. Text in, accounts out, in file
/// order; the caller reads the file and refuses a user given twice.
/// </summary>
public static class SrpVerifierFile
{
    private const int FieldCount = 4;

    /// <summary>Parses the file's text into its accounts.</summary>
    /// <param name="text">The whole file.</param>
    /// <param name="groups">The groups by index, from <see cref="SrpGroupFile.Parse" />.</param>
    /// <exception cref="SrpFileFormatException">
    /// A line without four fields, an empty user name, a bad number, or an index <paramref name="groups" /> lacks.
    /// </exception>
    public static IReadOnlyList<SrpVerifierEntry> Parse(string text, IReadOnlyDictionary<string, SrpGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);
        var entries = new List<SrpVerifierEntry>();
        foreach (var (lineNumber, fields) in SrpFileLines.Split(text, FieldCount))
        {
            entries.Add(ParseEntry(lineNumber, fields, groups));
        }

        return entries;
    }

    private static SrpVerifierEntry ParseEntry(int lineNumber, string[] fields, IReadOnlyDictionary<string, SrpGroup> groups)
    {
        if (fields[0].Length == 0)
        {
            throw new SrpFileFormatException(lineNumber, "the user name is empty");
        }

        var verifier = SrpFileLines.DecodeField(lineNumber, fields[1], "verifier");
        var salt = SrpFileLines.DecodeField(lineNumber, fields[2], "salt");
        if (!groups.TryGetValue(fields[3], out var group))
        {
            throw new SrpFileFormatException(lineNumber, $"the group index '{fields[3]}' is not in the groups file");
        }

        return new SrpVerifierEntry(fields[0], salt, verifier, group);
    }
}
