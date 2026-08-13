# Changelog

## 2026-08-12 - Product hardening and UI polish

- Fixed STA cancellation semantics so cancelling an import cannot release the UI while queued or active Siemens Openness work may still run.
- Added a regression test proving cancelled queued STA work is not reported complete before the STA worker reaches it.
- Hardened export session recovery with project identity checks based on the project name and path captured before the run.
- Retried the current export artifact after a successful controlled recovery instead of leaving it failed because of a stale project reference.
- Fixed import progress accounting for skipped artifacts so progress reaches the correct processed count.
- Corrected dry-run replacement text so it no longer promises backups when the backup option is disabled.
- Prevented automatic portal start when multiple running TIA instances require explicit user selection.
- Polished the operation summary dialog with clearer status coloring, fewer zero-value badges, a short outcome subtitle, larger layout, and sectioned details.

## 2026-08-11 - Export isolation and GraphicList failure recovery

- Changed global export statuses to `Completed`, `CompletedWithIssues`, `Aborted`, `Cancelled`, and `Failed`, while keeping the previous enum names as compatibility aliases.
- Added export preflight records for item, catalog, and adapter capabilities, including the concrete exporter handler and public Openness API used by each artifact.
- Added an explicit `HmiGraphicListExporter` handler so GraphicList failures such as `DiagState_3` are recorded as isolated artifact failures when the TIA session remains valid.
- Added one controlled session recovery attempt per export run. Recovery revalidates or reattaches the portal and adopts the currently open project before resolving subsequent artifacts from `LogicalKey`.
- Improved operation reports with separate `Unsupported` counts, run diagnosis, root artifact, cascaded not-attempted count, session health, and recovery diagnostics.
- Added offline regression tests for GraphicList preflight, GraphicList failure isolation, and the one-recovery queue policy.

## 2026-08-11 - Import preflight and unified operation feedback

- Added an explicit operation state model for connecting, opening projects, loading repositories, scanning inventory, validating, comparing, exporting, importing, backing up, completion, failure, and cancellation.
- Unified busy cursor, disabled active view controls, status text, and determinate/indeterminate progress handling through the shared operation model.
- Moved repository inventory loading and import dry-run into the same async operation shell used by export/import.
- Added import preflight validation for repository state, manifest consistency, XML well-formedness, duplicate IDs, missing ID-like metadata, local references, HMI dependency hints, datatype metadata, unsupported artifacts, and dependency-plan issues.
- Added dependency planning so selected artifacts are imported in dependency-first order instead of simple category order.
- Improved import diagnostics with full exception-chain logging, including reflection wrappers and Siemens inner exceptions.
- Added item-level import progress, elapsed-time logging, artifact path, target composition, target group, and import strategy to import logs.
- Changed dry-run summaries to say "Simulated" and explicitly state that Siemens XML import compatibility is not verified until a real import.
- Changed default import selection to New, Modified, and Conflicted artifacts while leaving Identical artifacts unchecked.
- Added `CONTRIBUTING.md` and `LICENSE` for open-source readiness.

## 2026-08-11 - Export recovery and full-run manifest

- Added artifact-level export results as the authoritative model for the run: `Succeeded`, `SkippedUnchanged`, `Unavailable`, `Restricted`, `Failed`, `AbortedSessionLost`, and `Cancelled`.
- Added global export statuses: `Succeeded`, `SucceededWithLimitations`, `PartiallySucceeded`, `Aborted`, `Cancelled`, and `Failed`.
- Added explicit TIA session health checks before every artifact and after export exceptions. A lost project/session now stops the queue and marks remaining artifacts as `AbortedSessionLost`.
- Added diagnostic enrichment around reflected Openness `Export` calls, including real object type, selected overload, temporary destination, full exception, stack trace, and HRESULT.
- Kept protected Safety blocks such as `FOB_RTG1` classified as `Restricted`, not as generic errors.
- Implemented transactional per-artifact payload promotion through same-directory temporary files. Invalid or empty exports do not overwrite a previous valid payload.
- Changed `manifest.json` to schema version 3. New entries are keyed by `LogicalKey` and include all scanned artifacts, including entries without payload.
- Preserved compatibility with legacy path-keyed manifests when loading repository inventory and incremental state.
- Updated pruning so no-payload entries such as `Unavailable`, `Restricted`, `Failed`, `AbortedSessionLost`, and `Cancelled` are not removed.
- Added operation report generation at `meta/operation-report.txt` with totals by domain, type, status, restricted/unavailable/error/aborted lists, duration, manifest path, and repository path.
- Added cooperative cancellation between artifacts. The UI presents it as "Cancel after current artifact" to avoid unsafe interruption of an Openness call.
- Added "Retry incomplete" export action. It selects only `Failed`, `AbortedSessionLost`, or explicitly retryable manifest entries and resolves objects again by `LogicalKey`.
- Updated the operation summary UI with global status, aborted/cancelled counters, manifest path, and operation report path.
- Added offline tests for the new state model, no-payload manifest entries, retry selection, schema 3 manifest lookup, restriction classification, session-loss classification, and transactional payload preservation.

## 2026-08-11 - Artifact coverage baseline

- Added a central artifact adapter registry backed by the catalog so export, import, repository inventory, selection, and ordering share one capability source.
- Implemented export dispatch for PLC watch tables, PLC force tables, PLC user constants, technological instance DB payloads, and classic HMI templates, popups, slide-ins, tag tables, connections, text lists, graphic lists, cycles, VB scripts, global elements, and screen overview.
- Implemented import and preview dispatch for the same import-capable PLC and classic HMI artifacts, including group/folder creation when the public Openness composition exposes `Create(string)`.
- Replaced destructive HMI screen replacement with backup plus `ImportOptions.Override`.
- Classified public API gaps as unavailable instead of unexpected failures, including PLC external sources and provider-level PLC alarm text lists.
- Added item-level disabling for system HMI cycles and system HMI tag tables discovered through `IsSystemObject`.
- Extended repository paths and inventory to support XML, SCL, XLSX, AML, directory-style, and compound payload metadata.
- Updated the WinForms tree so non-exportable artifacts are informational, not selectable.
