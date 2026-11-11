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

    [Theory]
    [InlineData("GoSim.Motors.Core.Features.*", "GoSim.Motors.Core.Features.Winding", true)]
    [InlineData("GoSim.Motors.Core.Features.*", "GoSim.Motors.Core.Features.Winding.Contracts", true)]
    [InlineData("gosim.motors.core.features.*", "GoSim.Motors.Core.Features.Winding", true)]
    [InlineData("GoSim.Motors.Core.Features.*.dll", "GoSim.Motors.Core.Features.Winding", true)]
    [InlineData("GoSim.Motors.Core.Features.*", "GoSim.Motors.Core.Features", false)]
    [InlineData("GoSim.Motors.Core.Features.*", "GoSim.Motors.Core.FeaturesExtra.Winding", false)]
    [InlineData("GoSim.Motors.Core.Features.*", "Other.GoSim.Motors.Core.Features.Winding", false)]
    [InlineData("GoSim.Motors.Core.Features.*", "GoSimXMotors.Core.Features.Winding", false)]
    [InlineData("*.Features.Winding", "GoSim.Motors.Core.Features.Winding", true)]
    [InlineData("*.Features.Winding", "GoSim.Motors.Core.Features.WindingExtra", false)]
    [InlineData("GoSim.*.Features.*", "GoSim.Motors.Core.Features.Winding", true)]
    [InlineData("GoSim.*.Features.*", "GoSim.Motors.Core.Other.Winding", false)]
    [InlineData("GoSim.Motors.Core.Features*", "GoSim.Motors.Core.Features", true)]
    public async Task WildcardTarget_MatchesAssemblyName(string target, string assemblyName, bool forbidden)
    {
        CSharpAnalyzerTest<ForbiddenReferencesAnalyser, DefaultVerifier> test = new()
        {
            TestCode = "public class MyClass;"
        };

        if (forbidden)
        {
            test.ExpectedDiagnostics.Add(
                new DiagnosticResult(ForbiddenReferencesAnalyser.DiagnosticId, DiagnosticSeverity.Error)
                    .WithArguments("TestProject", assemblyName));
        }

        test.TestState.AdditionalReferences.Add(CreateMockAssembly(assemblyName));
        ArchonSettingsTest.Add(test, $"archon_003.forbidden_references = TestProject->{target}");

        await test.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task OverlappingWildcardAndExactTargets_ReportOneDiagnosticPerReference()
    {
        const string forbiddenAssembly = "GoSim.Motors.Core.Features.Winding";
        CSharpAnalyzerTest<ForbiddenReferencesAnalyser, DefaultVerifier> test = new()
        {
            TestCode = "public class MyClass;",
            ExpectedDiagnostics =
            {
                new DiagnosticResult(ForbiddenReferencesAnalyser.DiagnosticId, DiagnosticSeverity.Error)
                    .WithArguments("TestProject", forbiddenAssembly)
            }
        };

        test.TestState.AdditionalReferences.Add(CreateMockAssembly(forbiddenAssembly));
        test.TestState.AdditionalReferences.Add(CreateMockAssembly("GoSim.Motors.Core.Contracts"));
        ArchonSettingsTest.Add(test,
            $"archon_003.forbidden_references = TestProject->GoSim.Motors.Core.Features.*, TestProject->{forbiddenAssembly}");

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
