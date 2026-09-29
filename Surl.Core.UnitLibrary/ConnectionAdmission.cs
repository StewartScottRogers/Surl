using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// Counts the connections the serving engine holds, in total and per remote IP address, and
/// admits a new one only while both counts are under their limits (ADR-0006, sections 1 and 5).
/// </summary>
/// <param name="maxConnections">The most connections at once; 0 is no limit.</param>
/// <param name="maxConnectionsPerAddress">The most connections at once from one address; 0 is no limit.</param>
internal sealed class ConnectionAdmission(int maxConnections, int maxConnectionsPerAddress)
{
    private readonly Lock gate = new();
    private readonly Dictionary<IPAddress, int> countsByAddress = [];
    private int count;

    /// <summary>
    /// Admits a connection from <paramref name="remoteEndPoint"/> and counts it, or refuses it
    /// and counts nothing. An endpoint with no IP address counts only against the total.
    /// </summary>
    /// <param name="remoteEndPoint">The connection's remote endpoint.</param>
    /// <returns>
    /// <see langword="null"/> when admitted, to be matched by one <see cref="Release"/>;
    /// otherwise the limit the connection is past, the total checked first.
    /// </returns>
    public ConnectionRefusal? TryAdmit(EndPoint remoteEndPoint)
    {
        var address = AddressOf(remoteEndPoint);

        lock (gate)
        {
            var refusal = FindLimitPassed(address);

            if (refusal is null)
            {
                Count(address);
            }

            return refusal;
        }
    }

    /// <summary>
    /// Stops counting a connection <see cref="TryAdmit"/> admitted.
    /// </summary>
    /// <param name="remoteEndPoint">The remote endpoint it was admitted with.</param>
    public void Release(EndPoint remoteEndPoint)
    {
        var address = AddressOf(remoteEndPoint);

        lock (gate)
        {
            count--;

            if (address is not null && --countsByAddress[address] == 0)
            {
                countsByAddress.Remove(address);
            }
        }
    }

    // Called under the gate.
    private ConnectionRefusal? FindLimitPassed(IPAddress? address) =>
        maxConnections > 0 && count >= maxConnections ? ConnectionRefusal.TooManyConnections
        : maxConnectionsPerAddress > 0 && CountFrom(address) >= maxConnectionsPerAddress ? ConnectionRefusal.TooManyConnectionsFromAddress
        : null;

    // Called under the gate.
    private void Count(IPAddress? address)
    {
        count++;

        if (address is not null)
        {
            countsByAddress[address] = CountFrom(address) + 1;
        }
    }

    private int CountFrom(IPAddress? address) => address is null ? 0 : countsByAddress.GetValueOrDefault(address);

    private static IPAddress? AddressOf(EndPoint remoteEndPoint) =>
        remoteEndPoint is IPEndPoint { Address: var address }
            ? address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address
            : null;
}
