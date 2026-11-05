using System.Net;

namespace Surl.Networking;

/// <summary>
/// An <see cref="IOriginSockets"/> that resolves and connects nothing: both calls run the test's script.
/// </summary>
internal sealed class FakeOriginSockets : IOriginSockets
{
    private readonly List<IPEndPoint> connectTargets = [];

    public Func<string, CancellationToken, Task<IPAddress[]>> Resolve { get; init; } =
        (_, _) => throw new InvalidOperationException("The test scripted no resolution.");

    public Func<IPEndPoint, CancellationToken, Task<DataTransport>> Connect { get; init; } =
        (_, _) => throw new InvalidOperationException("The test scripted no connect.");

    public IReadOnlyList<IPEndPoint> ConnectTargets => connectTargets;

    public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken) =>
        Resolve(host, cancellationToken);

    public Task<DataTransport> ConnectAsync(IPEndPoint target, CancellationToken cancellationToken)
    {
        connectTargets.Add(target);

        return Connect(target, cancellationToken);
    }
}
