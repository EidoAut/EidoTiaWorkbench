# Export Recovery

## Partial Export

The repository can now represent a partial export safely. A partial run still writes:

- Valid payloads that succeeded.
- Manifest entries for every scanned artifact.
- No-payload entries for restricted, unavailable, failed, aborted, and cancelled artifacts.
- `meta/operation-report.txt` with the final run status and detailed lists.

The UI distinguishes `SucceededWithLimitations`, `PartiallySucceeded`, and `Aborted` instead of collapsing all non-perfect runs into a generic error.

## Transactional Payload Preservation

Each artifact is exported to an isolated temporary Openness file, loaded, optionally normalized, then written to a same-directory temporary repository file.

The final payload is replaced only after:

- Openness returned.
- The temporary file exists.
- The file is non-empty.
- XML payloads parse as XML.
- The hash was calculated.

If validation or promotion fails, the previous valid payload remains in place. The manifest records the latest failed attempt separately from the last successful payload metadata.

## Retry Incomplete Export

The `Retry incomplete` action reads the last manifest and selects only entries that are safe to retry:

- `Failed`
- `AbortedSessionLost`
- Entries explicitly marked `IsRetryable`

It does not reuse Openness object references from the failed session. The current scan is matched by `LogicalKey`, and objects are resolved again in the open project.

`Restricted` artifacts are not retried automatically. `Unavailable` artifacts are not retried unless future code explicitly marks an item retryable.

## Cancellation

Cancellation is cooperative and runs between artifacts. The UI labels this as `Cancel after current artifact` because interrupting a Siemens Openness call in the middle would be unsafe.

When cancellation is requested:

- The current Openness call is allowed to finish.
- Remaining artifacts become `Cancelled`.
- The manifest and report are saved.
- Previously valid payloads are preserved.

## Recovering After Session Loss

After `AbortedSessionLost`:

1. Reopen or reattach to the intended TIA Portal V21 project.
2. Run a fresh scan.
3. Use `Retry incomplete`.
4. Review `meta/operation-report.txt` and `meta/manifest.json`.
5. If a specific artifact still fails while the session remains healthy, inspect its `Failed` technical detail.

No automatic reconnect is attempted because project selection and TIA Portal lifecycle are safety-sensitive.
