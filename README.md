# Eido TIA Workbench

Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

WinForms utility for **TIA Portal Openness V21** that scans, exports, imports, and validates PLC artifacts and classic WinCC HMI artifacts in a structured engineering workspace.

## Requirements

- TIA Portal V21 installed.
- TIA Portal Openness V21 installed and enabled.
- The Windows user allowed to use Openness according to the Siemens installation requirements.
- .NET Framework 4.8 Developer Pack.
- Visual Studio/MSBuild with C# support.
- x64 build and runtime only.

## Siemens Openness DLLs

Siemens DLLs are not included in this repository and must not be copied or redistributed.

The project references the official local V21 assemblies from:

`C:\Program Files\Siemens\Automation\Portal V21\PublicAPI\V21\net48\`

Required references in `EidoTiaWorkbench.Core`:

- `Siemens.Engineering.Base.dll`
- `Siemens.Engineering.Step7.dll`
- `Siemens.Engineering.WinCC.dll`

All Siemens references are configured with `Copy Local = False` (`Private=False`). They are resolved from the local TIA Portal installation at runtime and are not redistributed.

Classic WinCC support is based on the installed public V21 XML documentation and DLLs. Supported classic HMI artifacts include screens, templates, popups, slide-ins, HMI tag tables, HMI connections, text lists, graphic lists, user cycles, VB scripts, global screen elements, and screen overview. Unified exchange, hardware CAx, PLC external source export, and per-list PLC alarm text list XML exchange are classified conservatively when the public API does not expose a safe per-artifact round-trip.

## Runtime Resolution

At startup the WinForms application resolves Openness V21 assemblies in this order:

1. Environment variable `TIA_OPENNESS_V21`.
2. `C:\Program Files\Siemens\Automation\Portal V21\PublicAPI\V21`
3. `C:\Program Files\Siemens\Automation\Portal V21\PublicAPI\V21\net48`
4. Equivalent `Program Files (x86)` paths.

`TIA_OPENNESS_V21` may point either to `PublicAPI\V21` or directly to `PublicAPI\V21\net48`.

After locating PublicAPI, the application also adds the corresponding `Portal V21\Bin` folder to the process assembly/native DLL search path. This is required by the Siemens Openness runtime when it initializes `Siemens.Engineering.TiaPortal`.

If Openness V21 is missing or cannot be loaded, the application shows a startup error with the path that must be fixed.

## Build

The solution uses `StyleCop.Analyzers` through `PackageReference`, so run restore when building from a clean checkout.

Build the solution in Release x64:

```powershell
msbuild EidoTiaWorkbench.slnx /restore /p:Configuration=Release /p:Platform=x64
```

or build the WinForms project directly:

```powershell
msbuild EidoTiaWorkbench\EidoTiaWorkbench.WinForms.csproj /restore /p:Configuration=Release /p:Platform=x64
```

The build output must not contain Siemens DLLs. They are loaded at runtime from the local TIA Portal V21 installation.

Style and analyzer conventions are documented in `STYLE_GUIDE.md`.

## Repository Layout

PLC artifacts are exported below:

```text
PLC/<Device>/<CPU>/<Category>/<GroupPath>/<Name>__<hash>.<format>
```

Classic HMI artifacts are exported below:

```text
HMI/<Device>/<HMI target>/<Category>/<FolderPath>/<Name>.xml
```

Current categories include `Blocks`, `Types`, `TagTables`, `WatchTables`, `ForceTables`, `UserConstants`, `TechnologyObjects`, `Screens`, `Templates`, `Popups`, `SlideIns`, `Tags`, `Connections`, `TextLists`, `GraphicLists`, `Cycles`, `Scripts`, `GlobalElements`, and `ScreenOverview`.

PLC XML can be normalized for deterministic diffs. HMI screen XML is preserved by default because the Openness WinCC screen XML is the safe reimport format.

## Usage

1. Start TIA Portal V21 or let Eido TIA Workbench start it.
2. Open or select a TIA project.
3. Scan PLC artifacts and classic HMI artifacts.
4. Export selected items or all items to the chosen repository folder.
5. Import selected repository artifacts back into the open project when needed. Existing HMI artifacts can be skipped or replaced, and replacement exports a backup first when backup mode is enabled.

Long-running commands share one operation state model. The shell disables the active view, shows the busy cursor, updates status text immediately, and uses determinate progress when the amount of work is known or marquee progress when it is not.

Import always runs a local preflight before calling Siemens Openness. Preflight checks repository state, manifest consistency, XML well-formedness, duplicate IDs, common missing ID/reference problems, unsupported artifacts, and dependency-plan issues. Dry run is non-destructive and intentionally reports simulated actions only; it does not claim Siemens XML import compatibility until a real import calls Openness.

Import selection defaults to `New`, `Modified`, and `Conflict` repository artifacts. `Identical` artifacts are left unchecked.

Cancellation is cooperative and safe for Openness: it prevents new artifact work between items, but it does not release the UI while queued or active STA work may still touch TIA Portal.

Exports written by the tool include `meta/tia.txt`, `meta/manifest.json`, and `meta/operation-report.txt`.

`manifest.json` schema version 3 is an execution inventory, not just a list of successful payloads. It contains one entry per scanned artifact, including `Unavailable`, `Restricted`, `Failed`, `AbortedSessionLost`, `Cancelled`, and `SkippedUnchanged` items. Entries without payload keep empty `PrimaryPath`, `Files`, and hash fields while preserving the final status and reason.

Export runs finish with one of five global states: `Completed`, `CompletedWithIssues`, `Aborted`, `Cancelled`, or `Failed`. Artifact-level issues such as protected PLC blocks, unsupported API gaps, and isolated HMI GraphicList export errors are recorded per artifact so the remaining queue can continue when the TIA session is still valid.

If a TIA Portal project/session appears lost during export, the exporter makes one controlled recovery attempt. Recovery revalidates or reattaches the portal, adopts only the matching project by the captured project name/path, and resolves later artifacts again by `LogicalKey` instead of reusing old Openness references. Only confirmed unrecovered session loss marks remaining artifacts as `AbortedSessionLost`.

## Tests and Visual Validation

The solution includes `EidoTiaWorkbench.Tests`, an offline .NET Framework 4.8 x64 console test runner for path layout, catalog/adapter consistency, manifest round-trips, no-payload entries, retry selection, repository inventory parsing, schema 3 manifest lookup, multi-format payloads, compound manifest payloads, invalid XML detection, import preflight, dependency ordering, exception diagnostics, hashing, export preflight, GraphicList failure isolation, session-loss policy, safe STA cancellation, state classification, transactional payload preservation, anonymized coverage-regression counts, and import conflict policy stability.

Run tests after a Debug build:

```powershell
.\EidoTiaWorkbench.Tests\bin\x64\Debug\EidoTiaWorkbench.Tests.exe
```

Final UI validation screenshots are stored in `validation-screenshots` and cover dashboard, export, import, settings, and the about dialog.

## Contributing and License

See `CONTRIBUTING.md` for setup, style, validation, and Siemens-data handling guidelines.

Eido TIA Workbench is released under the MIT License. See `LICENSE`.
