// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ScanResultTexts.cs" company="Hämmer Electronics">
//   Copyright (c) All rights reserved.
// </copyright>
// <summary>
//   The translated texts that make up the header of a scan result file.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace PortScanner.Datatypes;

/// <summary>
/// The translated texts that make up the header of a scan result file. They are read from the language manager
/// on the user interface thread and handed to the writer, so that the writer needs no language manager itself.
/// </summary>
/// <param name="ScanFrom">The text in front of the time stamp of the scan.</param>
/// <param name="PortList">The link to the list of standardized ports.</param>
/// <param name="ListOfOpenPorts">The text in front of the scanned host.</param>
public sealed record ScanResultTexts(string ScanFrom, string PortList, string ListOfOpenPorts);
