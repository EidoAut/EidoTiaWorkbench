# EidoTiaWorkbench Style Guide

Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

The authoritative corporate standard is [EIDO C# Programming Style Guide v1.0](docs/EIDO_CSharp_Programming_Style_Guide_v1.0.md).

## Scope

This project uses the corporate `stylecop.json` located at the solution root. The file is included as an `AdditionalFiles` item in all seven projects, including the worker, V21 adapter, offline tests and visual validation tool. StyleCop.Analyzers reads the same settings in Visual Studio and MSBuild builds.

## StyleCop Integration

All projects reference `StyleCop.Analyzers` version `1.1.118` through `PackageReference` with `PrivateAssets="all"`. The analyzer package is used only at build/design time and is not copied to the runtime output folder.

The projects remain classic .NET Framework 4.8 MSBuild projects. They were not migrated to SDK-style.

## EditorConfig

`.editorconfig` defines formatting that StyleCop does not enforce by itself:

- UTF-8 BOM, CRLF, final newline.
- Four spaces, no tab indentation.
- `System` usings first, usings outside namespaces.
- K&R braces: opening braces on the same line as declarations and control statements, separated by one space.
- One statement per line.
- Standard C# spacing around commas, dots and binary operators.

The rules are compatible with C# 7.3.

## Readability

Follow the corporate guide's [line length and expression readability rules](docs/EIDO_CSharp_Programming_Style_Guide_v1.0.md#line-length-and-expression-readability):

- Target 120 characters; wrap calls, initializers and logical expressions at meaningful boundaries.
- Use descriptive locals for complex messages, conditions and rendering flags, preserving evaluation order.
- Extract focused helpers for distinct callback responsibilities and document non-obvious lifecycle behavior.
- Wrap XML documentation without merging parameter elements or breaking references.
- Keep StyleCop's multiline argument and member-spacing rules enabled. Review readability manually after formatting.

## Naming

- Types, methods, properties and events use PascalCase.
- Local variables and parameters use camelCase.
- Private fields use a leading underscore.
- Interfaces use the `I` prefix.
- Async methods use the `Async` suffix.

## XML Documentation

XML documentation files are enabled for Debug and Release builds. Compiler warning `CS1591` is active; do not add a project-level `NoWarn` entry for documentation warnings.

StyleCop documentation warnings remain enabled with `documentExposedElements` and `documentInternalElements` set to `true`; private helper documentation is contextual. Manual source files should include clear `summary`, `param`, `returns` and relevant `exception` tags for public/internal contracts and non-obvious private helpers.

Manual C# files preserve the copyright line and add the 76-character corporate banner, filename, responsibility and exact `Developed by EIDO Automation` signature. The copyright line remains compatible with the current `xmlHeader=false` configuration:

```csharp
// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.
```

## Generated Code

Generated files are marked in `.editorconfig`:

- `*.Designer.cs`
- `**/Resources.Designer.cs`
- `**/Settings.Designer.cs`

Do not manually reformat or rewrite generated WinForms and resource files to satisfy StyleCop.

## Suppressions

The following StyleCop rules are disabled in `.editorconfig` with project-level justification:

- `SA1309`: disabled because the project convention explicitly requires private fields to start with `_`.
- `SA1500`: disabled because EIDO requires opening braces on the same line as declarations and control statements.

No broad `NoWarn` is used for StyleCop or XML documentation diagnostics.

## Local Verification

Run the repeatable full style check (headers, encoding, formatting and Debug/Release builds with warnings treated as errors):

```powershell
.\tools\Test-EidoStyle.ps1
```

The check excludes generated Designer files. It does not replace review of ownership, error handling or domain behavior. To build and run regressions separately:

```powershell
$msbuild = 'C:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\amd64\MSBuild.exe'
& $msbuild .\EidoTiaWorkbench.slnx /restore /t:Rebuild /p:Configuration=Debug /p:Platform=x64 /m
& $msbuild .\EidoTiaWorkbench.slnx /restore /t:Rebuild /p:Configuration=Release /p:Platform=x64 /m
.\EidoTiaWorkbench.Tests\bin\x64\Debug\EidoTiaWorkbench.Tests.exe
```

Apply safe formatting:

```powershell
dotnet format .\EidoTiaWorkbench.slnx whitespace
dotnet format .\EidoTiaWorkbench.slnx analyzers --diagnostics SA1101 SA1503 SA1501 SA1122 SA1413 SA1513 SA1516 SA1001 SA1009 SA1028 SA1111 SA1116
```

The output folder must not contain Siemens DLLs or StyleCop analyzer DLLs.
