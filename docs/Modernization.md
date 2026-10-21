# Modernization architecture and verification

## Baseline and decisions

Baseline: main `EidoTiaWorkbench`, clean checkout, .NET Framework 4.8/C# 7.3/x64.
The initial build compiled but emitted many StyleCop warnings. Core combined
offline logic with six Siemens-dependent source files. Startup loaded V21 before
creating the UI. Existing services already provided inventory, incremental exports,
preflight, dependency ordering, transactional payloads and session recovery.
These services were retained rather than reimplemented.

Aletheia reference: commit `a118e732d94d0e47d9f0d3945a7e3ccb5f78f197`.
Reviewed MainForm, designer layout, ThemePalette and navigation controls. Adopted
semantic dark surfaces, cyan accent, Segoe UI/Consolas hierarchy, left navigation,
central workspace and status chrome. No financial behavior or dependencies copied.

## Assembly and process boundary

```text
WinForms -> Core (models, inventory, preflight, manifests, discovery, IPC client)
               |
               | versioned data contracts over inherited stdin/stdout pipes
               v
          Worker.exe -> Openness.V21 -> locally installed Siemens DLLs
                            single STA, existing import/export/session services
```

Core and Desktop contain no Siemens assembly references. The version-specific
adapter retains existing namespaces to minimize changes. A worker loads one
validated API directory for its entire lifetime. Switching requires an idle,
disconnected state and replaces the worker. The desktop uses .NET Framework 4.8;
there is no unnecessary frontend framework migration.

Inherited anonymous pipes are used instead of named pipes: only the spawned
process receives the redirected handles and there is no discoverable endpoint.
Protocol 1 has explicit DataContract types, XML DTD prohibition and 64 MiB frame
limits. No BinaryFormatter, arbitrary type activation or Siemens objects cross it.
Commands are serialized. Progress and durable log messages return during execution.
Cancel requests use the independent write channel and are acknowledged only after
safe completion. EOF initiates cooperative cancellation and session cleanup.
A disconnected worker reports uncertainty; it never automatically replays imports.
Initialization has a 20-second timeout. Active Siemens calls have no forced abort.

## Discovery and selection

Discovery reads HKLM SOFTWARE/Siemens/Automation/Openness in both registry views,
standard Portal V* installations, a custom path, TIA_OPENNESS and the deprecated
TIA_OPENNESS_V21 fallback. It validates assembly names, public-key tokens, matching
major versions, architecture and the V21 net48 directory. The worker then proves
that these assemblies can actually load in its framework. Detection is not proof
that the current Windows account has Openness access or that a project round-trip
works. Classic API detection requires Siemens.Engineering and Siemens.Engineering.Hmi.

Automatic chooses the highest valid installation with an implemented adapter.
Manual unsupported/missing selection remains offline; it never silently falls back.
Settings persist atomically under LocalAppData/EidoTiaWorkbench/integration.xml.
The old environment variable is accepted for migration, not required.

## Compatibility matrix

| TIA | Discovery | Adapter | Engineering capabilities | Executed verification | Limitations |
| --- | --- | --- | --- | --- | --- |
| V17 | Version parsing/classic layout | Not implemented | None advertised | Offline version/selection tests | No local SDK or live installation |
| V18 | Version parsing/classic layout | Not implemented | None advertised | Offline version/selection tests | No local SDK or live installation |
| V19 | Version parsing/classic layout | Not implemented | None advertised | Offline version/selection tests | No local SDK or live installation |
| V20 | Version parsing/classic layout | Not implemented | None advertised | Offline version/selection tests | No local SDK or live installation |
| V21 | Installed API identity validated | Extracted existing implementation, isolated worker | Existing PLC/classic HMI catalog; per-item restrictions retained | Debug/Release builds, offline regressions, live fresh-instance start/empty-project snapshot/close and worker cleanup | No attach/open/scan/export/import round-trip against a disposable project |
| Future | Standard path/registry candidates | Unsupported | None | Unknown-version rejection | Requires an explicit adapter and compatibility evidence |

V17–V20 support is deliberately unfinished. The framework for discovery and
selection does not constitute a working legacy adapter. Do not advertise these
versions as supported. Unified exchange retains its existing unsupported status.

## Safety corrections

- Dry-run checkbox routes the primary import button to preview only. Real mode
  visibly says REAL IMPORT and retains the confirmation dialog.
- Ask blocks existing-object replacement until Replace or Skip is chosen. PLC
  imports now resolve existing targets and honor the same decision as HMI.
- Enabled backups must produce a non-empty file before replacement proceeds.
- Worker preflight checks all selected version declarations before any import.
  It rejects declared cross-version payloads and never rewrites Siemens XML.
- Schema 3 remains unchanged. Manifest persistence now uses temporary-file atomic
  replacement. Existing hash and payload recovery tests remain active.
- Attachment selection uses stable process IDs and validates the executable's V21
  file-version family (2100 on the installed SDK), preventing reordered-list attach.
- Offline Validation runs local inventory/XML/dependency checks without Siemens.

Unknown XML provenance is not certified; normal preflight still applies and Siemens
is the final compatibility authority. Conflict detection cannot make another TIA
client's concurrent edits transactional. Use isolated disposable projects for tests.

## Visual system

ThemeColors centralizes Aletheia colors. Existing ThemedButton, StatusBadge,
PageHeader, SidebarNavigation, grid styling and dialog theme infrastructure remain
the shared component system. The dark palette is the default for new preferences;
existing light/system choices are preserved. Settings exposes functional persisted
controls; previous unbound placeholder checkboxes were removed to avoid suggesting
that they changed operation behavior. Import/export options remain in their views.

## Reproducible checks

```powershell
$build = 'C:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\amd64\MSBuild.exe'
& $build EidoTiaWorkbench.slnx /restore /p:Configuration=Debug /p:Platform=x64 /m
& $build EidoTiaWorkbench.slnx /restore /p:Configuration=Release /p:Platform=x64 /m
& .\EidoTiaWorkbench.Tests\bin\x64\Debug\EidoTiaWorkbench.Tests.exe
& .\EidoTiaWorkbench.OfflineTests\bin\x64\Debug\EidoTiaWorkbench.OfflineTests.exe
.\tools\Test-VisualLayout.ps1
```

OfflineTests references only Core and can be built without Siemens. The full
solution requires the V21 SDK to compile the adapter; set `/p:OpennessV21Path=...`
for a nonstandard build installation. Copy only the worker executable and EIDO
adapter assembly beside OfflineTests to run `--worker`; never copy Siemens DLLs.

The visual probe checks navigation, the no-Siemens desktop boundary and dry-run
event routing, and renders pages at 1280x720/1920x1080 with synthetic 100/125/150/200%
control scaling. These are not native per-monitor DPI tests. Real DPI, keyboard
accessibility and live engineering workflows remain manual acceptance work.
Screenshots are local generated artifacts under validation-screenshots/modernization.

## Current execution results

After workspace cleanup, the local evidence logs referenced below are retained in
`artifacts/diagnostics/`. Existing visual captures are in
`artifacts/validation/validation-screenshots/`. Debug outputs and intermediate
build files are archived in `artifacts/build-cache/`; ZIP archives are preserved
in `artifacts/archives/`. The runnable Release remains in
`EidoTiaWorkbench/bin/x64/Release/`, with a root-level Windows shortcut.

- Debug and Release x64 compilation: passed (style/documentation warning backlog remains).
- Regression runner: 34 tests, zero failures in both configurations.
- Independent offline contract suite: passed without a Siemens assembly reference.
- V21 worker load, project snapshot with no session, and orderly cleanup: passed.
- Fresh owned TIA lifecycle: failed during initialization with Siemens `EngineeringSecurityException` ("The operation has timed out."). The user authorized access for this session only and the dialog's `Yes` button was selected, without saving permanent authorization. The test runner, worker and owned TIA process subsequently exited. No project was opened. Start/close lifecycle validation remains unverified; this attempt is not recorded as passed.
- V21 lifecycle retry (2026-10-18): the test did not complete within a manually enforced ten-minute limit. The old harness discarded worker logs, so the blocked phase could not be established from that attempt. The access dialog appeared and disappeared before automated input; its selection was not observed. No `PASS` or Siemens exception was returned. After verifying process identities and parentage, the stalled test-owned portal, worker and runner were terminated. This was forced test cleanup, not a successful lifecycle close. The user's separate `EIDO_Janus` instance was preserved. Evidence: local `lifecycle-retry-output.log` and `lifecycle-retry-error.log`.
- Instrumented V21 diagnosis and fix (2026-10-18): the original STA entry point successfully started TIA and read the project snapshot, then blocked in `TiaPortal.Dispose()`. A managed stack capture showed Siemens shutdown waiting in `GC.WaitForPendingFinalizers()` while the main STA thread was blocked in native console-pipe `ReadFile`. The worker entry point now uses MTA; engineering calls retain their dedicated serialized STA. With that single threading change, the same lifecycle test passed: start, empty-project snapshot, disconnect and process cleanup. Disconnect completed in about 23 ms. Session-only access was granted with `Yes`. Evidence: local `openness-worker-stacks.log`, `lifecycle-diagnostic-output.log` and `lifecycle-mta-output.log`. No project was opened or modified by these tests. This supersedes the earlier unverified start/close result; project operations remain unverified.
- Visual probe: 24 synthetic page renders, navigation and dry-run dispatch passed. Settings and Import layout were revised after image inspection.
- No Siemens DLLs in the desktop Release output.
- No live project was opened or mutated. No legacy-version or native DPI certification is claimed.

## Desktop design revision (2026-10-18)

The presentation layer now shares Segoe UI typography, spacing/geometry tokens,
light and dark semantic palettes, button/checkbox/combo/tree/grid styling and
keyboard focus states. The shell uses grouped navigation and contextual section,
project and operation information. Project sessions, Export filters/inspector,
Import comparison/prevalidation, Validation issues, Activity details, Settings
cards and desktop dialogs use these components. Engineering algorithms, worker
contracts and Siemens adapters were not changed by this design revision.

`tools/Test-VisualLayout.ps1 -Configuration Release -Mode Preview` opens an
isolated interactive DEMO using production views with synthetic data. Engineering
commands are not connected. `-Mode Design` produces 88 view captures and six
dialog captures; `-Mode Checks` runs contrast, navigation and offline presentation
checks. The demo includes 1,200 export artifacts, multiple sessions, busy states,
validation errors and import conflicts. Large-font fixtures pair 1280x720/100%,
1366x768/125%, 1920x1080/150% and 2560x1440/200%; these are synthetic layout checks,
not certification across real monitor DPI configurations.

PerMonitorV2 support is enabled through the .NET Framework application config
and the Windows compatibility manifest. Native title bars/file dialogs retain
Windows styling. Native scrollbar styling depends on Windows theme support.
Actual monitor transitions and assistive-technology testing remain manual checks.
The GDI resource counter in the demo runner is diagnostic; it includes framework
and shared-font caches and does not by itself prove the absence of leaks.

Validation for the design revision: Debug and Release x64 rebuilt with warnings
as errors; EIDO header/encoding/whitespace/analyzer checks passed for 144 maintained
C# files. The 34 regression cases and independent offline contracts passed in both
configurations. The presentation probe passed contrast checks (4.5:1 for the
specified text pairs), navigation, theme persistence and dry-run routing, and wrote
24 shell renders. The demonstration suite wrote 88 scenario renders plus six
dialog renders. Dashboard captures were refreshed after the final font-reflow fix.
See `artifacts/validation/redesign-review.html` for before/after comparisons and
full-size captures. GDI counts are recorded in the render logs; no long-session
leak-free certification is claimed. Narrow/high-scale windows use scrolling and
standard table ellipsis; full content remains available through scrolling,
resizable columns and detail panels. Some native grid scrollbars retain Windows
appearance in dark mode.
