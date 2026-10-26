# EIDO C# Programming Style Guide v1.0

Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

Readability guidance updated on 2026-10-26.

## Purpose and precedence

Adaptation of the EIDO Python Programming Style Guide v1.0 to idiomatic C#.
Prioritize correctness, operational safety, resource ownership and maintainability.
Framework contracts, external schemas and Siemens protocol values take precedence
over cosmetic conventions. Do not translate Python syntax or PLC scan semantics.

## Naming and layout

Types, methods, properties, events, constants and enum members use PascalCase.
Interfaces use IPascalCase, parameters and locals camelCase, private fields
_camelCase, asynchronous methods PascalCaseAsync. Include units in ambiguous
values: timeoutMilliseconds, payloadSizeBytes. Preserve framework overrides and
published contracts; map external names at the boundary.

Use UTF-8 BOM and CRLF for C# to match this Windows repository, four spaces,
K&R braces: place the opening brace on the same line as the declaration or control
statement, separated by one space (for example, `private static int Main(string[] args) {`).
Keep closing braces on their own line. Use one statement per line, System directives
first and outside the namespace. Prefer lines under 120 characters, without artificial alignment.
Use `dotnet format whitespace`; do not combine contradictory formatters.
New syntax must compile under C# 7.3 and .NET Framework 4.8.

## Line length and expression readability

Use 120 characters as the normal line-length target for maintained C# code and
comments. This is a readability guideline, not a reason to break an indivisible
identifier, URL or external value. Review remaining long lines explicitly;
passing the formatter does not establish that expressions are easy to read.

When a call or declaration needs several lines, put its arguments or parameters
on separate lines, indented by four spaces. Keep short signatures intact when
only their expression body needs wrapping. Expand long object initializers to
one assignment per line and include the trailing comma required by StyleCop.
Separate multiline members with blank lines.

Break conditions, concatenations and LINQ chains at meaningful boundaries.
Keep each logical term or pipeline step together; avoid splitting a short nested
call just to move its argument onto another line. Do not use deep indentation or
artificial column alignment to accommodate a complicated expression.

Introduce a descriptive local variable when a conditional, composed message or
combination of flags obscures the surrounding call. Names such as
`integrationMessage`, `projectDetails` and `textFlags` should explain the value's
role. Preserve evaluation order, short-circuit behavior and the number of
property or method evaluations when moving expressions into locals.

Respect SA1116, SA1117 and SA1118 when wrapping calls. In particular, extract a
complex later argument into a local when wrapping it would violate SA1118; do
not suppress the diagnostic or compress the expression back into a very long
line. For example:

```csharp
var integrationMessage = this.SelectedInstallation == null
    ? "Offline mode: no usable adapter selected."
    : this.SelectedInstallation.ToString();
this.Log(ActivitySeverity.Info, "Integration", string.Empty, string.Empty, integrationMessage);
```

Long literal messages may be split into adjacent constant string expressions
joined with `+`. Preserve the exact resulting text, including spaces, escape
sequences and placeholders. Do not change user-facing messages or wire values
as a side effect of cosmetic cleanup.

## Corporate headers

Preserve copyright and licenses. Every new or modified EIDO-owned source has a
compact header describing filename, responsibility, relevant side effects and
execution ownership, followed by the exact signature `Developed by EIDO Automation`.
The separator is `// ` followed by 73 equals signs, exactly 76 characters.
Do not attribute generated, vendor or third-party code to EIDO.

```csharp
// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// OpennessWorkerClient.cs
// Owns one isolated engineering process and exchanges versioned data contracts.
// Serializes commands; cancellation waits for the active Siemens call to finish.
//
// Developed by EIDO Automation
// =========================================================================
```

## Documentation and decomposition

Use English identifiers, technical comments, XML documentation and internal
diagnostics. UI language is independent. Document public contracts with summary,
param, returns and exception tags where applicable. Describe mutation, ownership,
STA requirements, preconditions, cancellation and invalid external states. Do not
narrate obvious syntax. Private helpers need documentation when behavior is
non-obvious, not a duplicate of their names.

For private helpers that manage processes, IPC or discovery, document the
operational contract: required locks, caller-owned streams, partial reads,
startup readiness, bounded traversal, retained diagnostics and cleanup behavior,
as applicable. Describe when cancellation takes effect and whether disposal
waits for accepted work. Do not describe draining a queue as immediate abortion.

Keep XML documentation readable too. When an element needs multiple lines,
place its opening and closing tags on their own documentation lines, and wrap
the prose between them. Keep each parameter in its own XML element, preserve
`cref` and `paramref` references, and keep punctuation attached to the text.
Use `remarks` for lifecycle constraints and `exception` for relevant failure
conditions; avoid adding tags that merely repeat the signature.

Review methods around 50-80 effective lines, classes/files around 400-600 and
nesting beyond three levels. These are review thresholds, not mechanical errors.
Split by responsibility and lifecycle. Optional regions group real responsibilities
such as Initialization, Validation, Execution and Resource Cleanup; never every
method. A region does not cure excessive responsibilities.

Extract a focused helper when a nested callback or initializer hides a distinct
responsibility, such as converting an export progress callback into an IPC frame.
Keep the callback readable and document the helper's inputs and side effects.
Do not create helpers solely to reduce line counts: a useful extraction has a
clear purpose and preserves threading, serialization and exception behavior.

## State, configuration and errors

Use enums for domain states and explicit configuration objects for user choices.
Keep actual Siemens numeric protocol values. Do not replace universally meaningful
zero/one values with unnecessary constants. Keep secrets out of source and logs.
Validate configuration and wire data at their boundaries. Unknown API versions
must never inherit support merely because a directory exists.

Catch specific exceptions where recovery or reporting is possible. Broad catches
belong at UI/process/operation boundaries with a visible failure outcome. Preserve
original exceptions as inner exceptions within a process; transmit diagnostic text
and explicit result states across IPC, never proprietary exception objects.
Do not return an apparent success after a failed operation. Log an error once at
the responsible boundary. Retries must be bounded and safe for the operation.

## Resources and concurrency

Each stream, session, thread, process and CancellationTokenSource has one owner.
Use using/finally and deterministic cleanup. The desktop never owns Siemens objects.
One worker process loads one API family. All engineering calls run through its
single STA queue. Do not block the UI with engineering calls or update controls
from a worker thread. Route progress through the captured UI context.

Cancellation is cooperative: finish an active non-interruptible Siemens call,
then stop before the next artifact. Never terminate a process to simulate safe
cancellation. Do not complete a task while queued work can still mutate a project.
Shutdown drains accepted STA work and releases sessions before worker exit.

## Imports, exports and compatibility

Preview and import are distinct commands. Dry run must not reach a mutating handler.
Ask requires a user decision; it never implies Replace. Skip never replaces.
If backup is enabled, complete and verify the backup before replacement.
Use transactional same-directory promotion for payloads, manifests and settings.
Hash the bytes actually written. Preserve schema 3 compatibility and unknown
provenance; do not label unknown metadata as verified V21. Reject declared
cross-version XML unless an explicitly tested migration path exists.

## Example

```csharp
/// <summary>Decides whether a selected artifact may be imported.</summary>
/// <param name="exists">Whether the target currently contains the artifact.</param>
/// <param name="policy">Explicit conflict decision selected by the user.</param>
/// <returns>False when an existing artifact must be skipped.</returns>
/// <exception cref="InvalidOperationException">Ask still needs a user decision.</exception>
public static bool ShouldImport(bool exists, ImportConflictPolicy policy) {
    if (!Enum.IsDefined(typeof(ImportConflictPolicy), policy)) {
        throw new ArgumentOutOfRangeException(nameof(policy));
    }

    if (!exists) {
        return true;
    }

    if (policy == ImportConflictPolicy.Ask) {
        throw new InvalidOperationException("Choose Replace or Skip first.");
    }

    return policy == ImportConflictPolicy.Replace;
}
```

## Tooling and review

StyleCop.Analyzers 1.1.118, compiler XML diagnostics and `.editorconfig` are shared
by the projects. SA1309 is the documented exception for `_camelCase` private
fields; SA1500 is disabled to allow opening braces on the same line. No blanket suppression or fictional EIDO analyzer is introduced.
Functional failures and security defects are separate from style warnings.
Whitespace formatting does not automatically enforce the 120-character target
or improve decomposition and documentation. Review those aspects manually after
formatting. For readability refactors, inspect the diff for unchanged literals,
contracts, evaluation order and resource ownership; token comparison can support
that review for whitespace-only edits but cannot replace behavioral validation.
Generated Designer sources are excluded. Run `tools/Test-EidoStyle.ps1` to verify
owned headers, encoding, formatting and compiler/analyzer diagnostics in all seven
projects under Debug and Release. Warnings fail that verification; review of
resource ownership and domain contracts remains necessary.

Codex and contributors: read the project and this guide before changes. Apply
headers and formatting to affected owned files; avoid unrelated global churn.
Preserve external contracts and public behavior unless explicitly changed.
Run meaningful offline regressions, compile Debug and Release, inspect UI renders,
and distinguish simulated layouts from actual Windows DPI tests. Live Siemens
integration tests require disposable projects. Report actual commands, failures,
limitations and exceptions; compilation alone does not establish runtime safety.
