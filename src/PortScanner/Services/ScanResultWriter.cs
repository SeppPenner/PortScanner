// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ScanResultWriter.cs" company="Hämmer Electronics">
//   Copyright (c) All rights reserved.
// </copyright>
// <summary>
//   A service to write the result of a port scan to a text file.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace PortScanner.Services;

/// <inheritdoc cref="IScanResultWriter"/>
/// <summary>
/// A service to write the result of a port scan to a text file.
/// </summary>
/// <seealso cref="IScanResultWriter"/>
public sealed class ScanResultWriter : IScanResultWriter
{
    /// <summary>
    /// The separator line between the header and the ports.
    /// </summary>
    private const string Separator = "-------------------------------------------------------------------------------------";

    /// <inheritdoc cref="IScanResultWriter.GetFileName(DateTime)"/>
    /// <summary>
    /// Gets the name of the result file for the given time stamp. All scans of one day share one file.
    /// </summary>
    /// <param name="timestamp">The time stamp of the scan.</param>
    /// <returns>The file name, for example <c>ScanResult_20260815.txt</c>.</returns>
    /// <seealso cref="IScanResultWriter"/>
    public string GetFileName(DateTime timestamp)
    {
        return "ScanResult_" + timestamp.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".txt";
    }

    /// <inheritdoc cref="IScanResultWriter.WriteResult(string, string, DateTime, ScanResultTexts, IEnumerable{int})"/>
    /// <summary>
    /// Appends the header and the open ports of one scan to the given file.
    /// </summary>
    /// <param name="filePath">The path of the file to write to.</param>
    /// <param name="host">The scanned host as the user typed it.</param>
    /// <param name="timestamp">The time stamp of the scan.</param>
    /// <param name="texts">The translated <see cref="ScanResultTexts"/> of the header.</param>
    /// <param name="openPorts">The open ports to write.</param>
    /// <seealso cref="IScanResultWriter"/>
    public void WriteResult(string filePath, string host, DateTime timestamp, ScanResultTexts texts, IEnumerable<int> openPorts)
    {
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(openPorts);

        using var writer = File.AppendText(filePath);
        writer.WriteLine(texts.ScanFrom + timestamp.ToString("dd.MM.yyyy:HH:mm:ss", CultureInfo.InvariantCulture) + ":");
        writer.WriteLine(texts.PortList);
        writer.WriteLine(Separator);
        writer.WriteLine(texts.ListOfOpenPorts + host + ":");

        foreach (var port in openPorts)
        {
            writer.WriteLine(port.ToString(CultureInfo.InvariantCulture));
        }
    }
}
