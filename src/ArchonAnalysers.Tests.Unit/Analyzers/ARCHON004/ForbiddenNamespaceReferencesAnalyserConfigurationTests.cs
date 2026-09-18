using ArchonAnalysers.Analyzers.ARCHON004;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

using static ArchonAnalysers.Tests.Unit.Analyzers.ARCHON004.NamespaceReferenceTest;

namespace ArchonAnalysers.Tests.Unit.Analyzers.ARCHON004;

public class ForbiddenNamespaceReferencesAnalyserConfigurationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("SourceTarget")]
    [InlineData("->Target")]
    [InlineData("Source->")]
    public async Task MissingEmptyOrMalformedConfiguration_HasNoRestrictions(string? rules)
    {
        await RunAsync(
            "namespace Target { public class Secret; } namespace Source { public class Service { public Target.Secret? Value; } }",
            rules);
    }

    [Fact]
    public async Task WhitespaceAndMultipleRules_AreAccepted()
    {
        await RunAsync(
            """
            namespace Target.One { public class A; }
            namespace Target.Two { public class B; }
            namespace Source { public class Service { public Target.One.[|A|]? A; public Target.Two.[|B|]? B; } }
            """,
            "  Source  ->  Target.One  , Other -> Somewhere, Source -> Target.Two ");
    }

    [Fact]
    public async Task SameTreeAndOverlappingRules_AreNotExempt()
    {
        await RunAsync(
            "namespace App.Child { public class A { public App.Child.[|B|]? Value; } public class B; }",
            "App->App.Child");
    }

    [Fact]
    public async Task OverlappingRules_DoNotDuplicateOneSemanticReference()
    {
        await RunAsync(
            "namespace Target { public class Secret; } namespace Source.Child { public class Service { public Target.[|Secret|]? Value; } }",
            "Source->Target, Source.Child->Target");
    }

    [Fact]
    public async Task DiagnosticMessage_UsesActualSourceAndFirstMatchingRule()
    {
        CSharpAnalyzerTest<ForbiddenNamespaceReferencesAnalyser, DefaultVerifier> test = Create(
            "namespace Target.Child { public class Secret; } namespace Source.Child { public class Service { public Target.Child.{|#0:Secret|}? Value; } }",
            "Source->Target, Source.Child->Target.Child");

        test.ExpectedDiagnostics.Add(
            new DiagnosticResult(ForbiddenNamespaceReferencesAnalyser.DiagnosticId, DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("Source.Child", "Target.Child.Secret", "Target"));

        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task UnusedNamespaceImport_IsReported()
    {
        await RunAsync(
            """
            using [|Target.Child|];
            namespace Target.Child { public class Secret; }
            namespace Source { public class Service; }
            """,
            "Source->Target");
    }

    [Fact]
    public async Task AliasAndStaticImports_AreReported()
    {
        await RunAsync(
            """
            using Alias = [|Target.Secret|];
            using static [|Target.Utilities|];
            namespace Target { public class Secret; public static class Utilities { } }
            namespace Source { public class Service; }
            """,
            "Source->Target");
    }

    [Fact]
    public async Task AliasToWrappedType_CannotHideForbiddenConstituent()
    {
        await RunAsync(
            """
            [|using Secrets = Target.Secret[];|]
            namespace Target { public class Secret; }
            namespace Source { public class Service; }
            """,
            "Source->Target");
    }

    [Fact]
    public async Task Import_ReportsOnceForEachDistinctOverlappingRule()
    {
        CSharpAnalyzerTest<ForbiddenNamespaceReferencesAnalyser, DefaultVerifier> test = Create(
            "using {|#0:Target.Child|}; namespace Target.Child { public class Secret; } namespace Source { public class Service; }",
            "Source->Target, Source->Target.Child, Source->Target");

        test.ExpectedDiagnostics.Add(
            new DiagnosticResult(ForbiddenNamespaceReferencesAnalyser.DiagnosticId, DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("Source", "Target.Child", "Target"));
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult(ForbiddenNamespaceReferencesAnalyser.DiagnosticId, DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("Source", "Target.Child", "Target.Child"));

        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task NamespaceScopedImport_InheritsNamespaceScope()
    {
        await RunAsync(
            """
            namespace Source
            {
                using [|Target|];
                public class Service;
            }
            namespace Target { public class Secret; }
            """,
            "Source->Target");
    }

    [Fact]
    public async Task NamespaceScopedImport_AppliesWithoutDeclaredTypes()
    {
        await RunAsync(
            "namespace Source { using [|Target|]; } namespace Target { public class Secret; }",
            "Source->Target");
    }

    [Fact]
    public async Task FileImport_IsCheckedOncePerMatchingRuleAcrossApplicableTypes()
    {
        await RunAsync(
            """
            using [|Target|];
            namespace Target { public class Secret; }
            namespace Source.One { public class A; }
            namespace Source.Two { public class B; }
            """,
            "Source->Target");
    }

    [Fact]
    public async Task OrdinaryFileImport_DoesNotApplyToTypesInAnotherFile()
    {
        CSharpAnalyzerTest<ForbiddenNamespaceReferencesAnalyser, DefaultVerifier> test = Create(
            "using Target; namespace Allowed { public class Marker; }",
            "Source->Target");

        test.TestState.Sources.Add((
            "/Source.cs",
            "namespace Source { public class Service; } namespace Target { public class Secret; }"));

        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task GlobalImport_IsCheckedAgainstTypesAcrossCompilation()
    {
        CSharpAnalyzerTest<ForbiddenNamespaceReferencesAnalyser, DefaultVerifier> test = Create(
            """
            global using [|Target|];
            namespace ImportsOnly { public class Marker; }
            """,
            "Source->Target");

        test.TestState.Sources.Add((
            "/Source.cs",
            "namespace Source.Child { public class Service; } namespace Target { public class Secret; }"));

        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task GeneratedSource_IsAnalyzedNormally()
    {
        CSharpAnalyzerTest<ForbiddenNamespaceReferencesAnalyser, DefaultVerifier> test = Create(
            "namespace Target { public class Secret; }",
            "Source->Target");

        test.TestState.Sources.Add((
            "/Generated.g.cs",
            "// <auto-generated/>\nnamespace Source { public class Generated { public Target.[|Secret|]? Value; } }"));

        await test.RunAsync(CancellationToken.None);
    }

    private static async Task RunAsync(string code, string? configuredRules)
    {
        await Create(code, configuredRules).RunAsync(CancellationToken.None);
    }
}
