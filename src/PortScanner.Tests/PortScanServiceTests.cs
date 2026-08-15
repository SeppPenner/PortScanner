// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PortScanServiceTests.cs" company="Hämmer Electronics">
//   Copyright (c) All rights reserved.
// </copyright>
// <summary>
//   A class to test the <see cref="PortScanService" /> class.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace PortScanner.Tests;

/// <summary>
/// A class to test the <see cref="PortScanService"/> class. Every test that needs an open port opens one on the
/// loopback interface itself, so the tests need no network and no host outside of this machine.
/// </summary>
[TestClass]
public class PortScanServiceTests
{
    /// <summary>
    /// The timeout of a single connect attempt. A listening port on the loopback interface answers within
    /// microseconds, so this is only the price of the few closed ports the tests walk over. It is kept short
    /// on purpose, because a local firewall may drop the packets of a closed port instead of refusing the
    /// connection, and then every closed port costs the full timeout.
    /// </summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// The service under test.
    /// </summary>
    private readonly IPortScanService portScanService = new PortScanService();

    /// <summary>
    /// Checks whether a literal IP address is passed through instead of being sent to a name server.
    /// </summary>
    [TestMethod]
    public void ResolveHostReturnsTheAddressOfALiteralIpAddress()
    {
        var address = this.portScanService.ResolveHost("127.0.0.1");

        Assert.AreEqual(IPAddress.Loopback, address);
    }

    /// <summary>
    /// Checks whether the surrounding white space of a host is ignored.
    /// </summary>
    [TestMethod]
    public void ResolveHostIgnoresSurroundingWhiteSpace()
    {
        var address = this.portScanService.ResolveHost("  127.0.0.1  ");

        Assert.AreEqual(IPAddress.Loopback, address);
    }

    /// <summary>
    /// Checks whether the IPv4 address is preferred for a host that answers with both address families.
    /// </summary>
    [TestMethod]
    public void ResolveHostPrefersTheIpv4AddressOfLocalhost()
    {
        var address = this.portScanService.ResolveHost("localhost");

        Assert.AreEqual(AddressFamily.InterNetwork, address.AddressFamily);
    }

    /// <summary>
    /// Checks whether an empty host is reported instead of being sent to the name resolution.
    /// </summary>
    [TestMethod]
    public void ResolveHostWithAnEmptyHostThrowsAnArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() => this.portScanService.ResolveHost("   "));
    }

    /// <summary>
    /// Checks whether a port that somebody listens on is reported as open.
    /// </summary>
    [TestMethod]
    public void IsPortOpenReturnsTrueForAListeningPort()
    {
        var listener = StartListener(out var port);

        try
        {
            Assert.IsTrue(this.portScanService.IsPortOpen(IPAddress.Loopback, port, Timeout));
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>
    /// Checks whether a port that nobody listens on is reported as closed.
    /// </summary>
    [TestMethod]
    public void IsPortOpenReturnsFalseForAClosedPort()
    {
        var listener = StartListener(out var port);
        listener.Stop();

        Assert.IsFalse(this.portScanService.IsPortOpen(IPAddress.Loopback, port, Timeout));
    }

    /// <summary>
    /// Checks whether the listening port shows up in the result of a scan over a small range.
    /// </summary>
    [TestMethod]
    public void ScanPortsFindsTheListeningPort()
    {
        var listener = StartListener(out var port);

        try
        {
            var settings = GetSettings(port, port);

            var openPorts = this.portScanService.ScanPorts(IPAddress.Loopback, settings, null, CancellationToken.None);

            CollectionAssert.AreEqual(new[] { port }, openPorts.ToArray());
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>
    /// Checks whether the returned ports are sorted although the scan itself runs in parallel and therefore
    /// finishes the ports out of order.
    /// </summary>
    [TestMethod]
    public void ScanPortsReturnsTheOpenPortsInAscendingOrder()
    {
        var (firstListener, secondListener, firstPort) = StartNeighbouringListeners();

        try
        {
            var settings = GetSettings(firstPort, firstPort + 1);

            var openPorts = this.portScanService.ScanPorts(IPAddress.Loopback, settings, null, CancellationToken.None);

            CollectionAssert.AreEqual(new[] { firstPort, firstPort + 1 }, openPorts.ToArray());
        }
        finally
        {
            firstListener.Stop();
            secondListener.Stop();
        }
    }

    /// <summary>
    /// Checks whether the progress grows to exactly 100 percent and never jumps backwards. This is the
    /// regression test for the integer division that kept the progress bar at zero until version 1.0.7.0.
    /// </summary>
    [TestMethod]
    public void ScanPortsReportsAGrowingProgressUpTo100Percent()
    {
        // One scan at a time, so that the reported values arrive in a deterministic order. The percentage
        // itself is calculated the same way for a parallel scan, only the callbacks would interleave.
        var settings = GetSettings(1, 5) with { MaxParallelScans = 1 };
        var percentages = new List<int>();

        this.portScanService.ScanPorts(IPAddress.Loopback, settings, percentages.Add, CancellationToken.None);

        Assert.AreEqual(5, settings.PortCount);
        CollectionAssert.AreEqual(new[] { 20, 40, 60, 80, 100 }, percentages);
    }

    /// <summary>
    /// Checks whether a cancelled scan returns instead of throwing an <see cref="OperationCanceledException"/>
    /// at the caller.
    /// </summary>
    [TestMethod]
    public void ScanPortsWithACancelledTokenReturnsWithoutThrowing()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var openPorts = this.portScanService.ScanPorts(IPAddress.Loopback, new ScanSettings(), null, cancellation.Token);

        Assert.AreEqual(0, openPorts.Count);
    }

    /// <summary>
    /// Checks whether port 0, which is not a valid TCP port, is rejected.
    /// </summary>
    [TestMethod]
    public void ScanPortsWithAFirstPortOfZeroThrowsAnArgumentOutOfRangeException()
    {
        var settings = new ScanSettings { FirstPort = 0, LastPort = 10 };

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => this.portScanService.ScanPorts(IPAddress.Loopback, settings, null, CancellationToken.None));
    }

    /// <summary>
    /// Checks whether a range that ends before it starts is rejected.
    /// </summary>
    [TestMethod]
    public void ScanPortsWithAnInvertedRangeThrowsAnArgumentOutOfRangeException()
    {
        var settings = new ScanSettings { FirstPort = 100, LastPort = 99 };

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => this.portScanService.ScanPorts(IPAddress.Loopback, settings, null, CancellationToken.None));
    }

    /// <summary>
    /// Checks whether the default settings cover the whole valid port range and leave out port 0.
    /// </summary>
    [TestMethod]
    public void TheDefaultSettingsCoverTheWholeValidPortRange()
    {
        var settings = new ScanSettings();

        Assert.AreEqual(1, settings.FirstPort);
        Assert.AreEqual(65535, settings.LastPort);
        Assert.AreEqual(65535, settings.PortCount);
    }

    /// <summary>
    /// Gets the settings for a scan over the given range with the timeout and the parallelism of the tests.
    /// </summary>
    /// <param name="firstPort">The first port to scan.</param>
    /// <param name="lastPort">The last port to scan.</param>
    /// <returns>The <see cref="ScanSettings"/> to use.</returns>
    private static ScanSettings GetSettings(int firstPort, int lastPort)
    {
        return new ScanSettings
        {
            FirstPort = firstPort,
            LastPort = lastPort,
            Timeout = Timeout,
            MaxParallelScans = 64
        };
    }

    /// <summary>
    /// Starts a listener on a free port of the loopback interface. Port 0 tells the operating system to pick
    /// the port, which is what keeps the tests free of a hard coded port that might be taken.
    /// </summary>
    /// <param name="port">The port the listener ended up on.</param>
    /// <returns>The started <see cref="TcpListener"/>, the caller has to stop it.</returns>
    private static TcpListener StartListener(out int port)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        port = ((IPEndPoint)listener.LocalEndpoint).Port;
        return listener;
    }

    /// <summary>
    /// Starts two listeners on two neighbouring ports, so that a scan over exactly those two ports has to
    /// return both of them. The second port cannot be requested directly, it has to be tried, which is what
    /// the retries are for.
    /// </summary>
    /// <returns>The two started listeners and the port of the first one. The caller has to stop both.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no free pair of ports was found.</exception>
    private static (TcpListener FirstListener, TcpListener SecondListener, int FirstPort) StartNeighbouringListeners()
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var firstListener = StartListener(out var firstPort);

            if (firstPort < 65535)
            {
                try
                {
                    var secondListener = new TcpListener(IPAddress.Loopback, firstPort + 1);
                    secondListener.Start();
                    return (firstListener, secondListener, firstPort);
                }
                catch (SocketException)
                {
                    // The port right above the one the operating system handed out is taken, try again.
                }
            }

            firstListener.Stop();
        }

        throw new InvalidOperationException("No two neighbouring free ports were found on the loopback interface.");
    }
}
