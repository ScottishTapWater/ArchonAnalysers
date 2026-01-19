using ArchonAnalysers.Analyzers.ARCHON004;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace ArchonAnalysers.Tests.Unit.Analyzers.ARCHON004;

public class PackableProjectReferenceAnalyserTests
{
    [Fact]
    public async Task PackableProjectReferencingPackableProject_ReportsError()
    {
        const string testCode = """
                                namespace TestApp;
                                public class MyClass;
                                """;

        const string globalConfig = """
                                    is_global = true
                                    build_property.IsPackable = true
                                    build_property._ArchonProjectRefPackabilityMap = OtherProject|true
                                    """;

        CSharpAnalyzerTest<PackableProjectReferenceAnalyser, DefaultVerifier> test = new()
        {
            TestCode = testCode,
            ExpectedDiagnostics =
            {
                new(PackableProjectReferenceAnalyser.DiagnosticId, DiagnosticSeverity.Error)
            }
        };

        test.TestState.AdditionalReferences.Add(CreateMockAssembly("OtherProject"));
        test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", globalConfig));

        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task PackableProjectReferencingNonPackableProject_NoError()
    {
        const string testCode = """
                                namespace TestApp;
                                public class MyClass;
                                """;

        const string globalConfig = """
                                    is_global = true
                                    build_property.IsPackable = true
                                    build_property._ArchonProjectRefPackabilityMap = OtherProject|false
                                    """;

        CSharpAnalyzerTest<PackableProjectReferenceAnalyser, DefaultVerifier> test = new()
        {
            TestCode = testCode
        };

        test.TestState.AdditionalReferences.Add(CreateMockAssembly("OtherProject"));
        test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", globalConfig));

        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task NonPackableProjectReferencingPackableProject_NoError()
    {
        const string testCode = """
                                namespace TestApp;
                                public class MyClass;
                                """;

        const string globalConfig = """
                                    is_global = true
                                    build_property.IsPackable = false
                                    build_property._ArchonProjectRefPackabilityMap = OtherProject|true
                                    """;

        CSharpAnalyzerTest<PackableProjectReferenceAnalyser, DefaultVerifier> test = new()
        {
            TestCode = testCode
        };

        test.TestState.AdditionalReferences.Add(CreateMockAssembly("OtherProject"));
        test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", globalConfig));

        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task NonPackableProjectReferencingNonPackableProject_NoError()
    {
        const string testCode = """
                                namespace TestApp;
                                public class MyClass;
                                """;

        const string globalConfig = """
                                    is_global = true
                                    build_property.IsPackable = false
                                    build_property._ArchonProjectRefPackabilityMap = OtherProject|false
                                    """;

        CSharpAnalyzerTest<PackableProjectReferenceAnalyser, DefaultVerifier> test = new()
        {
            TestCode = testCode
        };

        test.TestState.AdditionalReferences.Add(CreateMockAssembly("OtherProject"));
        test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", globalConfig));

        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task MissingIsPackableProperty_DefaultsToFalse_NoError()
    {
        const string testCode = """
                                namespace TestApp;
                                public class MyClass;
                                """;

        const string globalConfig = """
                                    is_global = true
                                    build_property._ArchonProjectRefPackabilityMap = OtherProject|true
                                    """;

        CSharpAnalyzerTest<PackableProjectReferenceAnalyser, DefaultVerifier> test = new()
        {
            TestCode = testCode
        };

        test.TestState.AdditionalReferences.Add(CreateMockAssembly("OtherProject"));
        test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", globalConfig));

        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task NoProjectReferences_NoError()
    {
        const string testCode = """
                                namespace TestApp;
                                public class MyClass;
                                """;

        const string globalConfig = """
                                    is_global = true
                                    build_property.IsPackable = true
                                    """;

        CSharpAnalyzerTest<PackableProjectReferenceAnalyser, DefaultVerifier> test = new()
        {
            TestCode = testCode
        };

        test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", globalConfig));

        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task MultiplePackableProjectReferences_ReportsMultipleErrors()
    {
        const string testCode = """
                                namespace TestApp;
                                public class MyClass;
                                """;

        const string globalConfig = """
                                    is_global = true
                                    build_property.IsPackable = true
                                    build_property._ArchonProjectRefPackabilityMap = ProjectA|true,ProjectB|true
                                    """;

        CSharpAnalyzerTest<PackableProjectReferenceAnalyser, DefaultVerifier> test = new()
        {
            TestCode = testCode,
            ExpectedDiagnostics =
            {
                new(PackableProjectReferenceAnalyser.DiagnosticId, DiagnosticSeverity.Error),
                new(PackableProjectReferenceAnalyser.DiagnosticId, DiagnosticSeverity.Error)
            }
        };

        test.TestState.AdditionalReferences.Add(CreateMockAssembly("ProjectA"));
        test.TestState.AdditionalReferences.Add(CreateMockAssembly("ProjectB"));
        test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", globalConfig));

        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task MixedPackableAndNonPackableReferences_OnlyPackableReportsError()
    {
        const string testCode = """
                                namespace TestApp;
                                public class MyClass;
                                """;

        const string globalConfig = """
                                    is_global = true
                                    build_property.IsPackable = true
                                    build_property._ArchonProjectRefPackabilityMap = PackableProject|true,NonPackableProject|false
                                    """;

        CSharpAnalyzerTest<PackableProjectReferenceAnalyser, DefaultVerifier> test = new()
        {
            TestCode = testCode,
            ExpectedDiagnostics =
            {
                new(PackableProjectReferenceAnalyser.DiagnosticId, DiagnosticSeverity.Error)
            }
        };

        test.TestState.AdditionalReferences.Add(CreateMockAssembly("PackableProject"));
        test.TestState.AdditionalReferences.Add(CreateMockAssembly("NonPackableProject"));
        test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", globalConfig));

        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task CaseInsensitiveIsPackable_ReportsError()
    {
        const string testCode = """
                                namespace TestApp;
                                public class MyClass;
                                """;

        const string globalConfig = """
                                    is_global = true
                                    build_property.IsPackable = TRUE
                                    build_property._ArchonProjectRefPackabilityMap = OtherProject|TRUE
                                    """;

        CSharpAnalyzerTest<PackableProjectReferenceAnalyser, DefaultVerifier> test = new()
        {
            TestCode = testCode,
            ExpectedDiagnostics =
            {
                new(PackableProjectReferenceAnalyser.DiagnosticId, DiagnosticSeverity.Error)
            }
        };

        test.TestState.AdditionalReferences.Add(CreateMockAssembly("OtherProject"));
        test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", globalConfig));

        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ReferenceNotInMap_NoError()
    {
        const string testCode = """
                                namespace TestApp;
                                public class MyClass;
                                """;

        const string globalConfig = """
                                    is_global = true
                                    build_property.IsPackable = true
                                    build_property._ArchonProjectRefPackabilityMap = SomeOtherProject|true
                                    """;

        CSharpAnalyzerTest<PackableProjectReferenceAnalyser, DefaultVerifier> test = new()
        {
            TestCode = testCode
        };

        test.TestState.AdditionalReferences.Add(CreateMockAssembly("UnmappedProject"));
        test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", globalConfig));

        await test.RunAsync(CancellationToken.None);
    }

    private static MetadataReference CreateMockAssembly(string name, string code = "")
    {
        if (string.IsNullOrEmpty(code))
        {
            code = $"namespace {name} {{ public class Class1 {{ }} }}";
        }

        CSharpCompilation compilation = CSharpCompilation.Create(
            name,
            syntaxTrees: [CSharpSyntaxTree.ParseText(code)],
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            options: new(OutputKind.DynamicallyLinkedLibrary));

        using MemoryStream ms = new();
        EmitResult result = compilation.Emit(ms);
        if (!result.Success)
        {
            string errors = string.Join(", ", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            throw new InvalidOperationException($"Failed to emit assembly {name}: {errors}");
        }
        ms.Seek(0, SeekOrigin.Begin);
        return MetadataReference.CreateFromStream(ms);
    }
}
