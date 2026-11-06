using System.Text;

namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// One flow's requests answered by a <see cref="CoapResourceAnswerer"/> with its own
/// <see cref="CoapBlock1Uploads"/> and a manual clock, for block-wise exchanges written as
/// RFC 7959 section 3 writes them: <c>2:1/0/128</c> is Block2 NUM 1, M 0, 128 bytes.
/// </summary>
internal sealed class CoapTestExchange : IAsyncDisposable
{
    private static readonly CoapAccessRules Anonymous = new(AllowsAnonymous: true, HasAccounts: false);

    private readonly CoapResourceAnswerer answerer;

    public CoapTestExchange(CoapTestStore store, int preferredSizeExponent = CoapBlockOption.LargestSizeExponent)
    {
        answerer = new CoapResourceAnswerer(store.Store, Anonymous, new UnitTestCoapRandomSource(), preferredSizeExponent);
        Uploads = new CoapBlock1Uploads(store.Store, Clock, preferredSizeExponent);
    }

    public ManualTimeProvider Clock { get; } = new();

    public CoapBlock1Uploads Uploads { get; }

    /// <summary>A file body of <paramref name="length"/> bytes, <c>a</c> to <c>z</c> over and over.</summary>
    public static string Body(int length) => new([.. Enumerable.Range(0, length).Select(index => (char)('a' + (index % 26)))]);

    /// <summary>A Block1 or Block2 option from its RFC 7959 text, such as <c>1/0/128</c>.</summary>
    public static CoapOption Block(int number, string text)
    {
        var parts = text.Split('/');
        var sizeExponent = int.Log2(int.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture)) - 4;

        return new CoapBlockOption(uint.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), parts[1] == "1", sizeExponent).ToOption(number);
    }

    /// <summary>The RFC 7959 text of the response's option <paramref name="number"/>, or null without one.</summary>
    public static string? BlockOf(CoapResponse response, int number) =>
        response.Options.SingleOrDefault(option => option.Number == number) is { } option ? CoapBlockOption.Read(option).ToString() : null;

    /// <summary>The value of the response's uint option <paramref name="number"/>, or null without one.</summary>
    public static uint? UIntOf(CoapResponse response, int number) =>
        response.Options.SingleOrDefault(option => option.Number == number)?.ReadUInt();

    public static string PayloadOf(CoapResponse response) => Encoding.UTF8.GetString(response.Payload.Span);

    public Task<CoapResponse> SendAsync(CoapCode method, string path, params CoapOption[] options) =>
        SendWithBodyAsync(method, path, string.Empty, options);

    public async Task<CoapResponse> SendWithBodyAsync(CoapCode method, string path, string body, params CoapOption[] options)
    {
        CoapOption[] all =
        [
            .. path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(segment => CoapOption.FromString(CoapOptionNumber.UriPath, segment)),
            .. options,
        ];
        var request = new CoapMessage(CoapMessageType.Confirmable, method, 1, new byte[] { 0xC0 }, all, Encoding.UTF8.GetBytes(body));

        return await answerer.AnswerAsync(request, CoapRequestOptions.Read(request), "coap", peerIsAuthenticated: false, Uploads, CancellationToken.None);
    }

    public ValueTask DisposeAsync() => Uploads.DisposeAsync();
}
