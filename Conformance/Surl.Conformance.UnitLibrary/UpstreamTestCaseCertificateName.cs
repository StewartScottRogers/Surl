namespace Surl.Conformance;

/// <summary>
/// Reads the certificate a case's <c>&lt;server&gt;</c> line names: FILEFORMAT.md says a server
/// that needs a special certificate has the PEM file's name appended to its name, separated by a
/// space (<c>https test-localhost0h.pem</c>).
/// </summary>
public static class UpstreamTestCaseCertificateName
{
    private const string PemExtension = ".pem";

    /// <summary>
    /// Finds the certificate description a case's <c>https</c> server line names.
    /// </summary>
    /// <param name="testCase">The parsed case.</param>
    /// <returns>
    /// The description name, the PEM file's name without <c>.pem</c> (<c>test-localhost0h</c>), or
    /// <see langword="null"/> when no <c>https</c> line names a certificate.
    /// </returns>
    public static string? Find(UpstreamTestCase testCase)
    {
        ArgumentNullException.ThrowIfNull(testCase);

        return (testCase.FindPart("client", "server")?.Lines ?? [])
            .Select(line => line.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            .Where(words => words.Length > 1 && words[0].Equals("https", StringComparison.OrdinalIgnoreCase) && words[1].EndsWith(PemExtension, StringComparison.Ordinal))
            .Select(words => words[1][..^PemExtension.Length])
            .FirstOrDefault();
    }
}
