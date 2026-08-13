# Openness Capability Matrix

This matrix is based on the installed TIA Portal V21 public API documentation in `C:\Program Files\Siemens\Automation\Portal V21\PublicAPI\V21\net48\`, the compiled project references, and offline regression tests. It does not mark a live fixture export rerun as verified.

| Domain | Artifact | Scan | Export | Import | Format | Adapter | Target restrictions | Verified |
| ------ | -------- | ---: | -----: | -----: | ------ | ------- | ------------------- | -------: |
| PLC | UDT | Yes | Yes | Yes | XML | `PlcType.Export` / `PlcTypeComposition.Import` | Requires matching PLC target and referenced types. | API docs, build, tests |
| PLC | OB | Yes | Yes | Yes | XML | `OB.Export` / `PlcBlockComposition.Import` | Safety/protected blocks can be rejected by TIA. | API docs, coverage report |
| PLC | FB | Yes | Yes | Yes | XML | `FB.Export` / `PlcBlockComposition.Import` | Requires referenced types and blocks. | API docs, build |
| PLC | FC | Yes | Yes | Yes | XML | `FC.Export` / `PlcBlockComposition.Import` | Requires referenced types and blocks. | API docs, build |
| PLC | DB | Yes | Yes | Yes | XML | `DataBlock.Export` / `PlcBlockComposition.Import` | Instance DB import depends on owning FB and technology context. | API docs, build |
| PLC | PLC tag table | Yes | Yes | Yes | XML | `PlcTagTable.Export` / `PlcTagTableComposition.Import` | Requires compatible PLC tag table root. | API docs, build |
| PLC | PLC watch table | Yes | Yes | Yes | XML | `PlcWatchTable.Export` / `PlcWatchTableComposition.Import` | Uses `WatchAndForceTableGroup.WatchTables`. | API docs, tests |
| PLC | PLC force table | Yes | Yes | Yes | XML | `PlcForceTable.Export` / `PlcForceTableComposition.Import` | Uses `WatchAndForceTableGroup.ForceTables`. | API docs, tests |
| PLC | PLC user constant | Yes | Yes | Yes | XML | `PlcUserConstant.Export` / `PlcUserConstantComposition.Import` | Unit is an individual constant under its owning tag table. | API docs, build |
| PLC | Technology object | Yes | Yes | Yes | XML | `TechnologicalInstanceDB.Export` / `TechnologicalInstanceDBComposition.Import` | Represents technological instance DB payload, not a full guarantee of all technology object configuration. | API docs, build |
| PLC | PLC external source | Yes | No | No | SCL | Detectable only | Public API exposes create/generate from source, not direct export of existing source text. | API docs, coverage report |
| PLC | PLC alarm text list | Yes | No | No | XLSX | Detectable only | Public API exposes provider-level XLSX exchange, not a per-list artifact flow in this pipeline. | API docs |
| HMI Classic | Screen | Yes | Yes | Yes | XML | `Screen.Export` / `ScreenComposition.Import` | Requires compatible classic HMI target. | API docs, existing coverage repository |
| HMI Classic | Screen folder | No | No | No | XML | UI grouping only | Folder nodes expand to contained screens. | Catalog validation |
| HMI Classic | Template | Yes | Yes | Yes | XML | `ScreenTemplate.Export` / `ScreenTemplateComposition.Import` | Requires compatible classic HMI target and referenced faceplates/types. | API docs, coverage report |
| HMI Classic | Popup | Yes | Yes | Yes | XML | `ScreenPopup.Export` / `ScreenPopupComposition.Import` | Requires compatible classic HMI target. | API docs, coverage report |
| HMI Classic | Slide-in | Yes | Yes | Yes | XML | `ScreenSlidein.Export` / `ScreenSlideinComposition.Import` | Flat slide-in root in V21. | API docs |
| HMI Classic | Global elements | Yes | Yes | Yes | XML | `ScreenGlobalElements.Export` / `HmiTarget.ImportScreenGlobalElements` | Singleton imported at target level. | API docs, coverage report |
| HMI Classic | Screen overview | Yes | Yes | Yes | XML | `ScreenOverview.Export` / `HmiTarget.ImportScreenOverview` | Singleton imported at target level. | API docs, coverage report |
| HMI Classic | HMI tag table | Yes | Yes | Yes | XML | `TagTable.Export` / `TagTableComposition.Import` | System tag tables are item-level unavailable; user tables require connections/tags/types. | API docs, coverage report |
| HMI Classic | HMI connection | Yes | Yes | Yes | XML | `Connection.Export` / `ConnectionComposition.Import` | Imported before HMI tags. | API docs |
| HMI Classic | Text list | Yes | Yes | Yes | XML | `TextList.Export` / `TextListComposition.Import` | Requires compatible classic HMI target. | API docs, coverage report |
| HMI Classic | Graphic list | Yes | Yes | Yes | XML | `GraphicList.Export` / `GraphicListComposition.Import` | Auxiliary graphic payloads are represented by manifest file lists when present. | API docs, coverage report |
| HMI Classic | Cycle | Yes | Yes | Yes | XML | `Cycle.Export` / `CycleComposition.Import` | System cycles are item-level unavailable and kept as dependencies. | API docs, coverage report |
| HMI Classic | VB script | Yes | Yes | Yes | XML | `VBScript.Export` / `VBScriptComposition.Import` | Script XML is preserved exactly. | API docs, coverage report |
| HMI Unified | Unified tag table | No | No | No | Directory | Disabled | Unified directory exchange is outside the classic XML pipeline. | Catalog validation |
| HMI Unified | Unified script module | No | No | No | Directory | Disabled | Unified directory exchange is outside the classic XML pipeline. | Catalog validation |
| HMI Unified | Unified text list | No | No | No | Directory | Disabled | Unified directory exchange is outside the classic XML pipeline. | Catalog validation |
| HMI Unified | Unified graphic list | No | No | No | Directory | Disabled | Unified directory exchange is outside the classic XML pipeline. | Catalog validation |
| Hardware | Hardware CAx | No | No | No | AML | Disabled | Project/device-level transfer, not a selectable artifact in this version. | Catalog validation |

## Coverage Reconciliation

The supplied operation report contains two real runs. The second run discovered 323 artifacts and exported 265 legacy XML files. The generated repository ZIP contains 110 PLC block XML files, 83 PLC type XML files, 28 PLC tag table XML files, 44 HMI screen XML files, `meta/manifest.json`, and `meta/tia.txt`.

The previous unsupported dispatcher affected 53 HMI artifacts, 1 watch table, 1 force table, and 2 external sources. The current catalog and adapter registry classify the 53 HMI artifacts plus watch/force tables as implemented pipeline artifacts. External sources remain detectable but unavailable because the installed public API does not expose direct export of existing source text. Safety blocks are classified at export time as restricted when TIA returns a not-permitted diagnostic.
