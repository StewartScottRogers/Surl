using Surl.HttpServing;

namespace Surl.Control;

/// <summary>
/// Reads a control request's body whole, at most <see cref="ControlEndpoint.MaxBodyBytes"/> (the exchange's upload
/// limit, which <see cref="ControlEndpoint"/> sets), and checks its media type.
/// </summary>
internal static class ControlRequestBody
{
    /// <summary>The body's bytes; empty when none was announced.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The bytes.</returns>
    /// <exception cref="ControlErrorException">The body is past the bound, cannot be framed, or ended early.</exception>
    public static async Task<byte[]> ReadAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!request.Body.IsAnnounced)
        {
            return [];
        }

        if (!request.Body.IsReadable)
        {
            throw new ControlErrorException(ControlError.BadRequest("the request body's transfer coding cannot be read") with { ClosesConnection = true });
        }

        if (request.Body.AnnouncedLength > ControlEndpoint.MaxBodyBytes)
        {
            throw new ControlErrorException(ControlError.TooLarge());
        }

        using var copy = new MemoryStream();
        var outcome = await request.Body.CopyToAsync(copy, cancellationToken);

        return outcome switch
        {
            HttpRequestBodyDiscardOutcome.Discarded => copy.ToArray(),
            HttpRequestBodyDiscardOutcome.TooLarge => throw new ControlErrorException(ControlError.TooLarge()),
            _ => throw new ControlErrorException(ControlError.BadRequest("the request body ended before it was complete") with { ClosesConnection = true }),
        };
    }

    /// <summary>Refuses a body whose <c>Content-Type</c> is not <paramref name="mediaType"/>, parameters aside.</summary>
    /// <param name="request">The request.</param>
    /// <param name="mediaType">The media type the request takes, in lower case.</param>
    /// <exception cref="ControlErrorException"><c>415</c> when the type is absent or another.</exception>
    public static void RequireMediaType(HttpRequest request, string mediaType)
    {
        var contentType = request.Fields
            .Where(field => string.Equals(field.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
            .Select(field => field.Value.Split(';')[0].Trim(' ', '\t'))
            .ToArray();
        if (contentType.Length != 1 || !string.Equals(contentType[0], mediaType, StringComparison.OrdinalIgnoreCase))
        {
            throw new ControlErrorException(ControlError.UnsupportedMediaType(mediaType));
        }
    }
}
