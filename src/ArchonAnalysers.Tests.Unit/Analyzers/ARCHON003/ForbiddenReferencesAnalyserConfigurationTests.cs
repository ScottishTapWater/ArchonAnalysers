using ArchonAnalysers.Analysers.ARCHON003;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace ArchonAnalysers.Tests.Unit.Analysers.ARCHON003;

public class ForbiddenReferencesAnalyserConfigurationTests
{
    [Fact]
    public async Task CustomSingleForbiddenReference_TriggersOnConfiguredAssembly()
    {
        const string testCode = """
                                namespace TestApp;
                                public class MyClass;
                                """;

        const string settingsFile = """
                                    archon_003.forbidden_references = TestProject->CustomDomain
                                    """;

        CSharpAnalyzerTest<ForbiddenReferencesAnalyser, DefaultVerifier> test = new()
        {
            TestCode = testCode,
            ExpectedDiagnostics =
            {
                new(ForbiddenReferencesAnalyser.DiagnosticId, DiagnosticSeverity.Error)
            }
        };

        test.TestState.AdditionalReferences.Add(CreateMockAssembly("CustomDomain"));
        ArchonSettingsTest.Add(test, settingsFile);

        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task CustomSingleForbiddenReference_DoesNotTriggerOnNonConfiguredAssembly()
    {
        const string testCode = """
                                namespace TestApp;
                                public class MyClass;
                                """;

        const string settingsFile = """
                                    archon_003.forbidden_references = TestProject->CustomDomain
                                    """;

        CSharpAnalyzerTest<ForbiddenReferencesAnalyser, DefaultVerifier> test = new() { TestCode = testCode };

        test.TestState.AdditionalReferences.Add(CreateMockAssembly("Domain")); // Different name
        ArchonSettingsTest.Add(test, settingsFile);

        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task MultipleForbiddenReferences_TriggersOnAllConfigured()
    {
        const string testCode = """
                                namespace TestApp;
                                public class MyClass;
                                """;

        const string settingsFile = """
                                    archon_003.forbidden_references = TestProject->Domain, TestProject->Application, TestProject->Infrastructure
                                    """;

        CSharpAnalyzerTest<ForbiddenReferencesAnalyser, DefaultVerifier> test = new()
        {
            TestCode = testCode,
            ExpectedDiagnostics =
            {
                new(ForbiddenReferencesAnalyser.DiagnosticId, DiagnosticSeverity.Error),
                new(ForbiddenReferencesAnalyser.DiagnosticId, DiagnosticSeverity.Error),
                new(ForbiddenReferencesAnalyser.DiagnosticId, DiagnosticSeverity.Error)
            }
        };

        test.TestState.AdditionalReferences.Add(CreateMockAssembly("Domain"));
        test.TestState.AdditionalReferences.Add(CreateMockAssembly("Application"));
        test.TestState.AdditionalReferences.Add(CreateMockAssembly("Infrastructure"));
        ArchonSettingsTest.Add(test, settingsFile);

        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task MultipleForbiddenReferences_DoesNotTriggerOnNonConfigured()
    {
        const string testCode = """
                                namespace TestApp;
                                public class MyClass;
                                """;

        const string settingsFile = """
                                    archon_003.forbidden_references = TestProject->Domain, TestProject->Application
                                    """;

        CSharpAnalyzerTest<ForbiddenReferencesAnalyser, DefaultVerifier> test = new() { TestCode = testCode };

        test.TestState.AdditionalReferences.Add(CreateMockAssembly("Infrastructure"));
        test.TestState.AdditionalReferences.Add(CreateMockAssembly("Utilities"));
        ArchonSettingsTest.Add(test, settingsFile);

        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task EmptyConfiguration_NoRestrictions()
    {
        const string testCode = """
                                namespace TestApp;
                                public class MyClass;
                                """;

        const string settingsFile = """
                                    archon_003.forbidden_references =
                                    """;

        CSharpAnalyzerTest<ForbiddenReferencesAnalyser, DefaultVerifier> test = new() { TestCode = testCode };

        test.TestState.AdditionalReferences.Add(CreateMockAssembly("Domain"));
        test.TestState.AdditionalReferences.Add(CreateMockAssembly("Application"));
        ArchonSettingsTest.Add(test, settingsFile);

        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task WhitespaceInConfiguration_ProperlyTrimmed()
    {
        const string testCode = """
                                namespace TestApp;
                                public class MyClass;
                                """;

        const string settingsFile = """
                                    archon_003.forbidden_references =  TestProject -> Domain  ,  TestProject  ->  Application
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
    public async Task DllExtensionInMultipleReferences_ProperlyHandled()
    {
        const string testCode = """
                                namespace TestApp;
                                public class MyClass;
                                """;

        const string settingsFile = """
                                    archon_003.forbidden_references = TestProject->Domain.dll, TestProject->Application.dll
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
