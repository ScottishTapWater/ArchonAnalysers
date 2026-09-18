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
# Build first, then run from output directory
cd src
dotnet build Archon.slnx -c Release
cd ArchonAnalysers.Tests.Unit/bin/Release/net10.0
dotnet vstest ArchonAnalysers.Tests.Unit.dll
```

Note: The test project currently uses xUnit v2 packages. Invoking the test DLL
directly exits without running or reporting the suite; use `dotnet vstest` and
verify the discovered/executed test count.

### Packaging
```bash
cd src
dotnet pack ./ArchonAnalysers/ArchonAnalysers.csproj -o ./artifacts
```

## Architecture

### Roslyn Analyser Structure

ArchonAnalysers is a **Roslyn DiagnosticAnalyzer** project targeting .NET Standard 2.0 for broad compatibility. Key architectural points:

- **Analyser Registration**: Analysers use `RegisterSymbolAction` with `SymbolKind.NamedType` to analyse type declarations during compilation
- **Namespace Pattern Matching**: Uses regex to match namespace patterns (e.g., `*.Internal*` for ARCHON001)
- **Masking Logic**: ARCHON001 implements recursive masking - nested types are exempt if their containing type already restricts visibility
- **Scope Filtering**: ARCHON002 only analyses top-level types, exempting nested types from public API requirements

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

## Configuration

Both analysers currently use hardcoded namespace slugs (`"Internal"` and `"Public"`). There are TODO comments in the code noting that these should eventually come from configuration files.

Users can configure severity levels in `.editorconfig`:
```editorconfig
[*.cs]
dotnet_diagnostic.ARCHON001.severity = error
dotnet_diagnostic.ARCHON002.severity = warning
```

## Technical Constraints

- **Target Framework**: .NET Standard 2.0 (analyser), .NET 10.0 (tests)
- **Solution Format**: Uses `.slnx` (XML-based) instead of traditional `.sln`
- **Roslyn Version**: Microsoft.CodeAnalysis.CSharp 4.11.0
- **Test Runner**: Tests are run via `dotnet vstest <dll>`
