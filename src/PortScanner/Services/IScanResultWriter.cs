// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IScanResultWriter.cs" company="Hämmer Electronics">
//   Copyright (c) All rights reserved.
// </copyright>
// <summary>
//   A service to write the result of a port scan to a text file.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace PortScanner.Services;

/// <summary>
/// A service to write the result of a port scan to a text file.
/// </summary>
public interface IScanResultWriter
{
    /// <summary>
    /// Gets the name of the result file for the given time stamp. All scans of one day share one file.
    /// </summary>
    /// <param name="timestamp">The time stamp of the scan.</param>
    /// <returns>The file name, for example <c>ScanResult_20260815.txt</c>.</returns>
    string GetFileName(DateTime timestamp);

    /// <summary>
    /// Appends the header and the open ports of one scan to the given file. The file is created if it does
    /// not exist yet, an existing file is appended to, so a second scan on the same day does not overwrite
    /// the first one.
    /// </summary>
    /// <param name="filePath">The path of the file to write to.</param>
    /// <param name="host">The scanned host as the user typed it.</param>
    /// <param name="timestamp">The time stamp of the scan.</param>
    /// <param name="texts">The translated <see cref="ScanResultTexts"/> of the header.</param>
    /// <param name="openPorts">The open ports to write.</param>
    void WriteResult(string filePath, string host, DateTime timestamp, ScanResultTexts texts, IEnumerable<int> openPorts);
}
