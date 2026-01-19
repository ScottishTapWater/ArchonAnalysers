# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ArchonAnalysers is a Roslyn analyser package that enforces architectural rules through namespace-based conventions in C# projects. It is distributed as a NuGet package and integrates into the .NET build process to provide compile-time enforcement of architectural patterns.

## Common Commands

### Building
```bash
cd src
dotnet restore Archon.slnx
dotnet build Archon.slnx
```

### Running Tests
```bash
# Build first, then run using vstest
cd src
dotnet build Archon.slnx -c Release
dotnet vstest ArchonAnalysers.Tests.Unit/bin/Release/net10.0/ArchonAnalysers.Tests.Unit.dll
```

Note: The test project uses xunit 2.x with VSTest adapter. Use `dotnet vstest` rather than `dotnet test` due to the global.json test runner configuration.

### Packaging
```bash
cd src
dotnet pack ./ArchonAnalysers/ArchonAnalysers.csproj -o ./artifacts
```

## Architecture

### Roslyn Analyser Structure

ArchonAnalysers is a **Roslyn DiagnosticAnalyzer** project targeting .NET Standard 2.0 for broad compatibility. Key architectural points:

- **Analyser Registration**:
  - ARCHON001/002 use `RegisterSymbolAction` with `SymbolKind.NamedType` to analyse type declarations
  - ARCHON003/004 use `RegisterCompilationAction` to analyse assembly-level concerns
- **Namespace Pattern Matching**: Uses regex to match namespace patterns (e.g., `*.Internal*` for ARCHON001)
- **Masking Logic**: ARCHON001 implements recursive masking - nested types are exempt if their containing type already restricts visibility
- **Scope Filtering**: ARCHON002 only analyses top-level types, exempting nested types from public API requirements
- **MSBuild Integration**: ARCHON004 uses MSBuild targets to expose build properties via `CompilerVisibleProperty`

### Analyser Implementation Pattern

Both analysers follow this structure:
1. **Namespace filtering** - Skip if namespace doesn't match pattern
2. **Symbol masking** - Check if containing type already restricts visibility (ARCHON001 only)
3. **Accessibility checking** - Validate the type's accessibility against rules
4. **Diagnostic creation** - Report violation at the specific modifier token location

### NuGet Package Structure

- The project has `<IncludeBuildOutput>false</IncludeBuildOutput>` because analyser DLLs must be placed in `analyzers/dotnet/cs` path within the NuGet package
- Package includes the README.md file
- Uses `<DevelopmentDependency>true</DevelopmentDependency>` since it's a build-time tool
- Includes MSBuild targets in `build/` and `buildTransitive/` folders for exposing build properties to analysers (used by ARCHON004)

### Testing Architecture

- Uses `Microsoft.CodeAnalysis.CSharp.Analyzer.Testing` for testing Roslyn analysers
- Tests verify both positive cases (correct code) and negative cases (violations)
- The analyser project uses `InternalsVisibleTo` to allow tests to access internal members

## Implemented Analysers

### ARCHON001: Internals Are Internal
- **File**: `src/ArchonAnalysers/Analyzers/ARCHON001/InternalsAreInternalAnalyzer.cs`
- **Pattern**: Types in `*.Internal*` namespaces must be `internal`, `private`, or `private protected`
- **Severity**: Error
- **Key Implementation**: Recursive masking logic - if a containing type is already internal/private, nested types are exempt

### ARCHON002: Publics Are Public
- **File**: `src/ArchonAnalysers/Analyzers/ARCHON002/PublicsArePublicAnalyzer.cs`
- **Pattern**: Top-level types in `*.Public*` namespaces must be `public`, `protected`, or `protected internal`
- **Severity**: Warning
- **Key Implementation**: Only checks top-level types (no nesting logic needed)

### ARCHON003: Forbidden References
- **File**: `src/ArchonAnalysers/Analyzers/ARCHON003/ForbiddenReferencesAnalyser.cs`
- **Pattern**: Prevents assemblies from referencing other assemblies based on configurable rules
- **Severity**: Error
- **Key Implementation**: Uses `RegisterCompilationAction` to analyse all assembly references; configured via `.editorconfig` with `archon_003.forbidden_references` key using directional rules (e.g., `ProjectA->ProjectB`)

### ARCHON004: Packable Project Reference
- **File**: `src/ArchonAnalysers/Analyzers/ARCHON004/PackableProjectReferenceAnalyser.cs`
- **Pattern**: Packable NuGet projects should not reference other packable projects via `ProjectReference`
- **Severity**: Error
- **Key Implementation**: Uses MSBuild targets (`build/ArchonAnalysers.targets`) to expose `IsPackable` property and project reference packability map to the analyser via `CompilerVisibleProperty`. Uses `RegisterCompilationAction` to check references at build time.

## Configuration

ARCHON001 and ARCHON002 use configurable namespace slugs (`"Internal"` and `"Public"` by default).

Users can configure severity levels and rules in `.editorconfig`:
```editorconfig
[*.cs]
dotnet_diagnostic.ARCHON001.severity = error
dotnet_diagnostic.ARCHON002.severity = warning
dotnet_diagnostic.ARCHON003.severity = error
dotnet_diagnostic.ARCHON004.severity = error

# ARCHON003: Define forbidden reference rules (source->target format)
archon_003.forbidden_references = WebApp->Domain, Contracts->Infrastructure
```

ARCHON004 requires no additional configuration - it automatically detects packable projects via MSBuild properties.

## Technical Constraints

- **Target Framework**: .NET Standard 2.0 (analyser), .NET 10.0 (tests)
- **Solution Format**: Uses `.slnx` (XML-based) instead of traditional `.sln`
- **Roslyn Version**: Microsoft.CodeAnalysis.CSharp 4.11.0
- **Test Runner**: Tests are run directly via `dotnet <dll>`, not `dotnet test`