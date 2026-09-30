using System.Net;

namespace Surl.Networking;

/// <summary>
/// An <see cref="IDataConnectionSockets"/> that binds and connects nothing: <see cref="Listen"/>
/// hands out a <see cref="FakePassiveDataSocket"/>, and <see cref="ConnectAsync"/> runs the
/// test's script.
/// </summary>
internal sealed class FakeDataConnectionSockets : IDataConnectionSockets
{
    private readonly List<IPAddress> listenedAddresses = [];
    private readonly List<IPEndPoint> connectTargets = [];

    public Exception? ListenFailure { get; init; }

    public bool AcceptsIgnoreCancellation { get; init; }

    public Func<IPEndPoint, CancellationToken, Task<DataTransport>> Connect { get; init; } =
        (_, _) => throw new InvalidOperationException("The test scripted no connect.");

    public IReadOnlyList<IPAddress> ListenedAddresses => listenedAddresses;

    public IReadOnlyList<IPEndPoint> ConnectTargets => connectTargets;

    public FakePassiveDataSocket? LastSocket { get; private set; }

    public IPassiveDataSocket Listen(IPAddress address)
    {
        listenedAddresses.Add(address);

        if (ListenFailure is not null)
        {
            throw ListenFailure;
        }

        LastSocket = new FakePassiveDataSocket(new IPEndPoint(address, 50100), AcceptsIgnoreCancellation);

        return LastSocket;
    }

    public Task<DataTransport> ConnectAsync(IPEndPoint target, CancellationToken cancellationToken)
    {
        connectTargets.Add(target);

        return Connect(target, cancellationToken);
    }
}
