using Surl.HttpServing;

namespace Surl.Control;

/// <summary>A control request matched to its route.</summary>
/// <param name="Http">The HTTP request.</param>
/// <param name="Parameters">The decoded path segments the route's <c>{}</c> and <c>{*}</c> captured, in order.</param>
/// <param name="Query">The request's query.</param>
/// <param name="CancellationToken">The exchange's cancellation.</param>
internal sealed record ControlRequest(
    HttpRequest Http,
    IReadOnlyList<string> Parameters,
    ControlQuery Query,
    CancellationToken CancellationToken);
