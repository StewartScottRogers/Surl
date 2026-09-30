using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// Reads the upstream curl recordings embedded from <c>Fixtures/</c> (see its README).
/// </summary>
internal static class RecordedFixture
{
    /// <summary>
    /// The request head a case recorded, as the HTTP server hands it to the policy: the method
    /// and target from the request line, and every field, each value read one byte per
    /// character (Latin-1) as the HTTP server reads it.
    /// </summary>
    public static HttpAuthenticationRequest ReadRequest(string caseName) =>
        ReadHead(Encoding.Latin1.GetString(ReadBytes(caseName, "request.bin")));

    /// <summary>
    /// The <paramref name="requestNumber"/>th request of a <c>-ResponsesPerConnection</c>
    /// recording (<c>request-&lt;n&gt;.bin</c>), read as <see cref="ReadRequest(string)"/> reads one.
    /// </summary>
    public static HttpAuthenticationRequest ReadRequest(string caseName, int requestNumber) =>
        ReadHead(Encoding.Latin1.GetString(ReadBytes(caseName, $"request-{requestNumber}.bin")));

    /// <summary>
    /// The request head of a case's last connection, read as <see cref="ReadRequest"/> reads
    /// the first: the one carrying the answer to a challenge (a <c>-Connections 2</c> recording).
    /// </summary>
    public static HttpAuthenticationRequest ReadLastRequest(string caseName)
    {
        var blocks = Encoding.Latin1.GetString(ReadBytes(caseName, "request.bin")).Split("\r\n\r\n");

        return ReadHead(blocks.Last(block => block.Split("\r\n")[0].EndsWith(" HTTP/1.1", StringComparison.Ordinal)));
    }

    private static HttpAuthenticationRequest ReadHead(string text)
    {
        var lines = text
            .Split("\r\n")
            .TakeWhile(line => line.Length > 0)
            .ToList();
        var requestLine = lines[0].Split(' ');
        var fields = lines
            .Skip(1)
            .Select(line => line.Split(':', 2))
            .Select(parts => new KeyValuePair<string, string>(parts[0], parts[1].Trim(' ')))
            .ToList();

        return new HttpAuthenticationRequest(requestLine[0], requestLine[1], requestLine[0] is not ("GET" or "HEAD"), fields);
    }

    /// <summary>
    /// The value of the one <c>Authorization</c> field a case recorded.
    /// </summary>
    public static string ReadAuthorization(string caseName) =>
        ReadRequest(caseName).Fields.Single(field => field.Key == "Authorization").Value;

    /// <summary>
    /// One of a case's text files (<c>exitcode.txt</c>, <c>stderr.txt</c>), read as UTF-8.
    /// </summary>
    public static string ReadText(string caseName, string fileName) =>
        Encoding.UTF8.GetString(ReadBytes(caseName, fileName));

    private static byte[] ReadBytes(string caseName, string fileName)
    {
        using var stream = typeof(RecordedFixture).Assembly.GetManifestResourceStream($"Fixtures/{caseName}/{fileName}")
            ?? throw new InvalidOperationException($"No embedded fixture Fixtures/{caseName}/{fileName}.");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);

        return copy.ToArray();
    }
}
