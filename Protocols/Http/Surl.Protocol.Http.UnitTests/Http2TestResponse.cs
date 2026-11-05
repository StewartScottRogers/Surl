using Surl.Hpack;

namespace Surl.Protocol.Http;

/// <summary>
/// What the server wrote on one HTTP/2 stream, decoded: its header blocks (an interim
/// <c>:status 100</c> first, when there is one), its body, and how it ended.
/// </summary>
internal sealed class Http2TestResponse
{
    public List<IReadOnlyList<HpackHeaderField>> HeaderBlocks { get; } = [];

    public List<byte> Body { get; } = [];

    public List<int> DataFrameLengths { get; } = [];

    public bool EndedOnHeaders { get; set; }

    public bool EndedOnData { get; set; }

    public Http2ErrorCode? ResetCode { get; set; }

    /// <summary>The final header block's fields.</summary>
    public IReadOnlyList<HpackHeaderField> Fields => HeaderBlocks[^1];

    public string Status => Fields.Single(header => header.Name == ":status").Value;

    public string BodyText => HttpServerHarness.Latin1([.. Body]);
}
