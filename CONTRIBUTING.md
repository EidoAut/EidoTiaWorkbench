# Contributing

Thank you for helping improve EidoTiaWorkbench. This project targets Siemens TIA Portal Openness V21 on .NET Framework 4.8 and must stay safe for engineering projects.

## Development Setup

- Install TIA Portal V21 and Openness V21 when validating live import/export behavior.
- Install the .NET Framework 4.8 Developer Pack and Visual Studio/MSBuild.
- Build x64 only:

```powershell
msbuild EidoTiaWorkbench.slnx /restore /p:Configuration=Debug /p:Platform=x64
```

## Quality Bar

- Keep Openness calls on the STA worker.
- Preserve existing behavior unless a change is required for correctness or safety.
- Add or update offline tests for repository, manifest, validation, ordering, and path logic.
- Document Siemens-dependent limitations instead of hiding them behind generic success messages.
- Do not commit Siemens DLLs, generated binaries, zip archives, logs, or client project data.

## Style

- C# uses K&R braces as enforced by `.editorconfig`.
- Public and private members should explain intent when the reason is not obvious.
- Prefer small, focused changes over broad rewrites.

## Validation

Run the offline test runner after a Debug build:

```powershell
.\EidoTiaWorkbench.Tests\bin\x64\Debug\EidoTiaWorkbench.Tests.exe
```

Live TIA Portal validation should be recorded with the TIA Portal version, project type, selected artifacts, and any Siemens exception diagnostics.
