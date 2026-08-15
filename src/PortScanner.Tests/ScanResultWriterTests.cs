// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ScanResultWriterTests.cs" company="Hämmer Electronics">
//   Copyright (c) All rights reserved.
// </copyright>
// <summary>
//   A class to test the <see cref="ScanResultWriter" /> class.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace PortScanner.Tests;

/// <summary>
/// A class to test the <see cref="ScanResultWriter"/> class.
/// </summary>
[TestClass]
public class ScanResultWriterTests
{
    /// <summary>
    /// The time stamp all tests write, an afternoon one so that a 12 hour clock would be visible.
    /// </summary>
    private static readonly DateTime Timestamp = new(2026, 8, 15, 13, 15, 42, DateTimeKind.Local);

    /// <summary>
    /// The writer under test.
    /// </summary>
    private readonly IScanResultWriter scanResultWriter = new ScanResultWriter();

    /// <summary>
    /// The directory the result files of a single test are written to.
    /// </summary>
    private string testDirectory = string.Empty;

    /// <summary>
    /// Creates an empty directory outside of the repository for the files of the running test.
    /// </summary>
    [TestInitialize]
    public void CreateTestDirectory()
    {
        this.testDirectory = Path.Combine(Path.GetTempPath(), $"PortScanner_{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.testDirectory);
    }

    /// <summary>
    /// Removes the directory of the finished test.
    /// </summary>
    [TestCleanup]
    public void DeleteTestDirectory()
    {
        if (Directory.Exists(this.testDirectory))
        {
            Directory.Delete(this.testDirectory, true);
        }
    }

    /// <summary>
    /// Checks whether the file name carries the date of the scan, so that all scans of one day share one file.
    /// </summary>
    [TestMethod]
    public void GetFileNameContainsTheDateOfTheTimestamp()
    {
        Assert.AreEqual("ScanResult_20260815.txt", this.scanResultWriter.GetFileName(Timestamp));
    }

    /// <summary>
    /// Checks whether the header and the open ports end up in the file in the documented order.
    /// </summary>
    [TestMethod]
    public void WriteResultWritesTheHeaderAndThePorts()
    {
        var filePath = this.GetTestFilePath();

        this.scanResultWriter.WriteResult(filePath, "127.0.0.1", Timestamp, GetTexts(), [80, 443]);

        var lines = File.ReadAllLines(filePath);
        Assert.AreEqual(6, lines.Length);
        Assert.AreEqual("Scan from 15.08.2026:13:15:42:", lines[0]);
        Assert.AreEqual("https://en.wikipedia.org/wiki/List_of_TCP_and_UDP_port_numbers", lines[1]);
        StringAssert.StartsWith(lines[2], "-----");
        Assert.AreEqual("List of open ports for the host 127.0.0.1:", lines[3]);
        Assert.AreEqual("80", lines[4]);
        Assert.AreEqual("443", lines[5]);
    }

    /// <summary>
    /// Checks whether the time stamp uses the 24 hour clock. This is the regression test for the <c>hh</c>
    /// format that stamped an afternoon scan as <c>01:15:42</c> until version 1.0.7.0.
    /// </summary>
    [TestMethod]
    public void WriteResultUsesThe24HourClock()
    {
        var filePath = this.GetTestFilePath();

        this.scanResultWriter.WriteResult(filePath, "127.0.0.1", Timestamp, GetTexts(), []);

        var lines = File.ReadAllLines(filePath);
        StringAssert.Contains(lines[0], "13:15:42");
    }

    /// <summary>
    /// Checks whether a scan without a single open port still writes its header, so that the file shows that
    /// the scan happened.
    /// </summary>
    [TestMethod]
    public void WriteResultWithoutOpenPortsWritesTheHeaderOnly()
    {
        var filePath = this.GetTestFilePath();

        this.scanResultWriter.WriteResult(filePath, "127.0.0.1", Timestamp, GetTexts(), []);

        Assert.AreEqual(4, File.ReadAllLines(filePath).Length);
    }

    /// <summary>
    /// Checks whether a second scan of the same day is appended instead of replacing the first one.
    /// </summary>
    [TestMethod]
    public void WriteResultAppendsToAnExistingFile()
    {
        var filePath = this.GetTestFilePath();

        this.scanResultWriter.WriteResult(filePath, "127.0.0.1", Timestamp, GetTexts(), [80]);
        this.scanResultWriter.WriteResult(filePath, "127.0.0.2", Timestamp, GetTexts(), [443]);

        var lines = File.ReadAllLines(filePath);
        Assert.AreEqual(10, lines.Length);
        Assert.AreEqual("List of open ports for the host 127.0.0.1:", lines[3]);
        Assert.AreEqual("80", lines[4]);
        Assert.AreEqual("List of open ports for the host 127.0.0.2:", lines[8]);
        Assert.AreEqual("443", lines[9]);
    }

    /// <summary>
    /// Gets the English header texts, the same ones the application reads out of <c>en-US.xml</c>.
    /// </summary>
    /// <returns>The <see cref="ScanResultTexts"/> to write.</returns>
    private static ScanResultTexts GetTexts()
    {
        return new ScanResultTexts(
            "Scan from ",
            "https://en.wikipedia.org/wiki/List_of_TCP_and_UDP_port_numbers",
            "List of open ports for the host ");
    }

    /// <summary>
    /// Gets the path of the result file inside the directory of the running test.
    /// </summary>
    /// <returns>The path of the result file.</returns>
    private string GetTestFilePath()
    {
        return Path.Combine(this.testDirectory, this.scanResultWriter.GetFileName(Timestamp));
    }
}
