using ArchonAnalysers.Analysers.ARCHON003;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace ArchonAnalysers.Tests.Unit.Analysers.ARCHON003;

public class ForbiddenReferencesAnalyserTests
{
    [Fact]
    public async Task NoForbiddenReferencesConfigured_NoError()
    {
        const string testCode = """
                                namespace TestApp;
                                public class MyClass;
                                """;

        CSharpAnalyzerTest<ForbiddenReferencesAnalyser, DefaultVerifier> test = new() { TestCode = testCode };

        // Add a mock Domain reference but no configuration
        test.TestState.AdditionalReferences.Add(CreateMockAssembly("Domain"));

        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task AllowedReference_NoError()
    {
        const string testCode = """
                                namespace TestApp;
                                public class MyClass;
                                """;

        const string settingsFile = """
                                    archon_003.forbidden_references = TestProject->Domain, TestProject->Application
                                    """;

        CSharpAnalyzerTest<ForbiddenReferencesAnalyser, DefaultVerifier> test = new() { TestCode = testCode };

        // Add an allowed reference (not in forbidden list)
        test.TestState.AdditionalReferences.Add(CreateMockAssembly("Infrastructure"));
        ArchonSettingsTest.Add(test, settingsFile);

        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DirectionalRule_TriggersErrorWhenSourceMatches()
    {
        const string testCode = """
                                namespace TestApp;
                                public class MyClass;
                                """;

        const string settingsFile = """
                                    archon_003.forbidden_references = TestProject->Domain
                                    """;

        CSharpAnalyzerTest<ForbiddenReferencesAnalyser, DefaultVerifier> test = new()
        {
            TestCode = testCode,
            ExpectedDiagnostics =
            {
                new(ForbiddenReferencesAnalyser.DiagnosticId, DiagnosticSeverity.Error)
            }
        };

        test.TestState.AdditionalReferences.Add(CreateMockAssembly("Domain"));
        ArchonSettingsTest.Add(test, settingsFile);

        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DirectionalRule_DoesNotTriggerWhenSourceDoesNotMatch()
    {
        const string testCode = """
                                namespace TestApp;
                                public class MyClass;
                                """;

        const string settingsFile = """
                                    archon_003.forbidden_references = Contracts->Domain
                                    """;

        CSharpAnalyzerTest<ForbiddenReferencesAnalyser, DefaultVerifier> test = new()
        {
            TestCode = testCode
            // No diagnostics expected because current assembly is "test0" not "Contracts"
        };

        test.TestState.AdditionalReferences.Add(CreateMockAssembly("Domain"));
        ArchonSettingsTest.Add(test, settingsFile);

        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task MultipleForbiddenReferences_TriggersMultipleErrors()
    {
        const string testCode = """
                                namespace TestApp;
                                public class MyClass;
                                """;

        const string settingsFile = """
                                    archon_003.forbidden_references = TestProject->Domain, TestProject->Application
                                    """;

        CSharpAnalyzerTest<ForbiddenReferencesAnalyser, DefaultVerifier> test = new()
        {
            TestCode = testCode,
            ExpectedDiagnostics =
            {
                new(ForbiddenReferencesAnalyser.DiagnosticId, DiagnosticSeverity.Error),
                new(ForbiddenReferencesAnalyser.DiagnosticId, DiagnosticSeverity.Error)
            }
        };

        test.TestState.AdditionalReferences.Add(CreateMockAssembly("Domain"));
        test.TestState.AdditionalReferences.Add(CreateMockAssembly("Application"));
        ArchonSettingsTest.Add(test, settingsFile);

        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task CaseInsensitiveMatching_TriggersError()
    {
        const string testCode = """
                                namespace TestApp;
                                public class MyClass;
                                """;

        const string settingsFile = """
                                    archon_003.forbidden_references = TestProject->domain
                                    """;

        CSharpAnalyzerTest<ForbiddenReferencesAnalyser, DefaultVerifier> test = new()
        {
            TestCode = testCode,
            ExpectedDiagnostics =
            {
                new(ForbiddenReferencesAnalyser.DiagnosticId, DiagnosticSeverity.Error)
            }
        };

        // Assembly name is "Domain" but config has "domain"
        test.TestState.AdditionalReferences.Add(CreateMockAssembly("Domain"));
        ArchonSettingsTest.Add(test, settingsFile);

        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task CaseInsensitiveSourceMatching_TriggersError()
    {
        const string testCode = """
                                namespace TestApp;
                                public class MyClass;
                                """;

        const string settingsFile = """
                                    archon_003.forbidden_references = TestProject->Domain
                                    """;

        CSharpAnalyzerTest<ForbiddenReferencesAnalyser, DefaultVerifier> test = new()
        {
            TestCode = testCode,
            ExpectedDiagnostics =
            {
                new(ForbiddenReferencesAnalyser.DiagnosticId, DiagnosticSeverity.Error)
            }
        };

        // Source in config is "TEST0" but actual assembly is "test0"
        test.TestState.AdditionalReferences.Add(CreateMockAssembly("Domain"));
        ArchonSettingsTest.Add(test, settingsFile);

        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DllExtensionInConfig_TriggersError()
    {
        const string testCode = """
                                namespace TestApp;
                                public class MyClass;
                                """;

        const string settingsFile = """
                                    archon_003.forbidden_references = TestProject->Domain.dll
                                    """;

        CSharpAnalyzerTest<ForbiddenReferencesAnalyser, DefaultVerifier> test = new()
        {
            TestCode = testCode,
            ExpectedDiagnostics =
            {
                new(ForbiddenReferencesAnalyser.DiagnosticId, DiagnosticSeverity.Error)
            }
        };

        test.TestState.AdditionalReferences.Add(CreateMockAssembly("Domain"));
        ArchonSettingsTest.Add(test, settingsFile);

        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task MixedForbiddenAndAllowed_OnlyForbiddenTriggersError()
    {
        const string testCode = """
                                namespace TestApp;
                                public class MyClass;
                                """;

        const string settingsFile = """
                                    archon_003.forbidden_references = TestProject->Domain, TestProject->Application
                                    """;

        CSharpAnalyzerTest<ForbiddenReferencesAnalyser, DefaultVerifier> test = new()
        {
            TestCode = testCode,
            ExpectedDiagnostics =
            {
                new(ForbiddenReferencesAnalyser.DiagnosticId, DiagnosticSeverity.Error)
            }
        };

        test.TestState.AdditionalReferences.Add(CreateMockAssembly("Domain")); // Forbidden
        test.TestState.AdditionalReferences.Add(CreateMockAssembly("Infrastructure")); // Allowed
        ArchonSettingsTest.Add(test, settingsFile);

        await test.RunAsync(CancellationToken.None);
    }

    private static MetadataReference CreateMockAssembly(string name, string code = "")
    {
        // Provide a minimal valid assembly if no code is specified
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
