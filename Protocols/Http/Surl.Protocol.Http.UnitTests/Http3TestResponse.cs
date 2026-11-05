using Surl.Qpack;

namespace Surl.Protocol.Http;

/// <summary>What the server sent on one HTTP/3 request stream, decoded by <see cref="Http3TestClient.Response"/>.</summary>
internal sealed class Http3TestResponse
{
    /// <summary>Each <c>HEADERS</c> frame's field lines, in order: interim answers, the answer.</summary>
    public List<IReadOnlyList<QpackFieldLine>> FieldSections { get; } = [];

    /// <summary>The content of every <c>DATA</c> frame.</summary>
    public List<byte> Body { get; } = [];

    /// <summary>Whether the stream ended with FIN.</summary>
    public bool Fin { get; set; }

    /// <summary>The last field section.</summary>
    public IReadOnlyList<QpackFieldLine> Fields => FieldSections[^1];

    public string Status => Fields.Single(line => line.Name == ":status").Value;

    public string BodyText => HttpServerHarness.Latin1([.. Body]);

    /// <summary>The regular fields as <c>name: value</c>, as HTTP/2's tests compare them with HTTP/1.1's.</summary>
    public List<string> RegularFields => [.. Fields.Where(line => !line.Name.StartsWith(':')).Select(line => $"{line.Name}: {line.Value}")];
}
