# Eido TIA Workbench

Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

**Make your PLC project accessible to your favorite AI assistant.**

Eido TIA Workbench exports supported PLC and classic HMI objects from **Siemens
TIA Portal V21** into structured files that you can give to an AI assistant as
engineering context. The goal is to help you understand existing logic, develop
and review changes, document the program, and investigate machine faults with the
actual project as a reference.

Instead of describing every block by hand or relying only on screenshots, you can
share the relevant exported blocks, data types, tags and HMI objects. These files
provide material for an assistant to inspect and reason about; the quality of its
analysis still depends on the exported format, available dependencies and context.

The Windows application prepares and manages that engineering workspace. It does
not contain an AI model or automatically send your project to an AI service. You
choose the assistant and which files to share. Git-based reviews, offline
validation and controlled imports support this central workflow.

An **artifact** is an individual engineering object, such as a function block,
data type, tag table or HMI screen. A **workspace** or **repository** is the local
folder containing exported artifacts and their metadata; it does not have to be
a Git repository.

Engineering operations use Siemens Openness. Local repository validation works
**offline**, so the application can open and inspect files without loading Siemens
assemblies. V17–V20 can be detected in Settings but **do not have implemented
adapters**. See the [compatibility matrix](docs/Modernization.md#compatibility-matrix).

![Eido TIA Workbench exporting PLC and HMI artifacts with live progress and the Activity panel open](docs/images/eido-tia-workbench-export.png)

*Export in progress with the current artifact, completed-item count, cooperative
cancellation and Activity diagnostics visible in the same workspace. Individual
export restrictions are recorded per artifact; the screenshot shows an ongoing
operation, not a completed or error-free run.*

## What can you do with it?

| Task | What the application provides |
| --- | --- |
| Give an AI assistant project context | Export supported engineering objects as files that can be attached to a conversation or opened in an assistant-enabled workspace. |
| Support programming and troubleshooting | Provide the logic, types and tags needed to discuss behavior, propose changes and investigate fault hypotheses with your chosen assistant. |
| Inspect a project | Scan its PLC and classic HMI objects, browse the inventory by target and kind, and select the items to process. |
| Export engineering files | Write selected artifacts into a predictable directory structure, with optional PLC XML normalization and preserved HMI screen XML. |
| Maintain a versioned workspace | Reuse an incremental export destination, skip verified unchanged payloads and review file changes with your own Git or diff tools. |
| Check a workspace offline | Detect malformed XML, inconsistent manifests, missing payloads, common dependency/reference problems and declared version mismatches. |
| Prepare an import | Load repository artifacts, inspect local changes, preview actions and choose how existing target objects should be handled. |
| Diagnose a run | Follow the current artifact and counter, open Activity during processing and inspect per-artifact errors and the final operation report. |

Typical uses include understanding an unfamiliar PLC program with AI assistance,
tracing the conditions that prevent a sequence from advancing, preparing a change
to a function block, and generating documentation from the exported logic. The
same workspace can be reviewed with diff tools and versioned with Git.

An artifact export is **not a complete TIA project archive**. It does not promise
to reconstruct all hardware configuration, protected content or unsupported
objects. Keep the original project and use TIA's project-archive facilities when
you need a complete project backup. The tool also does not automatically commit,
push or synchronize files with GitHub.

## Working with your favorite AI

### From a TIA project to useful context

1. **Export the relevant scope.** Include the blocks involved in the task and their
   supporting types and tag tables. Add HMI objects when operator commands,
   indications or alarms are relevant. Check the export report for missing,
   restricted or failed objects.
2. **Open or share the exported files.** Use an assistant that can read files or
   access a local workspace. For a large project, start with the subsystem you are
   investigating and expand the context as needed.
3. **Describe the task and expected behavior.** Explain what the machine should
   do, what it actually does, and which blocks or signals you suspect. Ask the
   assistant to identify the files and logic supporting its conclusions and to
   distinguish observations from assumptions.
4. **Review the proposed explanation or change.** Use the assistant's output to
   guide your engineering work. If it produces an importable artifact, validate
   and preview it before an explicit import. Otherwise, implement the reviewed
   change in TIA Portal. Compile and test changes in the intended environment.

The export keeps the formats provided by the supported Openness operations,
including structured XML. It does **not** translate every PLC language into SCL
or guarantee that every model can interpret every Siemens XML construct.

### Example tasks

**Understand an existing sequence**

> Using these exported blocks, types and tags, explain how the automatic cycle
> starts and advances. Identify the relevant states, transitions and interlocks.
> Cite the files and symbols you use, and list any missing dependencies.

**Investigate a machine fault**

> The conveyor does not start after the operator requests automatic mode. Here
> are the relevant exported blocks, the alarm message and the signal values
> observed at the time. Trace the conditions required for the start command,
> identify which observations support each hypothesis, and suggest what to check
> next. Do not assume a signal value that I have not supplied.

**Prepare a programming change**

> Review this function block and its interface. Propose a timeout diagnostic for
> this sequence step, following the existing naming and programming conventions.
> Explain the affected states, interface changes and tests needed to verify the
> behavior. Keep the existing interlocks intact.

### What the export can tell you about a fault

Exported engineering files describe program structure and logic. They are not a
live recording of the machine: the tool does not collect online PLC traces,
physical measurements or a synchronized snapshot of process values for an AI.
For troubleshooting, add the observed symptoms, alarm text, relevant online
values, timing and operating conditions. Values present in exported engineering
files should not be treated as proof of the machine's state at the fault time.

An assistant can use this combined context to explain conditions and propose
checks. An export alone cannot establish that a sensor, actuator or physical
process caused a particular failure. The Activity log shown in this application
records Workbench operations, not the machine's runtime alarm history.

## How it works

```text
Open TIA project
      |
      v
Scan and select artifacts
      |
      v
Export through Openness --> Files + manifest + operation report
                                      |
                                      v
                           Your AI assistant + task context
                                      |
                                      v
                           Review explanation or proposed changes
                                      |
                                      v
                           Validate / preview / explicitly import
                           or implement changes in TIA Portal
```

The scan identifies objects and builds the selectable inventory. Export then
resolves the selected objects in the connected project, calls the matching
Openness export operation and records the result for each item. A protected or
unavailable item can therefore appear in the inventory without producing a file.

The manifest records artifact identity, payload paths, hashes and outcomes. During
an incremental export, the application compares the newly exported content with
the recorded hash and verifies the existing payload before deciding to skip a
write. **Incremental means fewer file writes; it does not eliminate the Openness
calls needed to obtain the current content.**

Import travels in the other direction: repository files are inventoried and
validated, dependencies are ordered, and the chosen conflict policy is applied
before supported objects are imported through Openness. A successful local
validation cannot guarantee that Siemens will accept a particular payload.

Under the hood, the WinForms interface and Core handle navigation, files,
validation and reporting. A separate worker process loads the V21 adapter and
executes Siemens calls on a single STA thread. Commands are serialized while
progress and diagnostics return to the interface. This separation lets local
features work without Openness and keeps engineering calls out of the UI thread.

## Find your way around

| Page | Purpose |
| --- | --- |
| **Dashboard** | Overview of the connection, active project, workspace and latest operation. |
| **Project** | Discover running TIA instances, attach or start an instance, open a project and scan its objects. |
| **Export** | Choose the scan profile, filter and select artifacts, configure export options and start or retry an export. |
| **Import** | Load a workspace, inspect artifact states, preview the selection and configure conflict handling and backups. |
| **Validation** | Run local checks against a repository without importing it. |
| **Activity** | Filter messages, inspect exception details, copy entries, open the log or export a report. |
| **Settings** | Select the Openness installation and configure the workspace and application preferences. |

## Usage

### 1. Connect and scan

1. Open **Settings → TIA Portal Integration** and select a usable V21 installation.
   Set the workspace folder where you want exports to be written.
2. In **Project**, attach to a running TIA Portal instance or start one from the
   application, then open/select the project to work with. Attaching uses an
   existing session; starting an instance lets the application own its lifecycle.
3. In **Export**, choose a scan profile and whether to include DBs, then refresh
   the inventory. Browse the tree and filter by scope, kind, device or text.
4. Select the artifacts you want to export. Scanning reads the inventory; it does
   not export files or import changes into the project.

A scan belongs to the project from which it was obtained. If you switch or lose
that project, scan again before exporting. Runtime discovery alone does not prove
that the Windows account has the required Openness access.

### 2. Export a selection

Choose **Export selected**, **Export all** or **Export changed** according to the
selection you want to process. Review these options before starting:

| Option | Effect |
| --- | --- |
| **Include DBs** | Includes data blocks in the scanned PLC inventory. |
| **Normalize PLC XML** | Normalizes supported PLC XML to make file comparisons more consistent. |
| **Preserve HMI XML** | Keeps HMI screen XML in its exported form for the supported round-trip path. |
| **Incremental** | Reuses the project destination and manifest, checking existing payloads before skipping unchanged files. |

With incremental export enabled, the destination is a project-named directory
under the workspace. A full, non-incremental export creates a separate run folder
with a timestamp and unique suffix. Select the generated repository root—the
folder containing `PLC`, `HMI` and/or `meta`—when loading that export for validation
or import.

For example, after exporting a PLC function block, you can review its XML in a
diff tool and commit the workspace using your normal Git workflow. On the next
incremental run, unchanged verified payloads remain in place. If the principal
payload was edited locally, the tool reports the discrepancy instead of silently
overwriting that edit.

### 3. Validate and preview

Open **Validation** to check a repository offline, or load the repository from
**Import** to inspect and select its artifacts. Repository states such as `New`,
`Modified` and `Identical` compare local files with their recorded manifest
baseline; they are **not a live comparison with the open TIA project**.

Use **Preview selected** or **Dry run** to review the intended import. Dry run does
not apply changes to Siemens objects. Resolve blocking validation issues before
proceeding to a real import.

### 4. Import deliberately

Connect to the intended target project and review the selected files. Existing
objects require an explicit conflict policy:

| Policy | Existing target object |
| --- | --- |
| **Skip** | Keep the existing object and omit its replacement. |
| **Replace** | Permit replacement of the existing object. |
| **Ask** | Block replacement until the decision is resolved. |

When backups are enabled, the required backup must succeed before replacement.
Switch out of dry-run mode to perform a real import; the primary action is then
labelled **REAL IMPORT** and retains its confirmation dialog. Review the final
summary and Activity details rather than assuming every selected object succeeded.

### 5. Follow progress and inspect results

Long-running commands share one operation state model. The progress panel shows
the current phase, artifact and completed-item count, with indeterminate progress
when the total is unknown. Navigation, Activity filters, log copying and opening
the log remain available. Incompatible engineering commands stay disabled until
the current operation ends. The redundant status-bar progress indicator has been
removed.

Import always runs a local preflight before calling Siemens Openness. Preflight checks repository state, manifest consistency, XML well-formedness, duplicate IDs, common missing ID/reference problems, unsupported artifacts, and dependency-plan issues. Dry run is non-destructive and intentionally reports simulated actions only; it does not claim Siemens XML import compatibility until a real import calls Openness.

Import selection defaults to `New`, `Modified`, and `Conflict` repository artifacts. `Identical` artifacts are left unchecked.

Cancellation is cooperative: it prevents new artifact work between items and
waits for the active Siemens call to finish. The interface remains available for
inspection while cancellation is pending; another engineering operation cannot
start until cleanup completes.

Exports written by the tool include `meta/tia.txt`, `meta/manifest.json`, and `meta/operation-report.txt`.

`manifest.json` schema version 3 is an execution inventory, not just a list of successful payloads. It contains one entry per scanned artifact, including `Unavailable`, `Restricted`, `Failed`, `AbortedSessionLost`, `Cancelled`, and `SkippedUnchanged` items. Entries without payload keep empty `PrimaryPath`, `Files`, and hash fields while preserving the final status and reason.

Export runs finish with one of five global states: `Completed`, `CompletedWithIssues`, `Aborted`, `Cancelled`, or `Failed`. Artifact-level issues such as protected PLC blocks, unsupported API gaps, and isolated HMI GraphicList export errors are recorded per artifact so the remaining queue can continue when the TIA session is still valid.

If a TIA Portal project/session appears lost during export, the exporter makes one controlled recovery attempt. Recovery revalidates or reattaches the portal, adopts only the matching project by the captured project name/path, and resolves later artifacts again by `LogicalKey` instead of reusing old Openness references. Only confirmed unrecovered session loss marks remaining artifacts as `AbortedSessionLost`.

### Incremental repository compatibility

An existing incremental repository must have a verified project identity in its
manifest. For older repositories without that identity, create a full export or
select a new destination. The application does not infer ownership from matching
project names. Primary files are checked by SHA-256; auxiliary files are checked
for existence and nonzero size, without individual content hashes.

## What gets written to disk

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

## Requirements and build

Windows, .NET Framework 4.8 and x64. Build with Visual Studio/MSBuild and restore
StyleCop.Analyzers. The full solution requires the local V21 SDK; only Core and
OfflineTests can be built without Siemens.

```powershell
msbuild EidoTiaWorkbench.slnx /restore /p:Configuration=Release /p:Platform=x64
msbuild EidoTiaWorkbench.OfflineTests/EidoTiaWorkbench.OfflineTests.csproj /restore /p:Configuration=Debug /p:Platform=x64
```

For nonstandard build installations set `/p:OpennessV21Path=<PublicAPI V21 net48 path>`.
Deploy EIDO's Desktop, Core, Worker and Openness.V21 outputs together. **Never copy
or redistribute Siemens DLLs.** All proprietary references retain `Private=False`.
Client, Core and worker must be updated together: IPC protocol 2 rejects older
protocol versions.

## Runtime selection

Settings → TIA Portal Integration reads both Windows registry views under
`HKLM\SOFTWARE\Siemens\Automation\Openness`, standard installation paths,
a custom directory and `TIA_OPENNESS`. `TIA_OPENNESS_V21` remains a deprecated
fallback. Assembly identity validation and worker loading precede engineering work.
Automatic selects the highest implemented adapter. An unavailable manual choice
stays offline; it never silently connects with another version. Disconnect and
finish all work before changing the selected runtime.

An installation being detected does not certify permissions or project compatibility.
The [architecture and verification record](docs/Modernization.md) distinguishes those states.
The [corporate C# guide](docs/EIDO_CSharp_Programming_Style_Guide_v1.0.md) defines coding rules.

## Recent improvements

- **Responsive operation feedback:** a single progress panel shows the phase,
  item count and current artifact. Navigation and Activity tools remain available
  during export/import; visual updates are batched to reduce UI work.
- **Project identity checks:** changing or losing the project invalidates the scan.
  Export verifies the scanned project again before processing artifacts. V21
  project paths are read from the API's `FileInfo` value.
- **Incremental integrity:** repositories record their source project path. The
  primary payload hash is checked against the actual file; local modifications
  produce a conflict. Missing or empty declared payloads prevent unchanged skips.
- **Explicit validation failures:** corrupt manifests and missing declared files
  are reported. Import preflight checks the selected target version; an unknown
  target produces a compatibility warning.
- **Worker recovery and IPC:** request IDs correlate commands, progress, results
  and cancellation. Observer failures do not desynchronize replies, and an exited
  worker no longer leaves the UI reporting an open portal.
- **EIDO code style:** updated formatting, XML documentation and programming guide,
  backed by strict build checks and regression tests.

See the [audit and correction record](docs/AUDIT_2026-10-26.md) for evidence,
remaining limitations and the distinction between offline tests and live TIA
acceptance.

## Tests and Visual Validation

The solution includes `EidoTiaWorkbench.Tests`, an offline .NET Framework 4.8 x64 console test runner for path layout, catalog/adapter consistency, manifest round-trips, no-payload entries, retry selection, repository inventory parsing, schema 3 manifest lookup, multi-format payloads, compound manifest payloads, invalid XML detection, import preflight, dependency ordering, exception diagnostics, hashing, export preflight, GraphicList failure isolation, session-loss policy, safe STA cancellation, state classification, transactional payload preservation, anonymized coverage-regression counts, and import conflict policy stability.

Run tests after a Debug build:

```powershell
.\EidoTiaWorkbench.Tests\bin\x64\Debug\EidoTiaWorkbench.Tests.exe
```

The current regression runner contains **35 tests**, passing in both Debug and
Release. Additional audit regressions run through the visual-validation tool,
covering repository failures, project identity, IPC observer failures and worker
exit without opening a Siemens project.

```powershell
.\tools\Test-EidoStyle.ps1
.\EidoTiaWorkbench.OfflineTests\bin\x64\Debug\EidoTiaWorkbench.OfflineTests.exe
.\tools\Test-VisualLayout.ps1 -Configuration Release
```

The style check validates EIDO headers and formatting and builds Debug and Release
with warnings treated as errors. Visual checks produce 24 page renders plus eight
busy-operation renders under `artifacts/validation/redesign-shell`. They simulate
export/import with bursts of activity messages and check progress-text layout,
Activity access and removal of the duplicate progress bar at 100–200% scale.
Native Windows DPI and live project round-trips remain separate manual acceptance
checks.

## Contributing and License

See `CONTRIBUTING.md` for setup, style, validation, and Siemens-data handling guidelines.

Eido TIA Workbench is released under the MIT License. See `LICENSE`.
