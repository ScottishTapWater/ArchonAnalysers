# ArchonAnalysers

[![CI Pipeline](https://img.shields.io/github/actions/workflow/status/hughesjs/ArchonAnalysers/ci-pipeline.yml?style=for-the-badge&logo=github&label=CI)](https://github.com/hughesjs/ArchonAnalysers/actions/workflows/ci-pipeline.yml)
[![CD Pipeline](https://img.shields.io/github/actions/workflow/status/hughesjs/ArchonAnalysers/cd-pipeline.yml?style=for-the-badge&logo=github&label=CD)](https://github.com/hughesjs/ArchonAnalysers/actions/workflows/cd-pipeline.yml)
[![NuGet](https://img.shields.io/nuget/v/ArchonAnalysers?style=for-the-badge&logo=nuget)](https://www.nuget.org/packages/ArchonAnalysers/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ArchonAnalysers?style=for-the-badge)](https://www.nuget.org/packages/ArchonAnalysers/)
[![License](https://img.shields.io/github/license/hughesjs/ArchonAnalysers?style=for-the-badge)](https://github.com/hughesjs/ArchonAnalysers/blob/master/LICENSE)
[![Made in Scotland](https://raw.githubusercontent.com/hughesjs/custom-badges/master/made-in/made-in-scotland.svg)](https://github.com/hughesjs/custom-badges)

Roslyn analysers for enforcing architectural rules in C# projects.

## Installation

```bash
dotnet add package ArchonAnalysers
```

## Features

ArchonAnalysers provides Roslyn analysers that enforce namespace-based architectural rules:

### ARCHON001: Internals Are Internal

Ensures that all types within namespaces containing `.Internal` are properly restricted with `internal` or `private` access modifiers. This prevents accidental exposure of internal implementation details.

- **Severity**: Error
- **Namespace Pattern**: `*.Internal*` (e.g., `MyApp.Internal`, `MyApp.Services.Internal`)
- **Allowed Modifiers**: `internal`, `private`, `private protected`
- **Special Handling**: Nested types are exempt if their containing type is already `internal` or `private`

### ARCHON002: Publics Are Public

Ensures that top-level types within namespaces containing `.Public` are appropriately exposed with `public` or `protected` access modifiers. This enforces discoverability of your public API surface.

- **Severity**: Warning
- **Namespace Pattern**: `*.Public*` (e.g., `MyApp.Public`, `MyApp.Api.Public`)
- **Required Modifiers**: `public`, `protected`, `protected internal`
- **Scope**: Only applies to top-level types (nested types are exempt)

### ARCHON003: Forbidden Assembly References

Prevents specified projects from referencing forbidden assemblies at compile time, enforcing architectural layering rules.

- **Severity**: Error
- **Configuration**: `archon_003.forbidden_references` (directional rules: `Source->Target`)
- **Matching**: Simple assembly name, case-insensitive, .dll extension optional
- **Scope**: Global configuration in single EditorConfig

#### Code Fix

ARCHON003 provides a code fix that automatically removes forbidden `<ProjectReference>` entries from `.csproj` files.

**Limitations:**
- Only works for SDK-style projects (`.csproj` with `<Project Sdk="...">`)
- Only removes `<ProjectReference>` elements (not `<PackageReference>` or `<Reference>`)
- Assumes project name matches assembly name
- Fix available from Error List window (not in-editor, due to compilation-level diagnostics)
- Requires project reload in IDE after applying fix

**To use:** Right-click the diagnostic in the Error List and select "Remove project reference to [AssemblyName]"

### ARCHON004: Forbidden Namespace References

Prevents code declared in configured source namespaces from referring to types or imports in configured target namespaces. This provides finer-grained dependency boundaries within an assembly as well as across assemblies.

- **Severity**: Error
- **Configuration**: `archon_004.forbidden_namespace_references` (directional rules: `Source->Target`)
- **Matching**: Case-sensitive, on complete namespace segments
- **Scope**: All declared type kinds, nested types, generated code, and referenced assemblies
- **Code fix**: None; removing an architectural dependency requires design judgment

## Usage

Once installed, the analysers will automatically run during compilation and highlight violations in your IDE.

## Configuration

### Customising Namespace Patterns

Both analysers support customising which namespace patterns trigger the rules via `.editorconfig`:

#### ARCHON001: Internal Namespace Slugs

```editorconfig
[*.cs]
# Single namespace slug (default: Internal)
archon_001.internal_namespace_slugs = Internal

# Multiple namespace slugs
archon_001.internal_namespace_slugs = Internal, Private, Hidden, Impl
```

Types in namespaces matching `*.Internal.*`, `*.Private.*`, `*.Hidden.*`, or `*.Impl.*` must be internal, private, or private protected.

#### ARCHON002: Public Namespace Slugs

```editorconfig
[*.cs]
# Single namespace slug (default: Public)
archon_002.public_namespace_slugs = Public

# Multiple namespace slugs
archon_002.public_namespace_slugs = Public, Api, Exposed, Contract
```

Top-level types in namespaces matching `*.Public.*`, `*.Api.*`, `*.Exposed.*`, or `*.Contract.*` must be public, protected, or protected internal.

**Notes:**
- Slugs are comma-separated with automatic whitespace trimming
- Empty or missing configuration uses defaults ("Internal" for ARCHON001, "Public" for ARCHON002)
- Slugs match complete namespace segments (e.g., "Internal" matches `App.Internal.Services` but not `App.InternalStuff`)
- Special regex characters are automatically escaped

#### ARCHON003: Forbidden Assembly References

Configure directional rules in a **single global EditorConfig** file:

```editorconfig
# Single global .editorconfig at solution root
[*.cs]
archon_003.forbidden_references = Contracts->Domain, Contracts->Application, Domain->Application
```

This enforces:
- Contracts → Domain: ❌ Forbidden
- Contracts → Application: ❌ Forbidden
- Domain → Application: ❌ Forbidden
- Domain → Contracts: ✅ Allowed
- Application → Domain: ✅ Allowed
- Application → Contracts: ✅ Allowed

##### Syntax

**Directional format:**
```editorconfig
archon_003.forbidden_references = Source->Target, AnotherSource->AnotherTarget
```

**Notes:**
- Assembly names are matched without version, culture, or public key token
- Matching is case-insensitive for both source and target
- The `.dll` extension is optional in configuration
- Whitespace around `->` is automatically trimmed
- Empty or missing configuration means no restrictions
- Rules must use the `Source->Target` format

#### ARCHON004: Forbidden Namespace References

Configure one or more directional namespace rules:

```editorconfig
[*.cs]
archon_004.forbidden_namespace_references = MyApp.Domain->MyApp.Infrastructure, MyApp.Contracts->MyApp.Application
```

Each configured namespace includes itself and all child namespaces on both sides of the rule. For example, `MyApp.Domain` includes `MyApp.Domain.Services`, and `MyApp.Infrastructure` includes `MyApp.Infrastructure.Data`. Matching uses exact namespace-segment boundaries, so `MyApp.DomainModels` is not included, and matching is case-sensitive.

ARCHON004 checks explicit and inferred semantic dependencies, including:

- Fields, properties, events, parameters, return values, locals, tuples, arrays, nullable types, and constructed generic types
- Object construction, method calls, extension methods, and instance or static member access
- Base types, implemented interfaces, generic constraints, attributes, casts, patterns, `typeof`, and `nameof`
- `var`, target-typed construction, aliases, and fully qualified references
- Namespace, alias, `using static`, file-level, and `global using` imports, including unused imports

Compilation-unit imports are checked against the declared types in that file. Global imports are checked against applicable declared types throughout the compilation. Namespace-scoped imports use their containing namespace as the source scope.

Missing or empty configuration means no namespace restrictions. Whitespace around rules is ignored, malformed entries are skipped, and multiple rules are comma-separated. Unresolved symbols are ignored so incomplete source does not produce speculative ARCHON004 diagnostics.

### Severity Configuration

Configure severity levels in your `.editorconfig`:

```editorconfig
[*.cs]
# Enforce internal types in configured namespaces (default: error)
dotnet_diagnostic.ARCHON001.severity = error

# Enforce public types in configured namespaces (default: warning)
dotnet_diagnostic.ARCHON002.severity = warning

# Enforce forbidden assembly references (default: error)
dotnet_diagnostic.ARCHON003.severity = error

# Enforce forbidden namespace references (default: error)
dotnet_diagnostic.ARCHON004.severity = error
```

### Example

#### Namespace Rules (ARCHON001 & ARCHON002)

```csharp
namespace MyApp.Internal
{
    // ✅ Correct - internal type in .Internal namespace
    internal class InternalService { }

    // ❌ ARCHON001 violation - public type in .Internal namespace
    public class PublicService { }
}

namespace MyApp.Public
{
    // ✅ Correct - public type in .Public namespace
    public class PublicApi { }

    // ❌ ARCHON002 violation - internal type in .Public namespace
    internal class InternalApi { }
}
```

#### Forbidden Namespace References (ARCHON004)

With `MyApp.Domain->MyApp.Infrastructure` configured:

```csharp
namespace MyApp.Domain.Services;

// ❌ ARCHON004: MyApp.Domain.Services cannot reference a type in
// MyApp.Infrastructure or any of its child namespaces.
public sealed class OrderService(MyApp.Infrastructure.Data.OrderStore store)
{
    // Inferred references are checked too.
    public object Load() => MyApp.Infrastructure.Data.Repository.Load();
}
```

## Development

### Prerequisites

- .NET 10.0 SDK or later
- (Optional) `act` for local CI/CD testing

### Building

```bash
cd src
dotnet restore Archon.slnx
dotnet build Archon.slnx
```

### Testing

```bash
cd src/ArchonAnalysers.Tests.Unit/bin/Release/net10.0
dotnet vstest ArchonAnalysers.Tests.Unit.dll
```

### Local CI/CD Testing

Test the CI pipeline locally:
```bash
./scripts/test-ci.sh
```

Test the CD pipeline locally:
```bash
./scripts/test-cd.sh
```

## Licence

This project is licensed under the MIT Licence - see the [LICENSE](LICENSE) file for details.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.
