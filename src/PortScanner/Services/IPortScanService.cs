// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IPortScanService.cs" company="Hämmer Electronics">
//   Copyright (c) All rights reserved.
// </copyright>
// <summary>
//   A service to resolve a host and to check its TCP ports.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace PortScanner.Services;

/// <summary>
/// A service to resolve a host and to check its TCP ports.
/// </summary>
public interface IPortScanService
{
    /// <summary>
    /// Resolves a host name or an IP address to a single <see cref="IPAddress"/>. An IPv4 address is preferred
    /// over an IPv6 address, because a host that answers on both is more likely to be reachable over IPv4.
    /// </summary>
    /// <param name="host">The host name or IP address to resolve.</param>
    /// <returns>The resolved <see cref="IPAddress"/>.</returns>
    /// <exception cref="ArgumentException">Thrown if the host is null, empty or white space.</exception>
    /// <exception cref="SocketException">Thrown if the host cannot be resolved.</exception>
    IPAddress ResolveHost(string host);

    /// <summary>
    /// Checks whether a TCP connection to the given port can be established within the given timeout.
    /// </summary>
    /// <param name="address">The address to connect to.</param>
    /// <param name="port">The port to connect to.</param>
    /// <param name="timeout">The time to wait for the connection.</param>
    /// <returns><c>true</c> if the port accepted the connection, <c>false</c> in every other case.</returns>
    bool IsPortOpen(IPAddress address, int port, TimeSpan timeout);

    /// <summary>
    /// Scans the port range of the given settings and returns the open ports in ascending order.
    /// </summary>
    /// <param name="address">The address to scan.</param>
    /// <param name="settings">The <see cref="ScanSettings"/> to use.</param>
    /// <param name="reportProgress">An optional callback that receives the progress in percent. It is only
    /// called when the percentage actually changes, not once per port.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that stops the scan. A cancelled
    /// scan returns the ports found so far instead of throwing.</param>
    /// <returns>The open ports in ascending order.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the port range or the parallelism of the
    /// settings is invalid.</exception>
    IReadOnlyList<int> ScanPorts(IPAddress address, ScanSettings settings, Action<int>? reportProgress, CancellationToken cancellationToken);
}
