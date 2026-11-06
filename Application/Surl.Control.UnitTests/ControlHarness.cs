using System.Net;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Control;

/// <summary>
/// Drives a <see cref="ControlEndpoint"/> through <see cref="InMemoryConnection"/> and the real
/// <c>Http1ConnectionServer</c>, so every test sees the bytes a client would.
/// </summary>
internal static class ControlHarness
{
    /// <summary>The token every endpoint under test is given.</summary>
    public const string Token = "0123456789abcdefghijklmnopqrstuv";

    /// <summary>A loopback peer.</summary>
    public static readonly EndPoint LoopbackPeer = new IPEndPoint(IPAddress.Loopback, 50000);

    /// <summary>An endpoint over <paramref name="target"/>.</summary>
    /// <param name="target">The target.</param>
    /// <returns>The endpoint.</returns>
    public static ControlEndpoint Endpoint(FakeControlTarget target) => new(target, new ControlToken(Token));

    /// <summary>Sends <paramref name="request"/> on one connection and returns everything written back.</summary>
    /// <param name="endpoint">The endpoint.</param>
    /// <param name="request">The request bytes, as text.</param>
    /// <param name="timeProvider">The clock.</param>
    /// <param name="cancellationToken">The exchange's cancellation.</param>
    /// <param name="peer">The peer, loopback when not given.</param>
    /// <returns>The response bytes, as text.</returns>
    public static async Task<string> ServeAsync(
        ControlEndpoint endpoint,
        string request,
        TimeProvider timeProvider,
        CancellationToken cancellationToken,
        EndPoint? peer = null)
    {
        var connection = new InMemoryConnection([Encoding.UTF8.GetBytes(request)], remoteEndPoint: peer ?? LoopbackPeer);
        await endpoint.ServeAsync(connection, Context(timeProvider, peer ?? LoopbackPeer, cancellationToken));

        return Encoding.UTF8.GetString(connection.WrittenBytes);
    }

    /// <summary>The context of a control exchange.</summary>
    /// <param name="timeProvider">The clock.</param>
    /// <param name="peer">The peer.</param>
    /// <param name="cancellationToken">The exchange's cancellation.</param>
    /// <returns>The context.</returns>
    public static ExchangeContext Context(TimeProvider timeProvider, EndPoint peer, CancellationToken cancellationToken) => new(
        1,
        new ListenUrl("http", "127.0.0.1", 0).WithBoundPort(18999),
        new IPEndPoint(IPAddress.Loopback, 18999),
        peer,
        new RecordingExchangeLog(),
        timeProvider,
        cancellationToken);

    /// <summary>A request with the token, closing the connection after it.</summary>
    /// <param name="method">The method.</param>
    /// <param name="target">The request target.</param>
    /// <param name="contentType">The <c>Content-Type</c>, or <see langword="null"/> for none.</param>
    /// <param name="body">The body, or <see langword="null"/> for none.</param>
    /// <returns>The request.</returns>
    public static string Request(string method, string target, string? contentType = null, string? body = null) =>
        RequestWith(method, target, $"Authorization: Bearer {Token}\r\n", contentType, body);

    /// <summary>A request with the given extra field lines, closing the connection after it.</summary>
    /// <param name="method">The method.</param>
    /// <param name="target">The request target.</param>
    /// <param name="fieldLines">Field lines, each ending in CRLF.</param>
    /// <param name="contentType">The <c>Content-Type</c>, or <see langword="null"/> for none.</param>
    /// <param name="body">The body, or <see langword="null"/> for none.</param>
    /// <returns>The request.</returns>
    public static string RequestWith(string method, string target, string fieldLines, string? contentType = null, string? body = null)
    {
        var head = new StringBuilder($"{method} {target} HTTP/1.1\r\nHost: 127.0.0.1\r\n{fieldLines}Connection: close\r\n");
        if (contentType is not null)
        {
            head.Append($"Content-Type: {contentType}\r\n");
        }

        if (body is not null)
        {
            head.Append($"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\n");
        }

        return $"{head}\r\n{body}";
    }

    /// <summary>The status line of a response.</summary>
    /// <param name="response">The response.</param>
    /// <returns>Such as <c>HTTP/1.1 200 OK</c>.</returns>
    public static string StatusLine(string response) => response[..response.IndexOf("\r\n", StringComparison.Ordinal)];

    /// <summary>The body of a response.</summary>
    /// <param name="response">The response.</param>
    /// <returns>The text after the head.</returns>
    public static string Body(string response) => response[(response.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4)..];
}
