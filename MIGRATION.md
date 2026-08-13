# Migration Notes

## Repository Layout

Existing repositories with only these legacy folders remain readable:

```text
PLC/<target>/Blocks
PLC/<target>/Types
PLC/<target>/TagTables
HMI/<target>/Screens
```

New exports can additionally create:

```text
PLC/<target>/WatchTables
PLC/<target>/ForceTables
PLC/<target>/UserConstants
PLC/<target>/TechnologyObjects
PLC/<target>/ExternalSources
PLC/<target>/AlarmTextLists
HMI/<target>/Templates
HMI/<target>/Popups
HMI/<target>/SlideIns
HMI/<target>/Tags
HMI/<target>/Connections
HMI/<target>/TextLists
HMI/<target>/GraphicLists
HMI/<target>/Cycles
HMI/<target>/Scripts
HMI/<target>/GlobalElements
HMI/<target>/ScreenOverview
```

PLC filenames still include the hash disambiguator. Classic HMI filenames remain readable and use the original artifact name.

## Manifest

The manifest schema remains backward compatible. Older entries with only `Sha256`, `LastExportUtc`, and identity fields can still be scanned. New entries include payload metadata so future compound artifacts can keep auxiliary files attached to one logical artifact.

## Capability Changes

PLC external sources and PLC alarm text lists are visible when scanned but are not selected for export or import unless a safe per-artifact public API path is implemented later. System HMI cycles and system HMI tag tables are also visible but disabled at item level.

Safety/protected blocks are still scanned as normal PLC blocks because the restriction is object-specific and is only confirmed by TIA during export. When TIA returns a protection or permission message, the operation records `Restricted` and does not create or keep a successful manifest entry for that artifact.

## Import Behavior

HMI replacement now backs up the existing artifact and imports with `ImportOptions.Override` instead of deleting first. This reduces destructive risk and keeps the backup if import fails.
