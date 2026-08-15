// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PortScanService.cs" company="Hämmer Electronics">
//   Copyright (c) All rights reserved.
// </copyright>
// <summary>
//   A service to resolve a host and to check its TCP ports.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace PortScanner.Services;

/// <inheritdoc cref="IPortScanService"/>
/// <summary>
/// A service to resolve a host and to check its TCP ports.
/// </summary>
/// <seealso cref="IPortScanService"/>
public sealed class PortScanService : IPortScanService
{
    /// <summary>
    /// The highest port number a TCP port can have.
    /// </summary>
    private const int HighestPossiblePort = 65535;

    /// <inheritdoc cref="IPortScanService.ResolveHost(string)"/>
    /// <summary>
    /// Resolves a host name or an IP address to a single <see cref="IPAddress"/>. An IPv4 address is preferred
    /// over an IPv6 address, because a host that answers on both is more likely to be reachable over IPv4.
    /// </summary>
    /// <param name="host">The host name or IP address to resolve.</param>
    /// <returns>The resolved <see cref="IPAddress"/>.</returns>
    /// <exception cref="ArgumentException">Thrown if the host is null, empty or white space.</exception>
    /// <exception cref="SocketException">Thrown if the host cannot be resolved.</exception>
    /// <seealso cref="IPortScanService"/>
    public IPAddress ResolveHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException("The host must not be empty.", nameof(host));
        }

        var addresses = Dns.GetHostAddresses(host.Trim());

        if (addresses.Length == 0)
        {
            throw new SocketException((int)SocketError.HostNotFound);
        }

        return addresses.FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork) ?? addresses[0];
    }

    /// <inheritdoc cref="IPortScanService.IsPortOpen(IPAddress, int, TimeSpan)"/>
    /// <summary>
    /// Checks whether a TCP connection to the given port can be established within the given timeout.
    /// </summary>
    /// <param name="address">The address to connect to.</param>
    /// <param name="port">The port to connect to.</param>
    /// <param name="timeout">The time to wait for the connection.</param>
    /// <returns><c>true</c> if the port accepted the connection, <c>false</c> in every other case.</returns>
    /// <seealso cref="IPortScanService"/>
    public bool IsPortOpen(IPAddress address, int port, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(address);

        using var client = new TcpClient(address.AddressFamily);

        try
        {
            var connect = client.ConnectAsync(address, port);

            if (!connect.Wait(timeout))
            {
                // The client is disposed right after this, which makes the still pending connect fault.
                // The continuation observes that exception so that it does not surface as an unobserved one.
                _ = connect.ContinueWith(static task => task.Exception, TaskScheduler.Default);
                return false;
            }

            return client.Connected;
        }
        catch (AggregateException)
        {
            // The connect was refused or the host is unreachable, which is the normal case for a closed port.
            return false;
        }
        catch (SocketException)
        {
            // The same, but thrown by the connect call itself instead of by the waited task.
            return false;
        }
    }

    /// <inheritdoc cref="IPortScanService.ScanPorts(IPAddress, ScanSettings, Action{int}, CancellationToken)"/>
    /// <summary>
    /// Scans the port range of the given settings and returns the open ports in ascending order.
    /// </summary>
    /// <param name="address">The address to scan.</param>
    /// <param name="settings">The <see cref="ScanSettings"/> to use.</param>
    /// <param name="reportProgress">An optional callback that receives the progress in percent.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that stops the scan.</param>
    /// <returns>The open ports in ascending order.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the port range or the parallelism of the
    /// settings is invalid.</exception>
    /// <seealso cref="IPortScanService"/>
    public IReadOnlyList<int> ScanPorts(IPAddress address, ScanSettings settings, Action<int>? reportProgress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(settings);
        CheckSettings(settings);

        var openPorts = new ConcurrentBag<int>();
        var scannedPorts = 0;
        var reportedPercentage = 0;

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = settings.MaxParallelScans,
            CancellationToken = cancellationToken
        };

        try
        {
            Parallel.For(settings.FirstPort, settings.LastPort + 1, options, port =>
            {
                if (this.IsPortOpen(address, port, settings.Timeout))
                {
                    openPorts.Add(port);
                }

                var scanned = Interlocked.Increment(ref scannedPorts);
                ReportProgress(reportProgress, ref reportedPercentage, scanned * 100 / settings.PortCount);
            });
        }
        catch (OperationCanceledException)
        {
            // The caller asked to stop, the ports found until here are still worth returning.
        }

        var result = openPorts.ToList();
        result.Sort();
        return result;
    }

    /// <summary>
    /// Reports a new percentage, but only when it is higher than the one reported before. Without that check
    /// the callback would fire once per port and the progress bar would also jump backwards, because the
    /// parallel scan finishes the ports out of order.
    /// </summary>
    /// <param name="reportProgress">The callback to call.</param>
    /// <param name="reportedPercentage">The percentage reported so far, updated in place.</param>
    /// <param name="percentage">The percentage to report.</param>
    private static void ReportProgress(Action<int>? reportProgress, ref int reportedPercentage, int percentage)
    {
        if (reportProgress is null)
        {
            return;
        }

        var previous = Volatile.Read(ref reportedPercentage);

        if (percentage > previous && Interlocked.CompareExchange(ref reportedPercentage, percentage, previous) == previous)
        {
            reportProgress(percentage);
        }
    }

    /// <summary>
    /// Checks the given settings and throws if the scan could not run with them.
    /// </summary>
    /// <param name="settings">The <see cref="ScanSettings"/> to check.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the port range or the parallelism is invalid.</exception>
    private static void CheckSettings(ScanSettings settings)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(settings.FirstPort, 1, nameof(settings));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(settings.LastPort, HighestPossiblePort, nameof(settings));
        ArgumentOutOfRangeException.ThrowIfLessThan(settings.LastPort, settings.FirstPort, nameof(settings));
        ArgumentOutOfRangeException.ThrowIfLessThan(settings.MaxParallelScans, 1, nameof(settings));
    }
}
