// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ScanSettings.cs" company="Hämmer Electronics">
//   Copyright (c) All rights reserved.
// </copyright>
// <summary>
//   The settings of one port scan.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace PortScanner.Datatypes;

/// <summary>
/// The settings of one port scan.
/// </summary>
public sealed record ScanSettings
{
    /// <summary>
    /// Gets the first port that is scanned. Port 0 is not a valid TCP port, so the scan starts at 1.
    /// </summary>
    public int FirstPort { get; init; } = 1;

    /// <summary>
    /// Gets the last port that is scanned.
    /// </summary>
    public int LastPort { get; init; } = 65535;

    /// <summary>
    /// Gets the time to wait for one connect attempt. A closed port that drops the packets instead of refusing
    /// the connection costs the full timeout, which is why this is far below the operating system default.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Gets the number of ports that are probed at the same time. Scanning the whole range one port after
    /// another would take hours, this is what keeps a full run in the range of minutes.
    /// </summary>
    public int MaxParallelScans { get; init; } = 128;

    /// <summary>
    /// Gets the number of ports covered by these settings.
    /// </summary>
    public int PortCount => this.LastPort - this.FirstPort + 1;
}
