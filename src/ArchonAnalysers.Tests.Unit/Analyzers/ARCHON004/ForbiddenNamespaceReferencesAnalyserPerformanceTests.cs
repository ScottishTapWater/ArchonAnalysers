using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using ArchonAnalysers.Analyzers.ARCHON004;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace ArchonAnalysers.Tests.Unit.Analyzers.ARCHON004;

public class ForbiddenNamespaceReferencesAnalyserPerformanceTests
{
    [Fact]
    public async Task LargeConfiguredTree_CompletesWithExpectedDiagnostics()
    {
        StringBuilder source = new("namespace Target { public class Secret; } namespace Source {");
        for (int index = 0; index < 500; index++)
        {
            source.Append("public class Type").Append(index)
                .Append(" { public Target.Secret? Value; }");
        }
        source.Append('}');

        CSharpCompilation compilation = CSharpCompilation.Create(
            "PerformanceProject",
            [CSharpSyntaxTree.ParseText(source.ToString())],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        AnalyzerOptions options = new(
            ImmutableArray<AdditionalText>.Empty,
            new TestConfigOptionsProvider("Source->Target"));
        CompilationWithAnalyzers compilationWithAnalyzers = compilation.WithAnalyzers(
            [new ForbiddenNamespaceReferencesAnalyser()],
            options);

        Stopwatch stopwatch = Stopwatch.StartNew();
        ImmutableArray<Diagnostic> diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
        stopwatch.Stop();

        Assert.Equal(500, diagnostics.Count(diagnostic => diagnostic.Id == ForbiddenNamespaceReferencesAnalyser.DiagnosticId));
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(15), $"Analyzer took {stopwatch.Elapsed}.");
    }

    private sealed class TestConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly AnalyzerConfigOptions _options;

        public TestConfigOptionsProvider(string rules) => _options = new TestConfigOptions(rules);

        public override AnalyzerConfigOptions GlobalOptions => _options;
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _options;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _options;
    }

    private sealed class TestConfigOptions : AnalyzerConfigOptions
    {
        private readonly string _rules;

        public TestConfigOptions(string rules) => _rules = rules;

        public override bool TryGetValue(string key, out string value)
        {
            if (key == ForbiddenNamespaceReferencesAnalyser.EditorConfigKey)
            {
                value = _rules;
                return true;
            }

            value = string.Empty;
            return false;
        }
    }
}
