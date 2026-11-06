using System.Net;
using Surl.HttpServing;

namespace Surl.Control;

/// <summary>
/// Answers the control requests of one connection (ADR-0124 decision 7): a peer that is not loopback is refused
/// <c>403</c>; a request without the token <c>401</c>, after <see cref="ControlEndpoint.RefusalDelay"/>; then the
/// request is matched to its route - <c>404</c> for an unknown path, <c>405</c> for a method the path does not take -
/// and its query keys are checked before it is answered.
/// </summary>
/// <param name="routes">Every request the endpoint answers.</param>
/// <param name="token">The control token.</param>
/// <param name="peer">The connection's remote end point.</param>
/// <param name="answers">Writes the answers.</param>
/// <param name="timeProvider">The clock the refusal delay waits on.</param>
internal sealed class ControlRequestHandler(
    IReadOnlyList<ControlRoute> routes,
    ControlToken token,
    EndPoint peer,
    ControlAnswers answers,
    TimeProvider timeProvider) : IHttpRequestHandler
{
    private const string PathPrefix = "/v1/";

    /// <inheritdoc/>
    public async Task<HttpAnswer> AnswerAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!IsLoopback(peer))
        {
            return answers.Error(request, ControlError.Forbidden());
        }

        if (!token.IsPresentedIn(request.Fields))
        {
            await Task.Delay(ControlEndpoint.RefusalDelay, timeProvider, cancellationToken);

            return answers.Error(request, ControlError.Unauthorized());
        }

        try
        {
            return await RouteAsync(request, cancellationToken);
        }
        catch (ControlErrorException failure)
        {
            return answers.Error(request, failure.Error);
        }
        catch (ControlRefusalException refusal)
        {
            return answers.Error(request, ControlError.Of(refusal));
        }
    }

    /// <summary>Whether <paramref name="endPoint"/> is a loopback address: <c>127.0.0.0/8</c> or <c>::1</c>.</summary>
    /// <param name="endPoint">A peer.</param>
    /// <returns><see langword="true"/> for a loopback IP end point, an IPv4-mapped one included.</returns>
    public static bool IsLoopback(EndPoint endPoint) =>
        endPoint is IPEndPoint { Address: var address }
        && IPAddress.IsLoopback(address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address);

    private Task<HttpAnswer> RouteAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var queryStart = request.Target.IndexOf('?', StringComparison.Ordinal);
        var path = queryStart < 0 ? request.Target : request.Target[..queryStart];
        var (route, parameters) = FindRoute(request.Method, path);
        var query = ControlQuery.Parse(queryStart < 0 ? string.Empty : request.Target[(queryStart + 1)..]);
        query.AllowOnly(route.QueryKeys);

        return route.Answer(new ControlRequest(request, parameters, query, cancellationToken));
    }

    // The route for the method and path: 404 when no route has the path, 405 when none with it takes the method.
    private (ControlRoute Route, IReadOnlyList<string> Parameters) FindRoute(string method, string path)
    {
        var matched = RoutesWithPath(path);
        if (matched.Length == 0)
        {
            throw UnknownPath(path);
        }

        var found = matched.FirstOrDefault(match => match.Route.Method == method);

        return found.Route is null ? throw MethodNotAllowed(path, matched.Select(match => match.Route.Method)) : (found.Route, found.Parameters!);
    }

    private (ControlRoute Route, IReadOnlyList<string>? Parameters)[] RoutesWithPath(string path)
    {
        if (!path.StartsWith(PathPrefix, StringComparison.Ordinal))
        {
            return [];
        }

        var segments = path[PathPrefix.Length..].Split('/');

        return [.. routes.Select(route => (Route: route, Parameters: route.Match(segments))).Where(match => match.Parameters is not null)];
    }

    private static ControlErrorException UnknownPath(string path) =>
        new(ControlError.NotFound($"no control request has path {path}"));

    private static ControlErrorException MethodNotAllowed(string path, IEnumerable<string> methods) =>
        new(ControlError.MethodNotAllowed($"{path} takes {string.Join(", ", methods)}"));
}
