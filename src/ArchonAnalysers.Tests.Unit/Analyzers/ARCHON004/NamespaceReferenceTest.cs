using ArchonAnalysers.Analyzers.ARCHON004;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Testing;

namespace ArchonAnalysers.Tests.Unit.Analyzers.ARCHON004;

internal static class NamespaceReferenceTest
{
    public static CSharpAnalyzerTest<ForbiddenNamespaceReferencesAnalyser, DefaultVerifier> Create(
        string code,
        string? configuredRules,
        CompilerDiagnostics compilerDiagnostics = CompilerDiagnostics.Errors)
    {
        CSharpAnalyzerTest<ForbiddenNamespaceReferencesAnalyser, DefaultVerifier> test = new()
        {
            TestCode = code,
            CompilerDiagnostics = compilerDiagnostics
        };

        if (configuredRules != null)
        {
            test.TestState.AnalyzerConfigFiles.Add((
                "/.editorconfig",
                $"""
                root = true

                [*.cs]
                archon_004.forbidden_namespace_references = {configuredRules}
                """));
        }

        return test;
    }

    public static async Task RunAsync(
        string code,
        string? configuredRules,
        params MetadataReference[] references)
    {
        CSharpAnalyzerTest<ForbiddenNamespaceReferencesAnalyser, DefaultVerifier> test =
            Create(code, configuredRules);
        test.TestState.AdditionalReferences.AddRange(references);
        await test.RunAsync(CancellationToken.None);
    }

    public static MetadataReference CreateReference(string assemblyName, string code)
    {
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(code)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using MemoryStream stream = new();
        EmitResult result = compilation.Emit(stream);
        if (!result.Success)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics));
        }

        return MetadataReference.CreateFromImage(stream.ToArray());
    }
}
