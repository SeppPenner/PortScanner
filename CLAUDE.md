# Project rules for Claude

## What this is

PortScanner is a small Windows Forms application that scans one host for open TCP ports. The user
types a host name or an IP address, presses the start button, and the application walks the port
range and appends every port it could connect to into a text file
`ScanResult_yyyyMMdd.txt`. That file is written into the current working directory of the process,
not next to the executable. The user interface is bilingual (German and English) and switches at
runtime through a combo box.

One solution `src/PortScanner.sln` with exactly one project:

- `src/PortScanner/PortScanner.csproj`, `OutputType` `WinExe`, `UseWindowsForms`,
  `ApplicationIcon` `Radar.ico`, `RuntimeIdentifiers` `win-x64`.

Layout inside `src/PortScanner`:

- `Program.cs`: the `Main` method, nothing but `Application.Run(new Main())`.
- `Main.cs`: the whole application logic. The constructor calls `InitializeComponent`,
  `InitializeCaption`, `InitializeBackgroundWorker`, `InitializeLanguageManager` and
  `LoadLanguagesToCombo` in exactly that order, see the quirk about the start order below. The scan
  itself runs in a `BackgroundWorker`, its `DoWork`, `ProgressChanged` and `RunWorkerCompleted`
  handlers are the three private methods at the bottom of the file.
- `Main.Designer.cs` plus `Main.resx`: the designer generated form. A `TableLayoutPanel` with the
  host label, the host text box, the language combo box, the start button and the progress bar.
  Do not hand edit these two, they belong to the Windows Forms designer.
- `GUIExtensions.cs`: one extension method `UiThreadInvoke` that marshals an `Action` to the UI
  thread. Note the file name, see the quirks.
- `GlobalUsings.cs`: all usings of the project.
- `languages/de-DE.xml` and `languages/en-US.xml`: the translations, copied to the output directory
  with `CopyToOutputDirectory=Always`.
- `License.txt` and `Radar.ico`: shipped with the application, the license also ends up in the
  installer.

Repository root: `README.md` (the only user documentation, spelled with capital letters unlike the
sibling repositories), `Changelog.md`, `License.txt` (MIT), `Screenshot_DE.PNG`,
`Screenshot_EN.PNG`, `.gitattributes` and `.gitignore`. `Setup/` holds the Inno Setup script, the
publish batch file and the built installer. There is no `.github` folder, no `Updating.md` and no
`HowToUse.md`.

## Build

```powershell
dotnet build src/PortScanner.sln -c Release
```

- Single target framework `net9.0-windows`, no multi-targeting. `RuntimeIdentifiers` is `win-x64`.
- All build properties live directly in `src/PortScanner/PortScanner.csproj`. There is **no**
  `Directory.Build.props` in this repository.
- `TreatWarningsAsErrors` is enabled, so every warning breaks the build, NuGet warnings (`NU****`)
  from restore included. A clean build reports zero warnings, keep it that way.
- `NU1803` (HTTP source usage during restore) is the one warning suppressed via `NoWarn`. Fix
  warnings instead of extending that list. `NuGetAudit` and `NuGetAuditMode=all` are on, so a
  vulnerable transitive package fails the build too.
- Versions come from GitVersion.MsBuild out of the git tags, for example `1.0.8-1` for the first
  commit after tag `1.0.7`. Never edit a version property or an assembly version by hand.
- Restore needs nuget.org. Several private feeds are configured globally on this machine
  (DevExpress, Telerik, a GitHub package feed). If one of them answers 404 for public packages,
  restore fails with `NU1301`. Then build with an explicit source:
  `dotnet build src/PortScanner.sln --source https://api.nuget.org/v3/index.json`.
- There are no tests. A behaviour change is verified by running the application: publish it, start
  it, scan `127.0.0.1` and check the written `ScanResult_yyyyMMdd.txt`. Never claim a run happened
  without running it.

## Code conventions

Follow the surrounding code, it is consistent in every hand written file:

- File header comment block with `<copyright file="..." company="Hämmer Electronics">` and a
  `<summary>`, then the namespace. The designer generated files have no such header.
- XML doc comments on every type and every member, private members included, no exceptions.
- `Nullable`, `ImplicitUsings` and `LangVersion latest` are enabled.
- New `using` directives go into `GlobalUsings.cs`, inside the existing `#pragma warning disable
  IDE0065` block, never at the top of a file. The editorconfig requires usings inside the namespace
  (`csharp_using_directive_placement=inside_namespace:warning`), which global usings cannot
  satisfy, that is what the pragma is for. Do not add other pragmas. The comment text in that block
  is German because Visual Studio generated it, leave it alone.
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

- **The progress report is integer division.** `ScannerDoWork` calls
  `this.scanner.ReportProgress(i / 65535)`, which is `0` for every port but the last one. The
  progress bar therefore never moves during a scan. `ScannerProgressChanged` additionally clamps
  the value with `e.ProgressPercentage > 100 ? 100 : e.ProgressPercentage`, which never triggers.
- **The scanned `TcpClient` is never disposed.** The loop body creates a `TcpClient` per port and
  drops it, so a full run leaks up to 65536 sockets until the finalizer or the process exit cleans
  up. Only the `StreamWriter` around the result file is disposed.
- **A scan practically never finishes.** The blocking `TcpClient(host, port)` constructor waits for
  the operating system connect timeout on every closed port, which is around 21 seconds on a
  dropped (as opposed to refused) connection. Multiplied by 65536 ports the run does not terminate
  in any useful time. The host is also resolved again on every single iteration.
- **The exception handler in the scan loop is empty.** `catch { }` with the comment `// ignored` is
  what makes a closed port a non-event, but it swallows a wrong host name just as silently, so a
  typo produces an empty result file instead of an error.
- **Port 0 is scanned.** The loop starts at `i = 0`, but port 0 is not a valid TCP port.
- **The time stamp in the result file uses `hh`.** `dd.MM.yyyy:hh:mm:ss` is the 12 hour clock
  without an AM/PM designator, so an afternoon scan is stamped `01:15:00` instead of `13:15:00`.
- **`WorkerSupportsCancellation` is set but dead.** Nothing ever calls `CancelAsync` and `DoWork`
  never looks at `CancellationPending`, so a running scan cannot be stopped from the user
  interface.
- **The start order in the constructor matters.** `InitializeLanguageManager` sets `de-DE` and only
  afterwards subscribes to `OnLanguageChanged`, so that first call does not update any control.
  The labels get their text because `LoadLanguagesToCombo` runs last and sets
  `SelectedIndex = 0`, which raises the combo box event, which sets the language again, which now
  does reach the handler. Reordering those two calls leaves the form in English designer text.
- **`GetWord` returns null for an unknown key** and does not fall back to another language. A new
  key has to be added to `de-DE.xml` **and** `en-US.xml`, otherwise one of the two languages shows
  an empty control. Assigning that null to `Control.Text` is legal because Windows Forms annotates
  the setter with `[AllowNull]`, which is why the nullable build stays warning free.
- **File name and class name disagree.** The file is `GUIExtensions.cs`, the class inside is
  `GuiExtensions`, and the copyright header in it claims the file is called `GuiExtensions.cs`.
- **`Main.cs` and `Main.Designer.cs` use a block scoped namespace**, while `Program.cs` and
  `GUIExtensions.cs` use a file scoped one, although `src/.editorconfig` asks for
  `csharp_style_namespace_declarations = file_scoped:warning`. That never breaks the build because
  `EnforceCodeStyleInBuild` is not set, so the IDE style rules are not run by the compiler.
- **The window caption carries the GitVersion informational version.** `InitializeCaption` writes
  `Application.ProductName + " " + Application.ProductVersion`, so an untagged commit shows
  something like `PortScanner 1.0.8-1+Branch.master.Sha...` in the title bar. That is expected, not
  a bug.
- **The installer is tracked although `.gitignore` excludes `*.exe`.**
  `Setup/PortScanner-Setup.exe` is in the repository because it was added with `git add -f`. A new
  installer has to be force added the same way.
- **The Inno Setup script is Windows-1252 without a BOM.** The only non ASCII byte in
  `Setup/PortScanner-Setup.iss` is the `0xE4` of `Hämmer Electronics`. Inno Setup reads a script as
  UTF-8 only when a BOM is present, otherwise it falls back to the system code page. That works on
  a Windows-1252 machine and produces `HÃ¤mmer Electronics` in the installer as soon as anybody
  saves the file as UTF-8 without BOM.
- **The publish is framework dependent.** `Setup/build-setup-files.bat` calls
  `dotnet publish -c Release -o bin/publish` without `--self-contained`, so the target machine
  needs an installed .NET desktop runtime.
- **AppVeyor badge without CI in the repository.** `README.md` links an AppVeyor build that is
  configured outside of this repository. There is no pipeline file here.
- **`src/PortScanner.sln.DotSettings`** is tracked and holds nothing but a ReSharper user
  dictionary (`H_00E4mmer`, `Mustnt`, `Portscan`). Leave it alone.
- **`.gitattributes` sets `* text=auto`** and every rule of the Visual Studio template below it is
  commented out. Any binary file that must not be normalized needs its own rule.

## Releasing

1. Make the change.
2. Add an entry at the top of `Changelog.md` in the existing format:
   `* **Version 1.0.8.0 (2026-08-15)** : Short description.`
3. Set `MyAppVersion` in `Setup/PortScanner-Setup.iss` to the same four part version.
4. Commit that.
5. Tag the commit with the plain version number, no `v` prefix (`1.0.7`, `1.0.6`, ...). The
   existing tags are lightweight tags, create new ones the same way.
6. **Then** build the installer, not before. `Setup/build-setup-files.bat` publishes the
   application and the Inno Setup compiler `ISCC.exe` turns
   `Setup/PortScanner-Setup.iss` into `Setup/PortScanner-Setup.exe`. GitVersion reads the tag, so
   an installer built before the tag exists carries a prerelease version in the executable.
7. `git add -f Setup/PortScanner-Setup.exe` and commit it, by convention with the message
   `Updated setup.`.
8. Push the commits and the tag.

The version in the `Changelog.md` has four parts (`1.0.8.0`), the tag has three (`1.0.8`).
GitVersion turns the tag into the assembly version, so an untagged commit produces something like
`1.0.8-1+Branch.master.Sha...`.

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
