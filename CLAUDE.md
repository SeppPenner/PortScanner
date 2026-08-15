# Project rules for Claude

## What this is

PortScanner is a small Windows Forms application that scans one host for open TCP ports. The user
types a host name or an IP address, presses the start button, and the application walks the port
range from 1 to 65535 and appends every port it could connect to into a text file
`ScanResult_yyyyMMdd.txt`. That file is written into the current working directory of the process,
not next to the executable, and all scans of one day end up in the same file. The user interface is
bilingual (German and English) and switches at runtime through a combo box.

One solution `src/PortScanner.sln` with exactly two projects:

- `src/PortScanner/PortScanner.csproj`, `OutputType` `WinExe`, `UseWindowsForms`,
  `ApplicationIcon` `Radar.ico`, `RuntimeIdentifiers` `win-x64`.
- `src/PortScanner.Tests/PortScanner.Tests.csproj`, MSTest, added in version 1.0.8.0. It targets
  `net10.0-windows` and sets `UseWindowsForms` as well, because it references the Windows Forms
  executable above.

Layout inside `src/PortScanner`:

- `Program.cs`: the `Main` method, nothing but `Application.Run(new Main())`.
- `Main.cs`: the form. The constructor calls `InitializeComponent`, `InitializeCaption`,
  `InitializeBackgroundWorker`, `InitializeLanguageManager` and `LoadLanguagesToCombo` in exactly
  that order, see the quirk about the start order below. `ScannerDoWork` only collects the host and
  the translated header texts from the user interface thread and then hands the actual work to the
  two services, keep it that way, the form is not the place for scan logic.
- `Main.Designer.cs` plus `Main.resx`: the designer generated form. A `TableLayoutPanel` with the
  host label, the host text box, the language combo box, the start button and the progress bar.
  Do not hand edit these two, they belong to the Windows Forms designer.
- `Services/PortScanService.cs` plus `Services/IPortScanService.cs`: `ResolveHost` turns the typed
  host into an `IPAddress`, `IsPortOpen` probes a single port, `ScanPorts` runs the whole range in
  parallel and returns the open ports sorted. The service knows nothing about Windows Forms, which
  is what makes it testable.
- `Services/ScanResultWriter.cs` plus `Services/IScanResultWriter.cs`: the result file, its name
  and its content.
- `Datatypes/ScanSettings.cs`: port range, connect timeout and parallelism of one scan.
  `Datatypes/ScanResultTexts.cs`: the three translated strings of the file header, read on the user
  interface thread so that the writer needs no language manager.
- `GUIExtensions.cs`: one extension method `UiThreadInvoke` that marshals an `Action` to the UI
  thread. Note the file name, see the quirks.
- `GlobalUsings.cs`: all usings of the project.
- `languages/de-DE.xml` and `languages/en-US.xml`: the translations, copied to the output directory
  with `CopyToOutputDirectory=Always`.
- `License.txt` and `Radar.ico`: shipped with the application, the license also ends up in the
  installer.

Layout inside `src/PortScanner.Tests`:

- `PortScanServiceTests.cs`: host resolution, a single open and a single closed port, the sorted
  result, the progress percentages, a cancelled scan and the rejected port ranges. Every open port
  a test needs is opened by the test itself with a `TcpListener` on the loopback interface, so no
  test depends on a host outside of this machine.
- `ScanResultWriterTests.cs`: the file name, the header, the 24 hour clock, a scan without a single
  open port and the append of a second scan.
- `GlobalUsings.cs`: all usings of the test project.

Repository root: `README.md` (the only user documentation, spelled with capital letters unlike the
sibling repositories), `Changelog.md`, `License.txt` (MIT), `Screenshot_DE.PNG`,
`Screenshot_EN.PNG`, `.gitattributes` and `.gitignore`. `Setup/` holds the Inno Setup script, the
publish batch file and the built installer. There is no `.github` folder, no `Updating.md` and no
`HowToUse.md`.

## Build

```powershell
dotnet build src/PortScanner.sln -c Release
```

```powershell
dotnet test src/PortScanner.sln -c Release
```

- Single target framework `net10.0-windows` in both projects, no multi-targeting.
  `RuntimeIdentifiers` is `win-x64`.
- All build properties live directly in the two `.csproj` files and are duplicated there. There is
  **no** `Directory.Build.props` in this repository.
- `TreatWarningsAsErrors` is enabled in both projects, so every warning breaks the build, NuGet
  warnings (`NU****`) from restore included. A clean build reports zero warnings, keep it that way.
- `NU1803` (HTTP source usage during restore) is the one warning suppressed via `NoWarn`. Fix
  warnings instead of extending that list. `NuGetAudit` and `NuGetAuditMode=all` are on, so a
  vulnerable transitive package fails the build too.
- Versions come from GitVersion.MsBuild out of the git tags, for example `1.0.9-1` for the first
  commit after tag `1.0.8`. Never edit a version property or an assembly version by hand.
- Restore needs nuget.org. Several private feeds are configured globally on this machine
  (DevExpress, Telerik, a GitHub package feed). If one of them answers 404 for public packages,
  restore fails with `NU1301`. Then build with an explicit source:
  `dotnet build src/PortScanner.sln --source https://api.nuget.org/v3/index.json`.
- Tests are MSTest, in the single test project `src/PortScanner.Tests`, which follows the same
  package set as the sibling repositories: `Microsoft.NET.Test.Sdk`, `MSTest.TestAdapter`,
  `MSTest.TestFramework`, `coverlet.collector` and `GitVersion.MsBuild`. `dotnet test` runs 18
  tests in a few seconds. They need no network: every port a test needs is opened on the loopback
  interface by the test itself, and every file a test writes goes into its own directory below
  `Path.GetTempPath()` which the test deletes afterwards. A test run leaves the working tree
  untouched. Never claim a test run happened without running it.
- Beyond the tests, a behaviour change of the form itself is verified by publishing the
  application, starting it and scanning `127.0.0.1`, then checking the written
  `ScanResult_yyyyMMdd.txt`. The form has no automated coverage, only the services have.

## Code conventions

Follow the surrounding code, it is consistent in every hand written file:

- File header comment block with `<copyright file="..." company="Hämmer Electronics">` and a
  `<summary>`, then the file-scoped namespace. The designer generated files have no such header.
- XML doc comments on every type and every member, private members included, no exceptions.
  Implementations of an interface member additionally carry `<inheritdoc cref="..."/>` and
  `<seealso cref="..."/>` pointing at that interface.
- `Nullable`, `ImplicitUsings` and `LangVersion latest` are enabled.
- New `using` directives go into the `GlobalUsings.cs` of the respective project, inside the
  existing `#pragma warning disable IDE0065` block, never at the top of a file. The editorconfig
  requires usings inside the namespace (`csharp_using_directive_placement=inside_namespace:warning`),
  which global usings cannot satisfy, that is what the pragma is for. Do not add other pragmas. The
  comment text in that block is German because Visual Studio generated it, leave it alone.
- Fields, properties, methods and events are always accessed with `this.` qualification
  (`dotnet_style_qualification_for_*` at severity `warning`).
- `src/.editorconfig` also enforces braces everywhere, no multiple blank lines, four spaces, CRLF,
  UTF-8, file scoped namespaces, `System` usings sorted first and `IDE0005` as warning. Analyzer
  warnings are fixed, not silenced.
- The language files are UTF-8 **without** BOM, use CRLF and are indented with tabs, except the
  `<Identifier>` line which uses four spaces. Change them with a script that preserves those bytes,
  an editor that "cleans up" the file produces a large and pointless diff.

## Known quirks

Do not silently "clean up" these, they are existing behaviour:

- **The scan defaults are a compromise, not an accident.** `ScanSettings` uses a connect timeout of
  500 milliseconds and 128 parallel probes. A closed port that answers with a reset is done
  immediately, but a port whose packets a firewall drops costs the full timeout, and on a machine
  that drops instead of refusing that is the normal case. 65535 ports at 128 in parallel therefore
  take roughly four minutes. Lowering the timeout loses slow hosts, raising the parallelism runs
  into the connection limits of the local machine.
- **Progress is reported only when the percentage changes.** `ScanPorts` counts the finished ports
  with `Interlocked.Increment` and reports through a `CompareExchange`, so the callback fires at
  most 100 times and never with a value below one already reported. Calling it once per port would
  flood the message queue of the form, and the parallel scan finishes the ports out of order, so
  without the comparison the bar would also jump backwards.
- **The cancellation does not use `BackgroundWorker.CancelAsync`.** The form owns a
  `CancellationTokenSource`, passes its token as the argument of `RunWorkerAsync` and cancels it
  from the same button that started the scan. `Parallel.For` needs a `CancellationToken`, and the
  scan service must not know about Windows Forms, so `CancellationPending` would have to be
  bridged into a token anyway. `DoWork` sets `e.Cancel` from the token, which keeps
  `RunWorkerCompletedEventArgs.Cancelled` correct.
- **A cancelled scan still writes its result file** with the ports found until the cancellation.
  `ScanPorts` swallows the `OperationCanceledException` of `Parallel.For` and returns what it has.
- **The start button is also the cancel button.** Its text switches between the language keys
  `StartPortscan` and `CancelPortscan`, and `OnLanguageChanged` has to pick the right one, which is
  why it looks at `this.scanner.IsBusy`.
- **The start order in the constructor matters.** `InitializeLanguageManager` sets `de-DE` and only
  afterwards subscribes to `OnLanguageChanged`, so that first call does not update any control.
  The labels get their text because `LoadLanguagesToCombo` runs last and sets
  `SelectedIndex = 0`, which raises the combo box event, which sets the language again, which now
  does reach the handler. Reordering those two calls leaves the form in English designer text.
- **`GetWord` returns null for an unknown key** and does not fall back to another language. A new
  key has to be added to `de-DE.xml` **and** `en-US.xml`, otherwise one of the two languages shows
  an empty control. Assigning that null to `Control.Text` is legal because Windows Forms annotates
  the setter with `[AllowNull]`, which is why the nullable build stays warning free. Where a
  non-nullable string is needed, as in `ScanResultTexts`, the call site adds `?? string.Empty`.
- **File name and class name disagree.** The file is `GUIExtensions.cs`, the class inside is
  `GuiExtensions`, and the copyright header in it claims the file is called `GuiExtensions.cs`.
- **`Main.Designer.cs` uses a block scoped namespace**, while every hand written file uses a file
  scoped one, although `src/.editorconfig` asks for
  `csharp_style_namespace_declarations = file_scoped:warning`. That never breaks the build because
  `EnforceCodeStyleInBuild` is not set, so the IDE style rules are not run by the compiler.
- **The window caption carries the GitVersion informational version.** `InitializeCaption` writes
  `Application.ProductName + " " + Application.ProductVersion`, so an untagged commit shows
  something like `PortScanner 1.0.9-1+Branch.master.Sha...` in the title bar. That is expected, not
  a bug.
- **The installer is tracked although `.gitignore` excludes `*.exe`.**
  `Setup/PortScanner-Setup.exe` is in the repository because it was added with `git add -f`. A new
  installer has to be force added the same way.
- **`Setup/PortScanner-Setup.iss` is UTF-8 with a BOM**, since version 1.0.8.0. Inno Setup reads a
  script as UTF-8 only when a BOM is present, otherwise it falls back to the system code page. Up
  to version 1.0.7.0 the file was Windows-1252 without a BOM, which happened to work on a
  Windows-1252 machine and would have produced `HÃ¤mmer Electronics` in the installer as soon as
  anybody saved it as UTF-8 without BOM. Keep the BOM.
- **The publish is self contained** since version 1.0.8.0.
  `Setup/build-setup-files.bat` calls `dotnet publish -c Release -r win-x64 --self-contained true`,
  which turns roughly 275 files and 118 MB into a 35 MB installer instead of the 1.8 MB of the
  framework dependent build up to 1.0.7.0. In exchange the target machine needs no installed .NET
  desktop runtime. The `runtimeconfig.json` of the publish shows which of the two it is:
  `includedFrameworks` means self contained, `frameworks` means framework dependent.
- **AppVeyor badge without CI in the repository.** `README.md` links an AppVeyor build that is
  configured outside of this repository. There is no pipeline file here.
- **`src/PortScanner.sln.DotSettings`** is tracked and holds nothing but a ReSharper user
  dictionary (`H_00E4mmer`, `Mustnt`, `Portscan`). Leave it alone.
- **`.gitattributes` sets `* text=auto`** and every rule of the Visual Studio template below it is
  commented out. Any binary file that must not be normalized needs its own rule.

## Releasing

1. Make the change.
2. Add an entry at the top of `Changelog.md` in the existing format:
   `* **Version 1.0.9.0 (2026-08-15)** : Short description.`
3. Set `MyAppVersion` in `Setup/PortScanner-Setup.iss` to the same four part version, without
   losing the BOM of that file.
4. Commit that.
5. Tag the commit with the plain version number, no `v` prefix (`1.0.8`, `1.0.7`, ...). The
   existing tags are lightweight tags, create new ones the same way.
6. **Then** build the installer, not before. `Setup/build-setup-files.bat` publishes the
   application and the Inno Setup compiler `ISCC.exe` turns `Setup/PortScanner-Setup.iss` into
   `Setup/PortScanner-Setup.exe`. GitVersion reads the tag, so an installer built before the tag
   exists carries a prerelease version in the executable.
7. `git add -f Setup/PortScanner-Setup.exe` and commit it, by convention with the message
   `Updated setup.`.
8. Push the commits and the tag.

The version in the `Changelog.md` has four parts (`1.0.9.0`), the tag has three (`1.0.9`).
GitVersion turns the tag into the assembly version, so an untagged commit produces something like
`1.0.9-1+Branch.master.Sha...`.

Note on running the batch file from an agent: `NoDefaultCurrentDirectoryInExePath` is set in this
environment, so `cmd` does not search the current directory for executables. Call it as
`call .\build-setup-files.bat` after a `cd /d` into `Setup`, because the `cd ..\src` inside the
batch file is relative to the start directory. A double click or a normal console is unaffected.

## Git

- **Never amend a commit.** No `git commit --amend`, not for a typo in the message, not to add a
  forgotten file, not even when the commit is still local. Write a follow-up commit instead. The
  release versions come from tags on exact commits, an amended commit leaves its tag pointing at a
  commit that no longer exists in the branch.

## Writing style

- Commit messages are written **in English only**: short, precise subject line, explanatory body
  when needed.
- Code comments and comments in project files such as `.csproj` are **always English**, regardless
  of the language used in the conversation.
- **No em dashes or en dashes** (`—`, `–`), neither in prose, commit messages, code comments nor
  documentation. Use a regular hyphen, comma, colon, parentheses or a separate sentence.
- German texts (documentation, chat replies) always use real umlauts and ß, never ASCII
  transliterations such as `ae`, `oe`, `ue` or `ss`. Identifiers, file names and configuration keys
  stay unchanged where umlauts are technically undesirable.
